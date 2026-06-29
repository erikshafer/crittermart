---
version: 0.1
status: Active
date: 2026-06-28
references:
  - docs/decisions/007-process-manager-via-handlers-for-order.md
  - docs/decisions/009-identity-ef-core-data-store.md
  - docs/rules/structural-constraints.md
  - docs/workshops/001-crittermart-event-model.md
  - src/CritterMart.Inventory/Program.cs
  - src/CritterMart.Inventory/Features/ReserveStock.cs
  - src/CritterMart.Inventory/Features/ReceiveStock.cs
  - src/CritterMart.Inventory/Stock/StockReceived.cs
  - src/CritterMart.Contracts/StockReservationFailed.cs
  - src/CritterMart.Identity/Program.cs
  - src/CritterMart.Identity/Customers/IdentityDbContext.cs
  - src/CritterMart.Identity/Features/RegisterCustomer.cs
  - docs/research/identity-ef-core-first-class-expansion.md
---

# Research: introducing CritterMart's first convention Wolverine Saga

> **What this is.** The de-risking spike behind a collaborative decision (Erik, 2026-06-28) to add
> CritterMart's **first convention `Wolverine.Saga`** — a feature the repo deliberately does not use
> today. It records (a) *why* there is no saga today, (b) the two **candidate homes** chosen, and
> (c) the two **technical unknowns** resolved — from documentation alone, so **no prototype code was
> written or merged.** A narrow, in-pipeline spike: it de-risks *plumbing*, it does **not** model the
> *domain*.
>
> **What it is not.** Not a decision, not a workshop, not a build order. Per the research README a
> research doc has no authority — it *informs* binding choices without *making* them. The location
> choices graduate to **workshop-slice amendments** (Workshop 001 for Inventory, Workshop 002 for
> Identity); the Identity case additionally touches **ADR 009** and so warrants an ADR note before any
> build. The domain questions this note flags as "deferred to modeling" are deliberately left open here.

## Bottom line

1. **CritterMart uses no `Wolverine.Saga` today, on purpose.** Verified in code, not docs: `grep` for the
   `Saga` base class returns zero `.cs` hits. The Order lifecycle is **Process Manager via Handlers
   (PMvH)** per [ADR 007](../decisions/007-process-manager-via-handlers-for-order.md) — the event-sourced
   `Order` aggregate *is* the process state; stateless handlers `FetchForWriting<Order>` and guard on
   `Status`. "No `Wolverine.Saga` base class" is an encoded non-negotiable
   ([`structural-constraints.md:76`](../rules/structural-constraints.md)).

2. **The plan is to *add* a saga, not *convert* one.** Converting Order or Cart would reverse ADR 007 and
   gut the PMvH teaching beat. Instead, introduce sagas in *new, additive* flows so the talk shows **three**
   coordination patterns side-by-side: **cascading messages** (per-hop), **PMvH** (Order/Cart as their own
   process manager), and a **convention `Saga`**.

3. **Two homes chosen, two backing stores — that contrast is the point.**
   - **Saga #1 — Inventory "backorder / replenishment"** (Marten-backed): started when a reservation can't
     be filled; waits for a restock; self-cancels on timeout. Stays inside the Inventory BC.
   - **Saga #2 — Identity "email-change confirmation"** (EF-Core-backed): request → confirm-or-expire
     window → apply or drop. Doubles as proof the **saga store is swappable** (EF Core vs Marten), extending
     ADR 009's "Wolverine is persistence-agnostic" thesis to sagas. Contingent on an ADR-009 revisit.

4. **Both plumbing unknowns resolved from docs — no spike code needed.** Inventory **must** use Marten→Wolverine
   **Event Forwarding**, *not* Event Subscriptions, because round-one bans the async daemon and subscriptions
   require it. Identity's EF-Core saga storage is **already wired** by the existing
   `AddDbContextWithWolverineIntegration` call; the saga is just a `DbSet`-mapped entity with a string key,
   and Postgres sidesteps the SQL-Server `nvarchar` footgun.

## Why there's no saga today

The Place-Order flow coordinates Inventory (stock), the stubbed payment provider, and Orders itself.
[ADR 007](../decisions/007-process-manager-via-handlers-for-order.md) considered `Wolverine.Saga`, a
separate process-manager stream, and PMvH — and chose **PMvH**: the `Order` aggregate serves as its own
process manager, with state-flag events (`StockReserved`, `PaymentAuthorized`), terminal events
(`OrderConfirmed` / `OrderCancelled`), and an idempotent scheduled `OrderPaymentTimeout`. The ADR names
the tradeoff explicitly — *"forgoes the `Wolverine.Saga` base class; intentional — the PMvH pattern is the
point."* The Cart abandonment flow uses the same idiom. So the absence is a deliberate, documented stance,
not a gap. This note does not disturb it — it adds sagas *elsewhere*.

A note on the upstream tooling: the installed JasperFx `ai-skills` set has **no saga skill** (closest is
`marten-aggregate-handler-workflow`, which is the PMvH-style aggregate pattern, and
`marten-event-subscriptions`). The convention-saga shape below was sourced from ctx7
(`/jasperfx/wolverine`, `guide/durability/sagas.md` + `guide/durability/efcore/sagas.md`).

