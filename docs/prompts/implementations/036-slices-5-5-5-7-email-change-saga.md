# Prompt: Implementations 036 — Slices 5.5 + 5.6 + 5.7 Identity Email-Change Saga (CritterMart's second convention `Wolverine.Saga`)

**Kind**: per-slice implementation (Identity email-change saga, consolidated per [[feedback-consolidate-slice-prs]] — including the workshop amendment and OpenSpec proposal, diverging from Saga #1's own two-PR precedent)
**Files touched**: `docs/workshops/002-identity-event-model.md` (v1.0 → v1.1, this session); `docs/rules/structural-constraints.md` (v1.6 → v1.7, this session — an ADR-022/ADR-009-amendment pairing gap closed during design review); `openspec/changes/slices-5-5-5-7-email-change-saga/{proposal.md,specs/customer-registry/spec.md,design.md,tasks.md}` (all new, this session, `openspec validate --strict` green); `docs/narratives/009-customer-changes-email.md` (new) + `docs/narratives/README.md` (count 8→9); `docs/prompts/implementations/036-slices-5-5-5-7-email-change-saga.md` (new, this file); `src/CritterMart.Identity/Customers/{EmailChange.cs,RequestEmailChange.cs,ConfirmEmailChange.cs,EmailChangeTimeout.cs,EmailChangeDeadline.cs}` (new); `src/CritterMart.Identity/Customers/IdentityDbContext.cs` (modified — `DbSet<EmailChange>` mapping); `src/CritterMart.Identity/Program.cs` (modified — bind `EmailChangeDeadline`); `tests/CritterMart.Identity.Tests/EmailChangeSagaTests.cs` (new) + integration coverage; `docs/retrospectives/implementations/036-slices-5-5-5-7-email-change-saga.md` (forthcoming)
**Mode**: solo implementation, fully consolidated — this single session runs the whole triangle (workshop amendment → OpenSpec proposal/specs/design/tasks → narrative → this prompt → code → tests → retro), per Erik's slice-PR preference, confirmed via AskUserQuestion at session start over mirroring Saga #1's two-PR split
**Commit subject**: `feat: add Identity email-change saga (EmailChange) — slices 5.5–5.7`

## Framing

CritterMart's first convention `Wolverine.Saga`, `Replenishment` (Inventory, Marten-backed, slices 2.5–2.7), proved the pattern works. This session lands the **second** — `EmailChange` in Identity, EF-Core-backed — to prove the saga *store* itself is swappable, extending [ADR 009](../../decisions/009-polecat-deferred-for-round-one.md)'s persistence-agnostic thesis from ordinary handlers to sagas, under [ADR 022](../../decisions/022-convention-sagas-additive-to-pmvh.md)'s binding guard (relational things the Wolverine way, never event-sourcing on SQL).

**New ground, not a copy-paste of Saga #1.** Every `Replenishment` trigger is system-internal (a stock event, a scheduled timeout); every `EmailChange` trigger except the timeout is **customer-facing HTTP** — a genuinely new wiring question with no in-repo precedent. `design.md` resolves it against current Wolverine docs (ctx7-verified, not assumed): a message handler method can carry `[WolverinePost]` directly (the same method serves both HTTP and bus dispatch), and the existing `RegisterCustomer.ValidateAsync` railway idiom composes with any handler, saga methods included. One open question is flagged, not resolved by fiat: whether a `NotFound`-routed saga message maps to HTTP `404` automatically — **verify this empirically early** (write the Alba test for "confirm after timeout" before assuming the status code).

**A caught-and-fixed defect, not a clean first draft.** An independent design review (Fable 5, fresh context) caught that the workshop's first draft let a re-request reschedule `EmailChangeTimeout` — impossible, since Wolverine has no scheduled-message cancellation, so the original timeout would still fire early against the "reset" window. Fixed to mirror `Replenishment`'s own re-open rule: a re-request updates `PendingEmail` only, the original deadline governs. Implement it this way — do **not** reintroduce a rescheduled timeout on re-request.

## Goal

- `EmailChange : Saga` (`Id = CustomerId`, `PendingEmail`) exists as a `DbSet`-mapped entity on `IdentityDbContext`; EF-Core saga storage active on the existing `AddDbContextWithWolverineIntegration<IdentityDbContext>` call (no new infra).
- `RequestEmailChange` at `POST /customers/{customerId}/email-change` opens the saga (guarded: `404 CustomerNotFound`, `409 EmailAlreadyRegistered`) or updates `PendingEmail` on an already-open one **without** rescheduling the timeout.
- `ConfirmEmailChange` at `POST /customers/{customerId}/confirm-email-change` applies the change and completes the saga, or rejects (`409 EmailChangeConflict`, saga stays open) if the pending email was claimed during the window, or no-ops if the window already closed.
- `EmailChangeTimeout` drops the pending change and completes the saga when still open; a late-arriving timeout after resolution is a silent no-op.
- All existing Identity tests remain green (confirm the baseline count at session start) plus new saga unit + integration tests; `dotnet build` zero errors.

## Spec delta

This session satisfies the **three ADDED requirements** in the `customer-registry` capability — *open an email-change saga on request* (5.5), *confirm within the window* (5.6), *drop on timeout* (5.7) — authored in `openspec/changes/slices-5-5-5-7-email-change-saga/specs/customer-registry/spec.md`, in this same session. Workshop 002 v1.1 § 6 carries the GWT scenarios 5.5–5.7; this session both authors and satisfies them. The OpenSpec change is the machine-readable contract; Narrative 009 is the human-readable companion. Both were authored this session, alongside the code — unlike Saga #1, where the design-return session (proposal + specs) preceded the implementation session by a separate PR.

## Orientation files

1. **`docs/workshops/002-identity-event-model.md` § 4 "Saga state and saga messages" + § 5 slices 5.5–5.7 + § 6 GWT scenarios + § 8 items 7–10** — the source of truth, amended this session.
2. **`openspec/changes/slices-5-5-5-7-email-change-saga/{proposal.md,design.md}`** — the "What Changes" build map and the HTTP-wiring decisions (with their ctx7 citations) authored this session.
3. **`docs/research/wolverine-saga-feasibility.md` § Candidate #2** — the original feasibility spike; the illustrative code sketch this saga's shape is grounded in (and diverges from, per the re-request fix and the added guards).
4. **`src/CritterMart.Inventory/Stock/Replenishment.cs`** — the Saga #1 reference implementation: `StartOrHandle` shape, mandatory `NotFound` statics, the `ReplenishDeadline` config-singleton pattern to mirror for `EmailChangeDeadline`.
5. **`src/CritterMart.Identity/Features/RegisterCustomer.cs`** — the `ValidateAsync`-returns-`ProblemDetails` railway idiom to reuse for both `RequestEmailChange`'s and `ConfirmEmailChange`'s guards; the email-normalization convention (`Trim().ToLowerInvariant()`).
6. **`src/CritterMart.Identity/Customers/IdentityDbContext.cs`** — the lowercase-column mapping discipline `EmailChange`'s `OnModelCreating` entry must match; the `ux_customers_email` out-of-band index this saga's conflict checks read against.
7. **`tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` + `IdentityAppFixture.cs`** — the Alba host-fixture pattern; **`tests/CritterMart.Inventory.Tests/ReplenishmentSagaTests.cs`** — the pure-unit-test style for saga `Start`/`Handle` methods, to mirror for `EmailChangeSagaTests.cs`.

## Working pattern

1. **Create feature branch** `feat/identity-email-change-saga`.
2. **Saga + messages first** — `EmailChange.cs` (`StartOrHandle`/`Handle`/`NotFound` methods), the three message records, `EmailChangeDeadline.cs`; unit-test the saga in isolation (open, re-request-updates-pending-without-reschedule, confirm-and-complete, timeout-and-complete) before any HTTP wiring.
3. **Wire HTTP** — `[WolverinePost]` directly on `EmailChange.StartOrHandle` and `EmailChange.Handle(ConfirmEmailChange, ...)`; paired `ValidateAsync` guards per design.md decisions 2–3; map `DbSet<EmailChange>` in `IdentityDbContext`; bind `EmailChangeDeadline` in `Program.cs`.
4. **Write the `NotFound`-to-404 Alba test early** (design.md's flagged open question) — resolve it empirically before writing the remaining integration tests, and note the actual finding in the retro.
5. **Integration tests** — request→open, unknown-customer/duplicate-email rejects, confirm→apply, confirm-conflict→stays-open, confirm-after-expiry→no-op, timeout→drop, timeout-after-confirm→no-op; `dotnet build` / `dotnet test` green.
6. **Retro** at close, confirming the spec-delta closure and recording the `NotFound`→HTTP-status finding.
7. **Live-verify against the real Aspire stack** per [[feedback-live-verify-after-changes]] — drive request→confirm→applied and request→timeout→dropped, and confirm the saga surfaces on CritterWatch ([[feedback-drive-demo-flows]]). Budget this before the CritterWatch trial's 2026-07-10 expiry.

## Deliverable plan (in order)

| File | Status |
|---|---|
| `docs/workshops/002-identity-event-model.md` | v1.0 → v1.1 (this session, already landed) |
| `docs/rules/structural-constraints.md` | v1.6 → v1.7 (this session, already landed) |
| `openspec/changes/slices-5-5-5-7-email-change-saga/proposal.md` | new (this session, already landed) |
| `openspec/changes/slices-5-5-5-7-email-change-saga/specs/customer-registry/spec.md` | new (this session, already landed) |
| `openspec/changes/slices-5-5-5-7-email-change-saga/design.md` | new (this session, already landed) |
| `openspec/changes/slices-5-5-5-7-email-change-saga/tasks.md` | new (this session, already landed) |
| `docs/narratives/009-customer-changes-email.md` | new + README count 8→9 (this session, already landed) |
| `src/CritterMart.Identity/Customers/EmailChange.cs` | new (`EmailChange : Saga`) |
| `src/CritterMart.Identity/Customers/RequestEmailChange.cs` | new (`[SagaIdentity]` on `CustomerId`) |
| `src/CritterMart.Identity/Customers/ConfirmEmailChange.cs` | new (`[SagaIdentity]` on `CustomerId`) |
| `src/CritterMart.Identity/Customers/EmailChangeTimeout.cs` | new (`: TimeoutMessage`) |
| `src/CritterMart.Identity/Customers/EmailChangeDeadline.cs` | new (config-singleton) |
| `src/CritterMart.Identity/Customers/IdentityDbContext.cs` | modify (`DbSet<EmailChange>` mapping) |
| `src/CritterMart.Identity/Program.cs` | modify (bind `EmailChangeDeadline`) |
| `tests/CritterMart.Identity.Tests/EmailChangeSagaTests.cs` | new (unit) |
| `tests/CritterMart.Identity.Tests/*` | add integration coverage |
| `docs/retrospectives/implementations/036-slices-5-5-5-7-email-change-saga.md` | new (at close) |

## Out of scope

- **Any proof the customer controls the new email's inbox** (confirmation token/link) — ADR 009's no-auth stance is unchanged; inventing one would be building an auth feature under cover of a saga demo.
- **A `CustomerEmailChanged` Published-Language event** — no consumer needs one yet; follows the same restraint slice 5.4 already set for `CustomerRegistered`'s early, consumer-less days.
- **A re-armed deadline on re-request** — named as a deliberate trade-off (Workshop 002 § 8, item 7); revisit only if a real product reason surfaces.
- **Name/address edit slices** — no confirm-or-expire window, so no saga; a plain mutating command, unmodeled, per the research's "don't invent a workflow" reasoning.
- **Any new `CritterMart.Contracts` type or cross-BC message** — every saga message is Identity-local by design; if implementation finds a reason to cross the boundary, stop and raise it (it would change the context map).
