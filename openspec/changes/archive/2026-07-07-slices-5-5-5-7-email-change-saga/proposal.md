## Why

Identity's `Customer` row supports no lifecycle past registration — there is no way for a customer to change
their email. CritterMart's coordination story currently shows two convention sagas side by side: Inventory's
`Replenishment` (Marten-backed, slices 2.5–2.7) and now Identity needs its own, to prove the **saga store
itself is swappable** (EF Core vs. Marten) — the whole point of choosing Identity as Saga #2's home over a
name/address edit, which would be a single mutating command with nothing to coordinate. Email-change-with-
confirmation is the smallest honest multi-step workflow Identity has: a real confirm-or-expire window.
Modeled in Workshop 002 v1.1 (slices 5.5–5.7); gated on [ADR 009's second amendment](../../../docs/decisions/009-polecat-deferred-for-round-one.md)
("deliberately boring CRUD" holds even as Identity grows a stateful consumer) and bound by
[ADR 022](../../../docs/decisions/022-convention-sagas-additive-to-pmvh.md) (a convention saga must do
relational things the Wolverine way, never re-implement event sourcing on SQL). Feasibility de-risked in
`docs/research/wolverine-saga-feasibility.md` § Candidate #2.

## What Changes

An `EmailChange` saga, EF-Core-backed (`DbSet<EmailChange>` on `IdentityDbContext`, keyed by `CustomerId`),
mirrors `Replenishment`'s shape on a different store:

- **Identity (5.5 — request).** `RequestEmailChange { customerId, newEmail }` opens an `EmailChange` saga
  when none is open for the customer — recording `PendingEmail` and scheduling an `EmailChangeTimeout`. Two
  guards run first: the customer must exist (`CustomerNotFound` otherwise), and `newEmail` must not already
  be registered to a *different* customer (`EmailAlreadyRegistered` otherwise — mirrors `RegisterCustomer`'s
  duplicate-email guard). When a saga is already open for the customer, `RequestEmailChange` updates
  `PendingEmail` to the newest value **without** rescheduling `EmailChangeTimeout` — the original deadline
  governs the confirm window (see Design decision below; this diverges from a naive "reset the deadline on
  every request" reading and was corrected during workshop review before this proposal was drafted).
- **Identity (5.6 — confirm).** `ConfirmEmailChange { customerId }` within the window applies the change:
  `Customer.Email` is set to the normalized `PendingEmail` and the saga completes (`MarkCompleted()`). If the
  pending email has since been claimed by another registration (`ux_customers_email` would reject the
  update), the command is rejected (`EmailChangeConflict`) and the saga **stays open** — the customer's only
  forward paths are the timeout dropping it, or a fresh `RequestEmailChange` for a different email. A
  `ConfirmEmailChange` for a customer with no open saga (window already expired, or already confirmed) is a
  silent no-op.
- **Identity (5.7 — timeout).** When `EmailChangeTimeout` fires and the saga is still open, the pending
  change is dropped (no row change) and the saga completes. A timeout delivered after the saga already
  resolved is a silent no-op (Wolverine has no scheduled-message cancellation, the same property
  `Replenishment`'s `ReplenishTimeout` and the Bruun slices rely on).
- **Persistence / wiring.** `EmailChange : Saga` is a `DbSet`-mapped EF-Core entity on the existing
  `IdentityDbContext` (`AddDbContextWithWolverineIntegration<IdentityDbContext>` already activates EF-Core
  saga storage — no new infrastructure call). String-keyed (`CustomerId`), same lowercase-column discipline
  as `Customer`. No new event, no stream, no projection — Identity's registry framing is unchanged.

### Design decisions carried from the workshop (Workshop 002 v1.1 § 8, open to redline)

- **Re-request does not reschedule the deadline.** Mirrors `Replenishment`'s re-open branch (no second
  `ReplenishTimeout`) for the identical reason: Wolverine offers no scheduled-message cancellation, so a
  rescheduled timeout would leave the *original* one still armed and firing early against the "new" window.
- **A confirm-time conflict leaves the saga open**, rather than completing it — the customer keeps a path
  forward (timeout, or a fresh request) instead of a dead-ended saga.
- **No new Published-Language event** for a successful email change (`CustomerRegistered` has no analogue
  here yet) — no consumer needs one; follows the same "graduates to `CritterMart.Contracts` on first
  consumer" convention already set for `CustomerRegistered`.

## Capabilities

### New Capabilities

(None — Identity's single capability `customer-registry` gains new behavior; the saga is not a new
capability, per one-capability-per-aggregate, CLAUDE.md § 4a.)

### Modified Capabilities

- `customer-registry`: Identity gains an email-change process. A `RequestEmailChange` now opens an
  `EmailChange` saga (CritterMart's second convention `Wolverine.Saga`, EF-Core-backed) that either applies
  or drops the pending change within a confirm-or-expire window — saga state lives in EF-Core saga storage,
  never on a stream, registration/resolution (5.1/5.2) unchanged. (Three ADDED requirements: open on
  request, confirm-or-conflict within the window, drop on timeout.)

## Impact

- **Identity.** New `Customers/EmailChange.cs` (`EmailChange : Saga`, `Id = CustomerId`, `PendingEmail`).
  New messages: `Customers/RequestEmailChange.cs`, `Customers/ConfirmEmailChange.cs`,
  `Customers/EmailChangeTimeout.cs` (a `TimeoutMessage`), `Customers/EmailChangeDeadline.cs`
  (config-singleton, mirrors `ReplenishDeadline`). `IdentityDbContext.OnModelCreating` gains the
  `EmailChange` entity mapping (lowercase columns, same discipline as `Customer`).
- **Persistence.** `EmailChange` rides the existing `UseEntityFrameworkCoreWolverineManagedMigrations` path
  (no extra DDL — keyed by PK, no secondary index needed). No new `CustomerRegistered`-style event, no
  projection.
- **Contracts / topology.** No new `CritterMart.Contracts` type and no cross-BC message — every saga message
  is Identity-local.
- **Observability.** CritterWatch gains its second saga, now proven on a second backing store (EF Core vs.
  Marten) — the persistence-agnostic thesis extended from ordinary handlers to sagas specifically.
- **Tests.** Saga unit tests over `EmailChange` (open, re-request idempotency without deadline reset,
  confirm-and-complete, confirm-conflict-stays-open, timeout-and-complete); Identity integration for the
  request→open and confirm→apply/timeout→drop wiring (Alba/EF Core); not-found no-op tests for
  `ConfirmEmailChange`/`EmailChangeTimeout` against an absent saga.
- **Downstream artifacts (same PR — consolidated per Erik's slice-PR preference).** `design.md` + `tasks.md`
  in this change; a narrative threading the email-change journey; Workshop 002 is already at v1.1; the
  implementation code, tests, and retro land in this same PR.
- **Out of scope.** Name/address edits (no confirm-or-expire window, so no saga — a plain mutating command
  suffices per the research's "don't invent a workflow" reasoning); a future `CustomerEmailChanged`
  Published-Language notification (deferred, no consumer needs it yet); Polecat-backed authentication or any
  proof the customer actually controls the new email inbox (ADR 009's no-auth stance is unchanged by this
  saga).
