---
version: 0.1
status: Active
date: 2026-07-04
references:
  - docs/retrospectives/implementations/036-slices-5-5-5-7-email-change-saga.md
  - docs/demo-runbook.md
  - docs/workshops/002-identity-event-model.md
  - docs/decisions/017-critterwatch-integrated.md
  - src/CritterMart.Identity/Customers/EmailChange.cs
  - src/CritterMart.Identity/Customers/EmailChangeTimeout.cs
  - src/CritterMart.Identity/Program.cs
  - src/CritterMart.Inventory/Program.cs
---

# Research: does CritterWatch beta.1 surface the EmailChange saga? (retro 036 open item, closed)

> **What this is.** The empirical close-out of the one item [retro
> `implementations/036`](../retrospectives/implementations/036-slices-5-5-5-7-email-change-saga.md) left
> open — *"CritterWatch's saga-lifecycle visual confirmation … not performed this session (no browser
> automation available)."* A later session (2026-07-04) booted the full Aspire stack, drove the live saga,
> and inspected the CritterWatch console **and** the underlying Postgres directly. This records the answer.
>
> **What it is not.** Not a decision and not a bug report against the saga. The `EmailChange` saga is
> **fully verified and correct** at every level (19/19 Identity tests green against real Postgres; live
> request→confirm→applied, request→timeout→dropped, and 404/409 guard flows all driven against the running
> stack). The two negative observations below are **CritterWatch-version + Wolverine-configuration facts**,
> not saga defects. Any binding change (e.g. adopting durable local queues) would graduate to an ADR/slice.

## Bottom line

**CritterWatch beta.1 does not visually surface the `EmailChange` saga — for two independent reasons, neither a saga defect:**

1. **The saga-instance view is a pre-1.0 stub.** The console's **Explore → Workflow** page (the beta.1
   successor to what the demo-runbook calls the "Saga Explorer") renders its own incompleteness banners:
   *"This feature is heavily in flight, but planned for 1.0,"* *"Structural discovery unavailable — observed
   only,"* and *"structural discovery unavailable (no source-gen / declared model); showing observed only."*
   It replays **observed runtime message traffic**, not saga instances as sagas. This applies to **both**
   CritterMart sagas — the Marten `Replenishment` would not render as an instance here either.

2. **The saga's timeout is scheduled in-memory, so it is absent from the durable Scheduled view.** The
   `EmailChangeTimeout` never reaches `identity.wolverine_incoming_envelopes` — the durable envelope table
   CritterWatch's **Reliability → Scheduled** view reads — so that view is correctly empty for it.

## Evidence (direct Postgres inspection, saga open)

Firing a bare `RequestEmailChange` (no confirm) opens the saga for its ~25 s window. Reading both tables in
the same sub-second shot, against the live Aspire Postgres (`crittermart` db, `identity` schema):

| Table | Result | Meaning |
|---|---|---|
| `identity.email_changes` | **1 row** — `id=<customerId>`, `pending_email=…`, `version=0` | The EF-Core saga **is** durably persisted (its own `DbSet`-mapped table). |
| `identity.wolverine_incoming_envelopes` | **0 rows** (any status) | The `EmailChangeTimeout` is **not** in the durable message store — it is held on a buffered (in-memory) local queue. |

The saga row proves persistence works; the empty envelope table proves the timeout is non-durable. The
console's Scheduled view is therefore accurate, not broken — there is genuinely nothing in the store to show.

## Root cause of the in-memory timeout

Neither `src/CritterMart.Identity/Program.cs` nor `src/CritterMart.Inventory/Program.cs` calls
`opts.Policies.UseDurableLocalQueues()` (nor marks the saga's local queue durable). Both services **do**
persist messages durably (Identity via `PersistMessagesWithPostgresql` + `AddDbContextWithWolverineIntegration`;
Inventory via Marten `IntegrateWithWolverine`), but Wolverine's **local queues default to buffered/in-memory**,
and a scheduled message on a non-durable local queue is held in memory rather than written to the incoming
envelope table. So the behavior is **uniform across both sagas** and is Wolverine's default, not an
EF-vs-Marten asymmetry.

> **Not yet empirically confirmed for the Marten sibling.** The uniform-behavior claim is inferred from the
> matching absence of `UseDurableLocalQueues()` in both services; it was **not** verified by driving a
> `Replenishment` saga and querying `inventory.wolverine_incoming_envelopes`. A future session wanting the
> rigorous comparison should do exactly that. (The demo-runbook § 5c likewise *asserts* the `ReplenishTimeout`
> shows in the Scheduled backlog; that assertion is now suspect and shares this open verification.)

## Consequences / what this touches

- **Stale doc references — "Saga Explorer".** `docs/demo-runbook.md` § 5c and `docs/workshops/002` § 8
  (item 10) both point at a "CritterWatch — Saga Explorer" saga-lifecycle visual. That view does not exist as
  such in beta.1 (it is the in-flight **Workflow** stub). These references predate the CritterWatch
  0.9.x→beta.1 upgrade (PR #117) and should be corrected — the honest beta.1 statement is *"saga instances
  are not yet surfaced; saga message flow is observable via Messaging Explorer."*
- **Robustness observation (not a shipped bug).** An in-memory saga timeout is lost if the Identity node
  restarts mid-window, which would leave the saga open with no timeout to close it. For a single-node teaching
  demo this is harmless and is Wolverine's default. A production email-change-confirmation flow would typically
  want `UseDurableLocalQueues()` — which would *also* make the timeout appear in CritterWatch's Scheduled view,
  turning the observability gap into a teaching payoff ("here is the confirm-or-expire window as durable,
  pending work"). Candidate for a future slice or a DEBT row; deliberately **not** actioned here.

## Promotion path

| Finding | Graduates to |
|---|---|
| "Saga Explorer" is stale for beta.1 | Corrective edits to `demo-runbook` § 5c and `workshops/002` § 8 (a `tidy:` docs PR) |
| Saga timeouts are in-memory / non-durable | An ADR or slice **iff** durable saga timeouts are wanted (survives restart + becomes CritterWatch-visible); else a `docs/skills/DEBT.md` row |
| Marten sibling not yet compared | A follow-up verification session (drive `Replenishment`, query `inventory.wolverine_incoming_envelopes`) |

## Closes

Retro `implementations/036`'s outstanding item — *"CritterWatch's saga-lifecycle surface … is unverified in
this session"* — is now **verified and explained**: the saga is fully functional and durably persisted; it is
CritterWatch beta.1 (instance view still in flight) and the in-memory timeout schedule that keep it off the
console, not any defect in the saga.
