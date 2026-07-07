# Tasks: slices-5-5-5-7-email-change-saga (slices 5.5–5.7)

## Implementation #036 — branch `feat/identity-email-change-saga`

Consolidated into **one PR** (workshop amendment + OpenSpec proposal/specs/design/tasks + narrative + prompt
+ code + tests + retro), per Erik's slice-PR preference — diverging from Saga #1's own two-PR precedent
(design-return PR #112, then implementation PR / retro 035), confirmed with Erik at session start.

### Saga + messages (build first — pure, unit-testable)

- [x] Add `src/CritterMart.Identity/Customers/RequestEmailChange.cs` —
      `record RequestEmailChange([property: SagaIdentity] string CustomerId, string NewEmail)`
- [x] Add `src/CritterMart.Identity/Customers/ConfirmEmailChange.cs` —
      `record ConfirmEmailChange([property: SagaIdentity] string CustomerId)`
- [x] Add `src/CritterMart.Identity/Customers/EmailChangeTimeout.cs` —
      `record EmailChangeTimeout([property: SagaIdentity] string CustomerId, TimeSpan Delay) : TimeoutMessage(Delay)`
- [x] Add `src/CritterMart.Identity/Customers/EmailChangeDeadline.cs` —
      `record EmailChangeDeadline(TimeSpan Duration)` + `static readonly Default` (short, demo-paced —
      mirrors `ReplenishDeadline.Default`; doc-comment notes the modeled production value is 24h)
- [x] Add `src/CritterMart.Identity/Customers/EmailChange.cs` — `EmailChange : Saga` (`Id = CustomerId`,
      `PendingEmail`) with `StartOrHandle(RequestEmailChange, EmailChangeDeadline)`,
      `Handle(ConfirmEmailChange, IdentityDbContext)`, `Handle(EmailChangeTimeout)` + static
      `NotFound(ConfirmEmailChange)` and `NotFound(EmailChangeTimeout)` (design.md decisions 1, 3, 4, 5).
      **Revised from the original plan: no `[WolverinePost]` on this class at all** — see below.

### HTTP wiring (slices 5.5 + 5.6 — new ground, no Saga #1 HTTP precedent)

- [x] ~~`[WolverinePost]` directly on `EmailChange.StartOrHandle`/`Handle(ConfirmEmailChange)`~~ **Failed
      empirically** — Wolverine's HTTP chain builder threw `UnResolvableVariableException` conflating the
      two chains when both lived on the saga class. **Fixed:** two separate static endpoint classes,
      `RequestEmailChangeEndpoint` and `ConfirmEmailChangeEndpoint` (in the same files as their command
      records), each `[WolverinePost]`-annotated, dispatching into the saga via `IMessageBus.InvokeAsync`
      (design.md decision 1, revised).
- [x] `RequestEmailChangeEndpoint.ValidateAsync` guard: `404 CustomerNotFound` (no `Customer` row),
      `409 EmailAlreadyRegistered` (normalized `newEmail` registered to a different customer) (design.md
      decision 2)
- [x] `ConfirmEmailChangeEndpoint.ValidateAsync` guard: loads the `EmailChange` row directly via EF Core
      (not saga-instance-scoped, per the Decision 1 revision); `409 EmailChangeConflict` if `PendingEmail`
      is now registered to a different customer (design.md decision 3)
- [x] **Verified empirically** (design.md's flagged open question): a `NotFound`-routed `ConfirmEmailChange`
      does **NOT** return `404` — `IMessageBus.InvokeAsync` doesn't propagate the saga's not-found state to
      the HTTP layer, so the endpoint's void `Post` returns Wolverine.Http's default success status for an
      action with no response value, **`204`**, indistinguishable from an actual successful confirm. Judged
      the more faithful "silent no-op" than a manufactured 404; no existence pre-check added (design.md
      decision 3, Risks).

### Wiring (persistence)

- [x] Modify `src/CritterMart.Identity/Customers/IdentityDbContext.cs` — add `DbSet<EmailChange>`, map in
      `OnModelCreating` with the same lowercase-column discipline as `Customer` (design.md decision 8).
      **Also required an explicit mapping for the base `Saga` class's inherited `Version` property**
      (`HasColumnName("version")`) — an undocumented casing gap found only by running the integration tests
      (a live `"column e.Version does not exist"` failure); see design.md decision 8's addendum.
- [x] Modify `src/CritterMart.Identity/Program.cs` — bind `EmailChangeDeadline` from
      `Identity:EmailChangeTimeout` (`GetValue<TimeSpan?>(...) ?? Default` + `AddSingleton`, mirrors
      `ReplenishDeadline`'s binding in Inventory's `Program.cs`)

### Tests

- [x] Add `tests/CritterMart.Identity.Tests/EmailChangeSagaTests.cs` (pure unit, mirrors
      `ReplenishmentSagaTests.cs`) — open on request, re-request updates `PendingEmail` without a second
      cascaded timeout, timeout-and-complete. (`Handle(ConfirmEmailChange, IdentityDbContext)` needs a real
      `DbContext`, so unlike `Replenishment`'s fully-pure surface, confirm's behavior is covered only at the
      integration level — a real asymmetry, not an oversight.)
- [x] Add integration coverage (Alba, mirrors `ReplenishmentSagaIntegrationTests.cs` +
      `RegisterCustomerTests.cs`'s host-fixture pattern) — request→open (`EmailChange` row + scheduled
      timeout), unknown-customer reject, duplicate-email reject, re-request updates pending email, confirm→apply
      (`Customer.Email` updated + saga row gone), confirm-conflict→stays-open, confirm-after-expiry→no-op
      (the empirical `NotFound`/`204` finding), timeout→drop, timeout-after-confirm-or-timeout→no-op. 9
      integration tests + 3 unit tests, all green.
- [x] `dotnet build` zero errors (whole repo, all 13 projects); `dotnet test` (whole repo) — 161/161 green,
      including all pre-existing Catalog/Inventory/Orders/CrossBc/Identity tests — no regressions.

### Artifacts

- [x] `docs/narratives/009-customer-changes-email.md` (v1.0) + `docs/narratives/README.md` count 8→9 —
      threads the email-change journey from the customer's perspective, sibling of this change
- [x] `docs/prompts/implementations/036-slices-5-5-5-7-email-change-saga.md` — the frozen session prompt;
      `docs/prompts/README.md` implementations count 35→36
- [x] `docs/retrospectives/implementations/036-slices-5-5-5-7-email-change-saga.md` — spec-delta closure (3
      ADDED `customer-registry` requirements landed); confirms the `NotFound`→`204` (not `404`) finding
- [x] Live-verify against the real Aspire stack (demo runbook): request → confirm → applied; request →
      timeout → dropped; both HTTP guards (`404`/`409`). **Not verified: CritterWatch's saga-lifecycle
      visual surface** (Workshop 002 § 8, item 10) — no browser automation was available this session;
      flagged in the retro's Outstanding section, not silently skipped.
- [x] `openspec archive slices-5-5-5-7-email-change-saga -y` — **post-merge tidy**, not this PR (per
      `customer-data`/Saga #1 precedent). Done 2026-07-07.
