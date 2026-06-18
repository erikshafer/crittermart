---
retrospective: 034
kind: implementations
prompt: docs/prompts/implementations/034-slices-5-3-5-4-customer-data.md
deliverable: openspec/changes/customer-data/ (proposal + design + tasks + customer-registry spec MODIFIED + order-lifecycle spec 2 ADDED), docs/narratives/007-customer-data-in-orders.md (v1.0), src/CritterMart.Contracts/CustomerRegistered.cs (new), src/CritterMart.Identity/{CritterMart.Identity.csproj,Customers/CustomerRegistered.cs (deleted),Features/RegisterCustomer.cs}, src/CritterMart.Orders/{Customers/LocalCustomerView.cs,Customers/CustomerRegisteredHandler.cs,Ordering/EnrichedOrderView.cs,Features/PlaceOrder.cs,Features/ListMyOrders.cs,Program.cs}, src/CritterMart.AppHost/Program.cs, src/CritterMart.Seeding/Program.cs, tests/CritterMart.Orders.Tests/CustomerRegisteredHandlerTests.cs (new), tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs (explicit-id test added)
date: 2026-06-18
mode: solo implementation
session-runner: Claude (Sonnet 4.6)
---

# Retrospective — Implementations 034: Slices 5.3 + 5.4 — customer data in Orders (consume CustomerRegistered, enrich order responses, extend seeder)

## Outcome summary

Completed the Published-Language edge between Identity and Orders that slice 033 had left declared-not-trafficked. Two slices, one PR, per the consolidate-slice-PRs convention.

**Slice 5.4 (built first):** `CustomerRegistered` graduates from `CritterMart.Identity.Customers` (Identity-internal) to `CritterMart.Contracts` (the shared PL assembly) at the moment Orders gains a consumer. `CustomerRegisteredHandler` in Orders — a plain `static Handle(CustomerRegistered, IDocumentSession)` method — upserts a `LocalCustomerView` document (`session.Store(...)`, `AutoApplyTransactions` commits). No Marten async daemon, no event subscriptions, no forwarding; the message arrives via RabbitMQ as a Wolverine envelope, exactly as `StockReserved` does.

**Slice 5.3 (built second):** `GET /orders/{orderId}` and `GET /orders/mine` now return `EnrichedOrderView` — the existing `OrderStatusView` wire shape plus `customerName?`. The enrichment is read-time: the endpoints load `LocalCustomerView` by customerId (a second primary-key hit; no query, no join) and call `EnrichedOrderView.From(view, customer?.DisplayName)`. When the local model is absent (eventually-consistent gap — PL event not yet delivered), `customerName` is `null`. The `OrderStatusView` projection is untouched; the enrichment wrapper is purely additive.

**Seeder extension:** `RegisterCustomer` gains `string? Id = null`; the seeder passes `"customer-demo"` explicitly, matching the frontend's `useCurrentCustomer` stub. The AppHost captures `identity` as a variable and injects `IDENTITY_URL` + `WaitFor(identity)` on the seeder.

**Tests:** 5 new in `CritterMart.Orders.Tests` (handler upsert, handler idempotency, GET order enriched present, GET order enriched absent/null, GET list enriched) + 1 new in `CritterMart.Identity.Tests` (registering with explicit id). All 133 tests green (`dotnet test --no-build` confirms). `dotnet build` zero errors.

## What worked

- **`InvokeMessageAndWaitAsync` as the handler test instrument.** Driving `CustomerRegisteredHandler` through Wolverine's full pipeline (rather than calling `Handle()` directly and adding manual `SaveChangesAsync()`) proved the `AutoApplyTransactions` middleware commits correctly and tested the whole message-dispatch path — exactly the pattern `StockReservationOutcomeTests` uses for `StockReserved`. No surprise here; spotting the precedent in `StockReservationOutcomeTests.cs` made the test shape instant.

- **The build-order constraint (5.4 before 5.3) was correct and paid off.** The enrichment endpoints reference `LocalCustomerView` and `EnrichedOrderView`, which only exist after 5.4's handler and types are in place. Implementing in the named order produced a single coherent diff with no forward references, and the compiler confirmed the ordering with zero ambiguity.

- **One `LocalCustomerView` load for the whole orders list.** All rows in `GET /orders/mine` share the same `customerId` from the `X-Customer-Id` header — one `LoadAsync<LocalCustomerView>(customerId)` before the `Select` projection serves all rows. The alternative (one load per order row) would be a classic N+1. Recognizing the invariant (single-customer query keyed by the header) eliminated the N+1 before it was written.

- **`EnrichedOrderView` as an additive wrapper, not a mutation of `OrderStatusView`.** The existing `OrderStatusView` fields appear in the same order in `EnrichedOrderView`, with `customerName?` appended. Existing deserializers that don't know the new field ignore it; the contract is backward-compatible. The `OrderStatusView` projection itself carries no cross-BC dependency. Two separations in one decision: read model purity, and forward-compatible wire shape.

- **The `CustomerRegistered` exchange rename is safe because there were no consumer bindings.** The old exchange (`crittermart.identity.customers:customerregistered`) was declared unconsumed since slice 033 shipped. Renaming by moving the type to `CritterMart.Contracts` simply creates a new exchange on the next boot; no in-flight messages are lost, no consumer queue was ever bound to the old name. It is explicitly safe to rename an unconsumed PL exchange.

