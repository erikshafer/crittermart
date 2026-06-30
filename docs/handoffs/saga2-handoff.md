# CritterMart — Handoff: Saga #2 (Identity email-change, EF-Core-backed) — 2026-06-30

> **Durable handoff** (version-controlled at `docs/handoffs/`). The next session's job is to
> **start Saga #2, design-first.** Committed by Erik on 2026-06-30.

## Mission for the next session

Begin **Saga #2** — CritterMart's **second** `Wolverine.Saga`, this one **EF-Core-backed** in the **Identity**
bounded context, to prove the **saga store is swappable** (the contrast with the Marten-backed Saga #1 is the
whole point). Run it **design-first**:

1. **ADR-009 revisit** — the *next session's deliverable* (a decision-only session; **no code**).
2. then: **Workshop 002 amendment** → OpenSpec proposal → narrative → prompt → implement → retro.

Do **not** jump to code. The ADR-009 revisit is the gate; produce that first.

## The decision the ADR-009 revisit must settle

`docs/decisions/009-polecat-deferred-for-round-one.md` established Identity as the deliberately **boring,
non-event-sourced** EF-Core data store (no Polecat) — its teaching value is being the persistence-agnostic
*foil*. Saga #2 gives Identity its **first stateful consumer**. The revisit question: *does "deliberately
boring CRUD" still hold, or is the saga the intended next proof of the persistence-agnostic thesis?*
**Guard (from the research):** the saga must stay "EF Core doing relational things the Wolverine way," NOT
drift into re-implementing event sourcing on SQL. The revisit can also absorb/formalize the **saga-showcase
ADR** that Saga #1 never got a dedicated one for (decision lives in the feasibility note + Workshop 001 +
retro 035): "CritterMart uses convention sagas, additively, alongside PMvH (ADR 007)."

## The saga shape (already de-risked from docs — no spike code needed)

- **Flow:** `RequestEmailChange` → opens an `EmailChange` saga (holds `PendingEmail`, schedules a 24h
  `EmailChangeTimeout`) → `ConfirmEmailChange` within the window → apply (set `Customer.Email`, still
  respecting the `ux_customers_email` unique index) + `MarkCompleted()`; on timeout → drop + `MarkCompleted()`.
- **Why this flow and not name/address edits:** it's the smallest *honest* Identity saga — a real
  confirm-or-expire window. A name/address change is a single mutating command with no awaited second event,
  so a saga there is ceremony. **Don't invent a workflow.**
- **Plumbing already wired (confirmed from docs):** EF-Core saga storage is activated by the **existing**
  `AddDbContextWithWolverineIntegration<IdentityDbContext>` call in `src/CritterMart.Identity/Program.cs` — no
  new infra. The saga is just a `DbSet<EmailChange>` on `IdentityDbContext`, **string key** (= CustomerId),
  mapped with the same lowercase-column discipline the `Customer` entity uses. Postgres dodges the
  SQL-Server `useNVarCharForStringId` footgun. It rides the existing
  `UseEntityFrameworkCoreWolverineManagedMigrations` (no extra DDL); keyed by PK so no secondary index.

## Saga API gotchas (hard-won on Saga #1 — apply directly; upstream ai-skills have NO saga skill)

- A message that BOTH starts and continues a saga uses **ONE `StartOrHandle`** method — NOT separate static
  `Start` + instance `Handle` for the same type (that resolves to the continuation path → blank-id saga →
  Marten/EF "id null or empty" error). For EmailChange, `RequestEmailChange` is the start.
- **`NotFound(msg)` static methods are MANDATORY** for the spec's "silent no-op" — Wolverine THROWS otherwise
  on a non-start message for a missing/completed saga (e.g. a `ConfirmEmailChange` arriving after the window
  expired, or an `EmailChangeTimeout` firing after confirmation already completed the saga). Empty bodies suffice.
- `[SagaIdentity]` lives in **`Wolverine.Persistence.Sagas`** (not `Wolverine.Attributes`) — correlate on
  `CustomerId` since it isn't named `Id`/`SagaId`.
- `TimeoutMessage` is a record: `record EmailChangeTimeout(string CustomerId) : TimeoutMessage(duration)`
  compiles. Prefer a **config-driven duration** (mirror Saga #1's `ReplenishDeadline` config-singleton) so a
  demo knob can shorten it.
- ctx7 was **quota-blocked** on 2026-06-30 — fallback is a direct WebFetch of
  `wolverinefx.net/guide/durability/efcore/sagas.html` (+ `guide/durability/sagas.html`).

## Canonical context — read these, don't re-derive

- **`<claude-memory>/next-pickup.md`** — authoritative pickup state (auto-loaded via `MEMORY.md`).
- `docs/research/wolverine-saga-feasibility.md` — **Candidate #2** section is the saga blueprint (shape, the
  two plumbing unknowns resolved, the promotion-path table, "why this flow").
