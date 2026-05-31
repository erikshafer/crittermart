# Retrospective: Implementations 008 — Slice 4.2 Reserve Stock Cross-BC (+ 4.5 cancel on stock failure)

**Prompt**: `docs/prompts/implementations/008-slice-4-2-reserve-stock.md`
**Outcome**: shipped — the project's first cross-BC message flow. Orders cascades `ReserveStock` to Inventory over RabbitMQ; Inventory reserves all lines atomically and cascades `StockReserved`/`StockReservationFailed` back; Orders records the outcome as a Klefter local commit (`stock_reserved`), or (bundled slice 4.5) records the failure and `OrderCancelled(stock_unavailable)` (`cancelled`). OpenSpec change `slice-4-2-reserve-stock` `--strict` valid; Narrative 004 → v1.2 (Moment 3). One consolidated PR.
**Tests**: full solution green — **32 total, 0 failures**: Orders 18 (7 unit folds + 11 integration incl. the cascade + inbound-handler + idempotency tracked-session tests), Inventory 6, Catalog 7 (unchanged), and **1 cross-BC two-host broker smoke** (new `CritterMart.CrossBc.Tests`).

## What shipped

- **`CritterMart.Contracts`** — new published-language project (both services reference it) holding the three cross-BC wire records: `ReserveStock`(+`ReserveStockLine`), `StockReserved`, `StockReservationFailed`. No Wolverine/Marten dependency.
- **RabbitMQ transport** — `WolverineFx.RabbitMQ` added (central package + both services); `UseRabbitMqUsingNamedConnection("rabbitmq").AutoProvision().UseConventionalRouting()`; AppHost wires `.WithReference(rabbitmq).WaitFor(rabbitmq)` on both `orders` and `inventory` (stale "slice 2.2" comment corrected). `opts.ApplicationAssembly = typeof(Program).Assembly` added to both for deterministic discovery.
- **Inventory** — `ReserveStockHandler.Handle(ReserveStock)`: all-or-nothing multi-stream reserve (one `StockReserved` per SKU in one transaction, or none), idempotent via `StockLevelView.Reservations`; cascades the outcome. The interim slice-2.2 HTTP route is retired.
- **Orders** — `PlaceOrder` cascades a whole-order `ReserveStock` (`(IResult, ReserveStock?)`); inbound `StockReservedHandler` / `StockReservationFailedHandler` append the Klefter events (the latter also `OrderCancelled`, slice 4.5), both guarded to act only while `awaiting_confirmation`. `OrderStatusView` gains `stock_reserved` / `cancelled` + folds.
- **OpenSpec** — `order-lifecycle` (2 ADDED reqs) + `stock-management` (1 MODIFIED + 1 ADDED); `design.md` (9 decisions) + `tasks.md`.
- **Narrative 004 → v1.2** — Moment 3.

## What worked

- **Cascading messages were the right call and held across every hop.** `PlaceOrder → ReserveStock`, `ReserveStock → StockReserved`, `StockReserved → (Klefter append)` — one uniform shape, no PMvH machinery. The whole flow unit/integration-tested with no broker via `DisableAllExternalWolverineTransports()` + tracked sessions (`tracked.Sent.SingleMessage<T>()`).
- **The whole-order-atomic reservation model made the bundled 4.5 fall out cleanly.** Because Inventory reserves all lines or none, a refusal reserved nothing — so the stock-failure cancel has nothing to release, sends no cross-BC `OrderCancelled`, and needs no slice 2.3. The two forks (scope + reserve model) reinforced each other.
- **The two-host broker smoke caught a real latent bug.** Without an explicit `opts.ApplicationAssembly`, Wolverine's endpoint discovery is non-deterministic when two service assemblies share a process — both hosts mapped Inventory's routes. Every isolated per-service test missed it (one assembly each). Setting `ApplicationAssembly` explicitly fixed it and is a genuine production-config robustness improvement. The test paid for itself the moment it ran.

## What was harder / notable