## What was harder / notable

- **Context-window compaction mid-session.** This session ran over two context windows (compaction occurred between task 6 and task 7). The `InvokeMessageAndWaitAsync` pattern and the exact test structure had to be re-derived from the in-repo precedent (`StockReservationOutcomeTests.cs`) rather than recalled from the earlier session context. The skill files (marten-event-subscriptions) loaded in the first window were not available in the second — re-reading them was skipped because the handler pattern was already confirmed from the precedent. No methodology gap, but it illustrates that compaction breaks skill-file context and the codebase's own precedents are the most reliable orientation point after a compaction.

- **One build error from the `CustomerRegistered` namespace change.** After moving the type to `CritterMart.Contracts`, the Identity test file still imported `CritterMart.Identity.Customers` for `CustomerRegistered`. Fix: add `using CritterMart.Contracts;` to the test file. Simple, but it means any test or consuming code needs an active check when the type's namespace changes — `dotnet build` was the catcher.

- **`ListMyOrdersTests` vs. `EnrichedOrderView`.** Existing tests in `ListMyOrdersTests` deserialize the `GET /orders/mine` response as `List<OrderStatusView>`. Since `EnrichedOrderView` is additive (all `OrderStatusView` fields present in order, `customerName` appended), `System.Text.Json` ignores the extra field and deserializes cleanly — no test changes required. Worth noting because it is not immediately obvious that the existing tests survive the return-type change without modification.

## Methodology refinements

- **When moving a shared type across namespaces, grep all test files for the old namespace before committing.** The compiler catches the first build, but the fix is mechanical; a pre-commit scan of all test/source files for the old namespace makes it part of the move, not a follow-up error.

- **Read-time enrichment is the correct pattern when a projection must stay pure.** `OrderStatusView` is a self-aggregating inline snapshot — it folds events from the Order stream and carries no cross-BC data. Adding a cross-BC field to it would violate the ADR 020 read/write split (the projection's correctness depends only on events in its own stream). Read-time enrichment (load a second document at the endpoint, compose an enrichment wrapper) is the pattern that preserves the projection's purity. The `EnrichedOrderView` shape makes the composition visible and testable independently.

- **For the list endpoint, check whether all rows share the same key before coding the enrichment.** `GET /orders/mine` is keyed by `X-Customer-Id`; every order in the result has the same `customerId`. One document load serves all rows. For a list endpoint keyed by something more complex (e.g., a mixed-customer admin view), the load would need to be per-unique-id. Establish the invariant before writing the enrichment loop.

## Outstanding / next-session inputs

- **`openspec archive customer-data` — post-merge tidy.** Syncs the `customer-data` change into `openspec/specs/` (active → archive). Standard post-merge step; run with `-y` to skip the interactive prompt.

- **`openspec/specs/customer-registry/spec.md` `## Purpose` is still TBD placeholder.** Flagged in the proposal; not fixed here (out of scope). A future `tidy: docs` session should fill it.

- **Frontend schema update for `customerName?`.** The SPA's `OrderStatusViewSchema` (Zod, in `client/`) doesn't know about `customerName` yet. The field will arrive in the JSON and be stripped by the schema validator. A follow-up frontend slice should add the optional field and surface it in the W4 order-tracking screen.

- **`POST /orders` body-keyed customerId harmonization.** The command still takes `PlaceOrder(string CustomerId)` from the body, while the other customer-keyed endpoints use the `X-Customer-Id` header. A follow-up harmonization slice would move it to the header.

- **POST-TALK: delete `Payment__DeclineOverAmount=100`** from `src/CritterMart.AppHost/Program.cs`. Still live in the AppHost — a single-line remove that restores always-approve behavior after the demo.

- **Live-stack verification** — offered proactively after merge per `[[feedback-live-verify-after-changes]]`. The seeder should now register `customer-demo` and `GET /orders/{orderId}` should return `customerName: "Demo Customer"` for any order placed by `customer-demo`.

- **Carry-forwards (unchanged, non-blocking):** owed owner eyeballs (#77 trace, #78 Docker grouping); CritterWatch trial expires 2026-07-10; `NU1507` + `global.json` SDK pin; no frontend CI job; spike branch `spike/efcore-identity` retained as reference impl.

## Spec-delta — landed?

**Named delta landed.** The prompt named: two new requirements in `order-lifecycle` (*Consume `CustomerRegistered` and maintain a local customer read model*, slice 5.4; *Resolve customer identity at read time*, slice 5.3) plus one modified requirement in `customer-registry` (the optional-explicit-id addition). All three are in the `customer-data` OpenSpec change — 2 ADDED reqs in `order-lifecycle/spec.md`, 1 MODIFIED req in `customer-registry/spec.md`. Narrative 007 (v1.0) is the human-readable companion. All five handler + enrichment tests (Orders) and the explicit-id test (Identity) are green. `openspec validate customer-data --strict` confirms the change is Complete. Four-step closure: **prompt named → session executed → this retro confirms → `customer-data` change records it.** Post-merge tidy: `openspec archive customer-data -y`.