- `docs/research/identity-ef-core-first-class-expansion.md` — the parent "first-class EF-Core Identity" idea;
  the saga is its concrete first flow (open question #2 = "does the contrast survive?").
- `docs/decisions/009-polecat-deferred-for-round-one.md` — the ADR to revisit.
- `docs/workshops/002-identity-event-model.md` — **exists**; amend it with the email-change slices.
- Identity code: `src/CritterMart.Identity/Program.cs`, `Customers/IdentityDbContext.cs`,
  `Features/RegisterCustomer.cs`.
- **Saga #1 reference implementation (Marten-backed):** `src/CritterMart.Inventory/Stock/Replenishment.cs` +
  its siblings; retro `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md`; the
  archived change at `openspec/changes/archive/2026-06-30-slices-2-5-2-7-replenishment-saga/`.

## Promotion path (from the feasibility note)

| Step | Artifact |
|---|---|
| Saga-showcase decision (additive, NOT a PMvH conversion) + Identity grows a stateful consumer | **ADR-009 revisit** |
| The email-change slices (events / commands / saga / timeout) | **Workshop 002 amendment** → OpenSpec → narrative → prompt → implement → retro |

Sequencing was settled with Erik: Inventory first (done — Saga #1), then Identity once ADR-009 is revisited —
exactly where we are now. A workshop amendment also satisfies the design-return cadence.

## Current repo state (2026-06-30)

- `main` @ **`9ac3db0`**, clean tree. Shipped this session (all merged + post-merged + live-verified):
  - **PR #115** — Saga #1 demo affordances (`Inventory__ReplenishTimeout=25s` knob, `demo-traffic.ps1
    -Backorder`/`-Cover`, runbook §5c).
  - **PR #116** — Aspire aligned to **13.4.6** (SDK + Hosting).
  - **PR #117** — CritterWatch **1.0.0-beta.1** + WolverineFx **6.16.0** + a necessitated Marten-9.12
    stateless-projection fix (retro `docs/retrospectives/chore/004-critterwatch-next-release-upgrade.md`).
- **Tooling:** .NET 10 / C# 14, Wolverine **6.16.0**, Marten **9.12.0**, CritterWatch **1.0.0-beta.1**,
  Aspire **13.4.6**, EF Core 10 (Npgsql 10.0.2) in Identity.

## Standing warnings (carry forward)

- **DO NOT bump Wolverine PAST 6.16.0** until a newer CritterWatch targets it — same startup
  `TypeLoadException` crash class. CW beta.1 ↔ WolverineFx 6.16.0 is the current coupling
  (`Directory.Packages.props` comments are the canonical record). The old "no 6.13.x" hold is resolved.
- **Marten 9.12 gotcha:** instance-registered inline projections lose constructor-injected state — keep
  projections **stateless**, apply per-projection config on the **read side**. (Relevant if Saga #2 adds a view.)
- MessagePack CVE (`GHSA-hv8m-jj95-wg3x`) still **suppressed** (beta.1 → 2.5.302 < the 3.0.214 fix);
  re-check on the next CW release.
- CritterWatch **trial expires 2026-07-10**.
- **Post-talk:** delete the FOUR AppHost demo knobs (`Payment__DeclineOverAmount`, `Payment__AuthDelay`,
  `Orders__PaymentTimeout`, `Inventory__ReplenishTimeout`).
- Dependabot #104/#105 will self-regenerate (their branches were deleted on close); #106/#107/#109 (otel,
  test-stack, Swashbuckle) remain as separate concerns.

## Working preferences (from memory)

Present options + a recommendation at genuine forks and let Erik decide ([[feedback-collaborate-on-decisions]]),
ideally as 2–4 AskUserQuestion options with rich previews ([[feedback-options-with-previews]]); prefer
tool-backed over freeform ([[feedback-prefer-tool-backed-over-freeform]]); live-verify after substantial
changes and drive the demo flows yourself ([[feedback-live-verify-after-changes]], [[feedback-drive-demo-flows]]);
when asked to "persist/make durable," ask where it should live ([[feedback-ask-where-to-persist]]).

## Suggested skills for the Saga #2 sessions

- **ADR-009 revisit:** no special skill — follow the `docs/decisions/` ADR format; cross-reference ADR 007
  (PMvH) and the feasibility note.
- **Workshop amendment:** the in-repo `event-modeling` skill.
- **Implementation:** `critterstack-arch-new-project-wolverine-efcore`, `wolverine-handlers-efcore`,
  `wolverine-handlers-declarative-persistence`; `find-docs`/ctx7 (or the WebFetch fallback above) for
  Wolverine EF-Core saga specifics.
- **OpenSpec:** `opsx:propose` / `openspec-propose`.