## Candidate #1 — Inventory backorder / replenishment saga (Marten-backed)

**Shape (illustrative — the workshop discovers the real events):**

```csharp
// Inventory/Stock/Replenishment.cs  (Marten-stored saga, Id = Sku)
public record ReplenishTimeout(string Sku) : TimeoutMessage(48.Hours());

public class Replenishment : Saga
{
    public string? Id { get; set; }       // = Sku
    public int Outstanding { get; set; }

    public static (Replenishment, ReplenishTimeout, RequestRestock) Start(BackorderOpened e)
        => (new Replenishment { Id = e.Sku, Outstanding = e.Shortfall },
            new ReplenishTimeout(e.Sku), new RequestRestock(e.Sku, e.Shortfall));

    public void Handle(StockReceived e)    { if (e.Quantity >= Outstanding) MarkCompleted(); }
    public void Handle(ReplenishTimeout t) { /* escalate to operator */ MarkCompleted(); }
}
```

**Corrections to the first sketch, against the real code:**

- `StockReservationFailed(string OrderId, string Reason)`
  ([`StockReservationFailed.cs`](../../src/CritterMart.Contracts/StockReservationFailed.cs)) is
  **order-scoped and SKU-blind** — its only reason string is `"insufficient"`
  ([`ReserveStock.cs:42`](../../src/CritterMart.Inventory/Features/ReserveStock.cs)). It **cannot** key a
  SKU-scoped saga. The handler *does* know which line was short (`anyShort` at `ReserveStock.cs:38`) but
  never surfaces the per-SKU fact. So the saga's **start trigger is net-new** — an Inventory-local
  `BackorderOpened(Sku, Shortfall)` (name TBD by the workshop) raised on the short path.
- `StockReceived(string Sku, int Quantity)`
  ([`StockReceived.cs`](../../src/CritterMart.Inventory/Stock/StockReceived.cs)) is the natural restock
  correlator, but it is a Marten **stream event**, not a routed message. A saga `Handle(StockReceived)`
  needs it delivered *as a message*.
- `RequestRestock` does **not** exist anywhere — net-new, and implies a stubbed supplier/operator that
  fulfils it (a demo affordance).

**The decisive technical finding (unknown #1 — how the restock reaches the saga):** there are two
Marten→Wolverine mechanisms, and a **round-one constraint picks one for us**:

| | Event Forwarding | Event Subscriptions |
|---|---|---|
| API | `IntegrateWithWolverine(x => x.UseFastEventForwarding = true)` | `.PublishEventsToWolverine()` / `.ProcessEventsWithWolverineHandlersInStrictOrder()` |
| Async daemon | **Not required** (fires at `SaveChangesAsync`) | **Required** |
| Ordering | No guarantee | Strict sequential |

CLAUDE.md's *"Do Not — round one: No async projection daemon"* rules out Subscriptions (they would force a
**new** daemon in Inventory — the existing async-projection "teaser" lives in Orders, not here). Inventory's
[`Program.cs`](../../src/CritterMart.Inventory/Program.cs) runs **inline snapshots only** + `IntegrateWithWolverine()`
+ `AutoApplyTransactions()` — exactly Event Forwarding's prerequisites and nothing more. **So: Event
Forwarding (`UseFastEventForwarding = true`).** (Wolverine 6 note: the old `.EventForwardingToWolverine()`
extension was removed; `UseFastEventForwarding` is the current API.) Saga correlation comes from
`[SagaIdentity]` on the SKU-bearing property, since it isn't named `Id`/`SagaId`.

**Deferred to the workshop (domain, not plumbing):** forward the *raw* `StockReceived` and let `[SagaIdentity]`
filter it (chatty — every receipt queries saga storage) **vs.** emit a dedicated `RestockArrived(Sku, Qty)`
message (no annotation on a domain event); and how one SKU-keyed saga tracks aggregate shortfall when several
orders queue on the same SKU.

## Candidate #2 — Identity email-change confirmation saga (EF-Core-backed)

**Shape (illustrative):**

```csharp
// Identity/Customers/EmailChange.cs  (EF-Core saga; DbSet<EmailChange> on IdentityDbContext, Id = CustomerId)
public record EmailChangeTimeout(string Id) : TimeoutMessage(24.Hours());

public class EmailChange : Saga
{
    public string? Id { get; set; }              // = CustomerId
    public string PendingEmail { get; set; } = "";

    public static (EmailChange, EmailChangeTimeout) Start(RequestEmailChange c)
        => (new EmailChange { Id = c.CustomerId, PendingEmail = c.NewEmail },
            new EmailChangeTimeout(c.CustomerId));

    public async Task Handle(ConfirmEmailChange c, IdentityDbContext db)   // confirmed in time
    {
        var customer = await db.Customers.FindAsync(Id);
        customer!.Email = PendingEmail;          // must still respect ux_customers_email
        MarkCompleted();
    }

    public void Handle(EmailChangeTimeout t) => MarkCompleted();           // window expired → drop
}
```