- **Three `StockReserved`s, one fact.** `Inventory.Stock.StockReserved` (per-SKU stream event), `Contracts.StockReserved` (order-level wire message), `Orders.Order.StockReserved` (order-level Klefter event). Each owned by its context, the shared project owns only the wire message. Disambiguated by namespace; documented in design.md decision 5. Easy to confuse — worth the explicit decision record.
- **Two `Program`s in one test process.** Booting both services in `CritterMart.CrossBc.Tests` required an MSBuild `Aliases="InventoryApp"` on the Inventory reference + `extern alias` so `AlbaHost.For<Program>()` (Orders) stayed unambiguous. Combined with the `ApplicationAssembly` fix, the two-host fixture is stable.
- **Conditional cascade from an HTTP endpoint.** `PlaceOrder` returns `(IResult, ReserveStock?)`; the 409 paths return a `null` cascade (Wolverine skips it). Clean once found, but the nullable-tuple-member idiom isn't obvious.
- **Workshop divergence (documented, faithful).** The Workshop's `ReserveStock { orderId, sku, quantity }` is single-SKU; the shipped contract carries the whole order's lines. The §6.1 GWT used single-line orders by example, not intent — recorded in design.md decision 2.

## Methodology refinements

- **A two-host integration test is worth its weight once a slice crosses a process boundary** — it exercises real serialization/routing AND surfaces process-composition bugs (the `ApplicationAssembly` issue) that single-service tests structurally cannot. Candidate convention: the first cross-BC slice for any new service pair gets one such smoke.
- **Forks that shape the contract should be resolved before authoring the spec, not during implementation.** The multi-line reservation model wasn't in the handoff's fork list; it surfaced while writing the `order-lifecycle` SHALL statements. Catching it there (and pausing to ask) kept the spec and code aligned from the start. Reinforces putting "how does this behave for the non-trivial input?" to the user early.

## Outstanding / next-session inputs

- **ADR for the published-language contracts project** (+ paired `docs/rules/structural-constraints.md` note): the `CritterMart.Contracts` shared-project decision meets the ADR bar (spans two BCs, non-obvious tradeoff). Deliberately kept out of this feat PR per the frozen prompt's scope — a follow-up `tidy:` / ADR session. Rationale is captured in design.md decision 4 in the meantime.
- **`openspec archive slice-4-2-reserve-stock`** after merge → folds the two deltas into the durable `order-lifecycle` + `stock-management` main specs (the latter's `## Purpose` is still the `TBD` archive placeholder from slice 2.1 — worth filling in the same `tidy:` step).
- **README / index refresh** (BC-status rows, the new `CritterMart.Contracts` + `CritterMart.CrossBc.Tests` projects, capability/test counts) — a separate `tidy: docs` concern, kept out of this PR (no opportunistic edits).
- **CritterWatch (ADR 013)** — still deferred; 4.2 is the first slice with live broker traffic, so it remains the natural home, but the unresolved tier/feed/license question (paid feed 401s on CI) keeps it a dedicated follow-up.
- **Design-return cadence**: this is the **2nd implementation PR** against Orders since the #28/#30 design-return (after 4.1). One more Orders implementation slice is in budget before the next mandatory interleave.
- **Next slice**: **4.3 — authorize payment (stubbed)**, the second gate. It reacts to the Order-stream `StockReserved` and cascades `AuthorizePayment` to the in-process stubbed provider — the same cascading-handler shape this slice established, now in-process rather than cross-BC.

## Spec-delta — landed?

**Yes.** `order-lifecycle` (ADDED: reserve stock for a placed order; cancel on stock failure) and `stock-management` (MODIFIED: cross-BC reserve + publish-back; ADDED: idempotent reservation) authored and `--strict` valid; satisfied by code (32 green tests incl. the real-broker round-trip). Narrative 004 records Moment 3 in its Document History (v1.2). Workshop § 6.1 slices 4.2 + 4.5 satisfied; the single-SKU→whole-order message-shape divergence is documented in design.md decision 2 (a faithfulness note; no workshop amendment required this slice as the §5 slice-table intent is preserved).
