# Prompt: Implementations 008 — Slice 4.2 Reserve Stock Cross-BC (+ 4.5 cancel-on-stock-failure, one PR)

**Kind**: per-slice implementation, consolidated one PR (narrative bump + OpenSpec proposal + implementation + prompt/retro), per the consolidate-slice-prs convention. **Bundles slices 4.2 + 4.5** — a user-sanctioned deviation from one-slice-one-PR (resolved at this session's scope fork) so the stock-failure path ends end-to-end rather than leaving a visibly stuck order.
**Files touched**: this prompt; `openspec/changes/slice-4-2-reserve-stock/{proposal.md, specs/order-lifecycle/spec.md, specs/stock-management/spec.md, design.md, tasks.md}` (new); `docs/narratives/004-customer-purchase.md` (→ v1.2, Moment 3); `Directory.Packages.props` (+ `WolverineFx.RabbitMQ`); `src/CritterMart.Orders/CritterMart.Orders.csproj` + `src/CritterMart.Inventory/CritterMart.Inventory.csproj` (+ RabbitMQ ref); `src/CritterMart.AppHost/Program.cs` (`.WithReference(rabbitmq)`/`.WaitFor` on `orders`+`inventory`; fix stale "slice 2.2" comment); a shared/cross-BC message contract (`ReserveStock`/`StockReserved`/`StockReservationFailed`); `src/CritterMart.Inventory/Features/ReserveStock.cs` (+ message handler) and/or a new handler file; `src/CritterMart.Orders/Features/PlaceOrder.cs` (cascade `ReserveStock`); new Orders inbound handler(s) for the Klefter commits; `src/CritterMart.Orders/Order/OrderStatusView.cs` (+ statuses, + `Apply` methods, + `StockReserved`/`StockReservationFailed`/`OrderCancelled` events); both services' `Program.cs` (`UseRabbitMq().AutoProvision()`); `tests/CritterMart.Orders.Tests/**` + `tests/CritterMart.Inventory.Tests/**` (tracked-session + one broker smoke); `docs/retrospectives/implementations/008-slice-4-2-reserve-stock.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; collaborative on genuine forks (present options + recommendation, user decides — memory `feedback-collaborate-on-decisions`, `feedback-options-with-previews`)
**Commit subject**: `feat: slice 4.2 reserve stock cross-BC (+ 4.5 cancel on stock failure)`

## Framing

Slice 4.2 is the **cross-BC, over-the-broker centerpiece** of the whole project and the OpenTelemetry distributed-trace demo (ADR 005). It is the first time CritterMart sends a message across a bounded-context boundary: Orders sends `ReserveStock` to Inventory over RabbitMQ (ADR 003), Inventory reserves (or refuses) on the Stock stream, and the outcome returns to Orders as a **Klefter local commit** on the Order stream. It introduces the project's **first Wolverine message handlers** (until now everything is Wolverine.Http endpoints), the **first live broker traffic**, and the **first multi-service test**. Bundled slice 4.5 closes the failure path: a `StockReservationFailed` Klefter commit drives the aggregate decision `OrderCancelled(stock_unavailable)`.

## Goal

When an order is placed (`OrderPlaced`), Orders cascades a `ReserveStock { orderId, sku, quantity }` message to Inventory over RabbitMQ. Inventory's message handler mirrors the existing reserve logic: sufficient stock → append `StockReserved` on the Stock stream and cascade `StockReserved` back; insufficient/no stock → no Stock-stream change and cascade `StockReservationFailed { orderId, sku, reason }` back. On the grant, Orders appends the Klefter `StockReserved` to the Order stream and `OrderStatusView` reads `stock_reserved`. On the refusal, Orders appends the Klefter `StockReservationFailed` **and** `OrderCancelled(stock_unavailable)`; `OrderStatusView` reads `cancelled`. Both sides are idempotent under at-least-once delivery (duplicate-guard). Proven by tracked-session tests both sides + one RabbitMQ-container smoke; `openspec validate --strict` passes.

## Spec delta

A new OpenSpec change `slice-4-2-reserve-stock` with **two** capability deltas (no new capability — one-per-aggregate holds):
- **`order-lifecycle`** (MODIFIED/ADDED): the Order aggregate gains the Klefter `StockReserved` commit (status `stock_reserved`), the Klefter `StockReservationFailed` commit, and the 4.5 aggregate decision `OrderCancelled(stock_unavailable)` (status `cancelled`); plus the Orders-side at-least-once guard (terminal/already-present → ignore).
- **`stock-management`** (MODIFIED): reserving stock now arrives as a **cross-BC message** (not only the HTTP route), Inventory **publishes** `StockReserved`/`StockReservationFailed` back to Orders, and the handler is idempotent against duplicate `ReserveStock` for an `orderId` already reserved.

Narrative 004 gains **Moment 3** (→ v1.2, `slices [3.1, 4.1, 4.2, 4.5]`). Workshop § 6.1 slices 4.2 + 4.5 are satisfied as written (incl. the required failure + duplicate-delivery scenarios); assess whether any amendment is needed (the bundling of 4.5 and the message-shape choice may warrant a note).

## Locked decisions (collaborative forks, resolved with the user this session)

1. **Scope = bundle 4.5.** Reserve round-trip (grant + fail Klefter commits) **plus** the 4.5 aggregate decision `OrderCancelled(stock_unavailable)`. No cross-BC release — no reservation existed to release (2.3 stays out). Sanctioned deviation from one-slice-one-PR so the failure path is end-to-end.
2. **Trigger = cascading messages from handlers** (NOT bespoke PMvH machinery, NOT raw `IMessageBus.PublishAsync`, NOT Marten event subscriptions). `PlaceOrder` returns `(IResult, ReserveStock)`; Inventory's `Handle(ReserveStock)` cascades `StockReserved`/`StockReservationFailed`; Orders' inbound handlers append the Klefter events. Cascading is a first-class, heavily-tested Wolverine feature; PMvH (ADR 007) remains the Order's *lifecycle* frame, not per-hop machinery (memory `feedback-cascading-over-pmvh`). **Event Subscriptions were ruled out — they require Marten's async daemon, which ADR 008 / CLAUDE.md "Do Not" forbids for round one.**
3. **RMQ topology = conventional routing** (`UseRabbitMq(...).AutoProvision()`). Lowest ceremony; the broker hop still shows in the OTel trace. Explicit exchanges/queues/bindings declined for round one.
4. **Tests = tracked-session per side + one thin broker smoke.** `InvokeMessageAndWaitAsync` / `TrackActivity` assert the cascades with no broker (fast); one RabbitMQ-Testcontainer smoke exercises the real wire.
5. **Duplicate-guards in scope, both sides** (§ 6.1 GWT requires them): Inventory — reservation for `orderId` already exists → no-op (re-publish or not; either preserves correctness); Orders — Order stream terminal or `StockReserved` already present → ignore.
6. **CritterWatch (ADR 013) deferred** to a dedicated follow-up — unresolved tier/feed/license question (paid feed 401s on CI). Goes to the retro Outstanding list, not this PR.

## Message-contract decision (the one real design wrinkle — resolve in implementation, flag to user)

Inventory's current `ReserveStock` is an **HTTP** command — `record ReserveStock(string OrderId, int Quantity)` with **SKU on the route** (`POST /stock/{sku}/reservations`). The cross-BC **message** has no route, so its wire shape is `ReserveStock { orderId, sku, quantity }` (SKU in the body). These cannot cleanly be the same type. Decide deliberately (do **not** silently overwrite `ReserveStock.cs`): introduce a distinct cross-BC message contract, and either retain the HTTP endpoint as an interim trigger or retire it. Where the shared message contract physically lives (a shared project vs. duplicated records per service) must respect the structural-constraints rule that services do not reference each other's projects — confirm against `docs/rules/structural-constraints.md` before choosing.

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** §§ 2 (BC summary, Klefter notes), 3 (full Place Order storyboard — 4.2 is the cross-BC hop + the OTel span list), 4 (Inventory + Order vocabulary), 5 (slice rows 2.2/2.3/4.2/4.5), 6.1 (GWT for 2.2, 4.2, 4.5 incl. required failure + duplicate-delivery scenarios).
2. **`docs/narratives/004-customer-purchase.md`** (v1.1) — Moment 2 ends at `awaiting_confirmation`; the "does *not* yet see" bullets explicitly defer stock reservation to 4.2. Moment 3 continues from there.
3. **`openspec/specs/order-lifecycle/spec.md`** + **`openspec/specs/stock-management/spec.md`** — the two durable specs this change extends.
4. **`src/CritterMart.Inventory/Features/ReserveStock.cs`** + **`Stock/StockReserved.cs`** + **`Stock/StockLevelView.cs`** — the existing reserve logic (insufficient-stock guard) to mirror into a message handler; the Stock-stream `StockReserved` event.
5. **`src/CritterMart.Orders/Order/OrderStatusView.cs`** + **`Features/PlaceOrder.cs`** — the Order stream/projection to extend and the handler that cascades `ReserveStock`.
6. **`src/CritterMart.AppHost/Program.cs`** — RabbitMQ provisioned-but-unreferenced (line ~14, stale "slice 2.2" comment to fix).
7. **ADRs 003 (RabbitMQ transport), 007 (PMvH for Order), 005 (OTel), 008 (inline projections, no daemon).** **`docs/rules/structural-constraints.md`** for the cross-service contract placement.
8. **Stack reality**: `Directory.Packages.props` (Wolverine 6.1 / Marten 9.2 / JasperFx 2.2 / .NET 10) — **no `WolverineFx.RabbitMQ` entry yet; add at 6.1.0**. `WolverineFx.RuntimeCompilation` needed for Dynamic codegen. `nuget.config` is nuget.org-only behind `<clear />` — do **not** re-add `packages.jasperfx.net` (401s on CI).
9. **Skills**: `wolverine-integrations-rabbitmq`, `wolverine-messaging-message-routing`, `wolverine-handlers-fundamentals`, `marten-aggregate-handler-workflow`, `wolverine-testing-integration` (+ `wolverine-testing-with-testcontainers`). Use `find-docs` (ctx7 `/jasperfx/wolverine`) for the current RabbitMQ setup + Wolverine.Http tuple-cascade signature **before** writing transport/cascade code — the API surface moves.

## Working pattern

Author in pipeline order, all on branch `feat/slice-4-2-reserve-stock`: (1) this frozen prompt [review gate]; (2) narrative 004 → v1.2; (3) OpenSpec change + `validate --strict`; (4) implementation (packages → AppHost wiring → message contracts → Inventory handler → Orders cascade + inbound handlers → projection/statuses); (5) tests green; (6) retro. Verify Wolverine RabbitMQ + cascade API against current docs before wiring. One consolidated PR; the user merges. `openspec archive` is a post-merge `tidy:` step.

## Out of scope

- **No slice 2.3 (release stock), 4.3 (payment), 4.4 (confirm), 4.6/4.7 (other cancels).** 4.5's cancel emits **no** cross-BC `OrderCancelled` (no reservation to release).
- **No `OrderPaymentTimeout` scheduling, no `OrdersAwaitingPayment*` projection** — those are 4.7's; this slice schedules nothing (timeout-deferral heuristic).
- **No CritterWatch** (ADR 013) — deferred follow-up.
- **No async daemon / Event Subscriptions** (ADR 008).
- **No README/index refresh** and **no `openspec archive`** — both post-merge `tidy: docs` concerns (no opportunistic edits).
- **No new capability** — deltas land on existing `order-lifecycle` + `stock-management`.