**Why *this* flow and not name/address edits:** a name or physical-address update is a single mutating
command with an immediate result — no pending state, no awaited second event, no timeout — so a saga there
is ceremony with nothing to coordinate. A saga earns its keep only when state must survive *across messages
and time*. **Email change with confirmation** qualifies because of the confirm-or-expire window; a
**support-ticket / complaint SLA** flow would also qualify (escalation timeouts) but invents a whole Support
sub-domain Identity doesn't have. Email-change is the smallest honest saga in Identity.

**Technical findings (unknown #2 — EF-Core saga storage):**

- **Already wired.** `AddDbContextWithWolverineIntegration<IdentityDbContext>`
  ([`Program.cs:44`](../../src/CritterMart.Identity/Program.cs)) activates EF-Core saga support per the
  Wolverine EF-Core docs — no new infrastructure call.
- **The saga is just a mapped entity.** Add `DbSet<EmailChange>` to
  [`IdentityDbContext`](../../src/CritterMart.Identity/Customers/IdentityDbContext.cs) and map it in
  `OnModelCreating` with the **same lowercase-column discipline** the `Customer` entity uses (to satisfy the
  Weasel-unquoted vs EF-quoted casing reconciliation already documented in that file).
- **String key works**, and **Postgres dodges a SQL-Server-only footgun.** Wolverine's saga sample uses
  `string? Id` natively; the `useNVarCharForStringId` flag is a SQL-Server `nvarchar` concern that does not
  apply to our Postgres.
- **Rides existing migrations, no extra DDL.** The saga table is created by the same
  `UseEntityFrameworkCoreWolverineManagedMigrations` path that creates `customers` (tables/columns/PK). The
  known Identity gotcha — Weasel skips **secondary indexes**, which is why the email unique index is applied
  out-of-band in `Program.cs` (`EnsureEmailUniqueIndex`) — does **not** bite here: the saga is keyed by its
  PK and needs no secondary index.
- **Deferred to the workshop (domain):** a confirmed change must still honour `ux_customers_email` if another
  registration grabbed that email during the open window — graceful handling is a modeled rule, not a blocker.

**Relationship to prior research.** This directly answers the open question parked in
[`identity-ef-core-first-class-expansion.md`](identity-ef-core-first-class-expansion.md) — *"an EF-Core-backed
saga (if a genuine multi-step Identity workflow exists) … only if a real workflow motivates it; don't invent
one."* Email-change-with-confirmation **is** that genuine workflow. This note does not supersede that one;
it picks up its saga thread and gives it a concrete first flow. Its caution still governs: the saga must
stay "EF Core doing relational things the Wolverine way," not drift into re-implementing event sourcing on SQL.

## What graduates where (promotion path)

| Finding | Graduates to |
|---|---|
| Add the repo's first convention saga(s); additive, not a PMvH conversion | An **ADR** (the saga-showcase decision; cross-references ADR 007) |
| Inventory backorder slice — `BackorderOpened` / `RequestRestock` / forwarding / `[SagaIdentity]` | **Workshop 001** amendment → OpenSpec → narrative → prompt → implement |
| Identity grows its first **stateful consumer** (a saga) | An **ADR-009 revisit** (does "deliberately boring CRUD" still hold?), then **Workshop 002** amendment |
| "Event Forwarding, not Subscriptions, because no daemon" | Encoded in the Inventory slice's design + a structural-constraints note |

Recommended sequencing: **Inventory first** (the in-character, Marten-backed saga; no ADR-009 entanglement),
then **Identity** as a deliberate second step once ADR 009 is revisited. A workshop amendment is itself the
**design-return** the cadence favours after the recent docs/tidy run.

## Open questions deferred to modeling

1. **Inventory restock delivery** — forward raw `StockReceived` + `[SagaIdentity]`, or a dedicated
   `RestockArrived` message? (Plumbing proven for both; the choice is a domain/event-naming decision.)
2. **Backorder fan-in** — one SKU-keyed saga vs. shortfall aggregation across multiple waiting orders.
3. **Who fulfils `RequestRestock`?** A stubbed supplier, an operator HTTP affordance, or a timed auto-restock
   demo lever.
4. **Identity / ADR 009** — does adding a saga revise "deliberately boring EF Core CRUD," or is it framed as
   the intended *next* proof of the persistence-agnostic thesis?
5. **CritterWatch payoff** — confirm the saga store, the auto-scheduled `TimeoutMessage`, and saga-lifecycle
   correlation surface on the console as intended (the original motivation for wanting a real saga at all).

## Sources

- ctx7 `/jasperfx/wolverine` — `guide/durability/sagas.md`, `guide/durability/efcore/sagas.md`,
  `guide/durability/efcore/index.md` (saga base class, `TimeoutMessage`, `[SagaIdentity]`,
  `MarkCompleted()`, EF-Core saga storage, string-id handling).
- Skill `marten-event-subscriptions` — Event Forwarding vs Subscriptions, daemon requirements, the
  Wolverine-6 `UseFastEventForwarding` API change.
- Repo code, read first-hand (referenced files in frontmatter).
</content>
</invoke>
