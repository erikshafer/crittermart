## Why

Slice 4.2 is the **cross-bounded-context centerpiece** of CritterMart and the OpenTelemetry distributed-trace demo (ADR 005). It is the first time anything crosses a service boundary: the **Orders** context asks the **Inventory** context to set aside an order's goods, over RabbitMQ (ADR 003), and the answer returns as a **Klefter local commit** on the Order's own stream. Narrative 004's Moment 3 is this proposal's human-readable sibling.

This change **bundles slice 4.5** (cancel an order when stock cannot be reserved) so the failure path ends end-to-end rather than leaving a placed order visibly stuck — a deliberate, user-sanctioned deviation from one-slice-one-PR. The bundling is clean precisely because the chosen reservation model (below) makes a refusal leave **nothing reserved**, so the cancel has no stock to release and triggers no cross-BC traffic (slice 2.3 stays out).

It also introduces several firsts: the project's **first Wolverine message handlers** (everything prior is Wolverine.Http endpoints), its **first live RabbitMQ traffic**, its **first shared published-language contract** project, and its **first cross-stream atomic write on the Inventory side**.

## What Changes

- When an order is placed (`OrderPlaced`), Orders sends **one** `ReserveStock { orderId, lines: [{ sku, quantity }, …] }` message — the whole order in a single message — to Inventory over RabbitMQ (conventional routing). The send is a cascading message returned from the place-order handler.
- Inventory handles `ReserveStock` as a **message** (the reserve logic moves off the interim slice-2.2 HTTP route): it reserves **all** of the order's lines **atomically** — every line's SKU `Stock` stream gets a `StockReserved` in one transaction, or none does. Sufficient across all lines → publish `StockReserved { orderId }` back; any line short → modify nothing and publish `StockReservationFailed { orderId, reason }` back.
- Orders records the outcome as a **Klefter local commit** on the Order stream: `StockReserved` → `OrderStatusView` status `stock_reserved`; `StockReservationFailed` → append the failure commit **then** `OrderCancelled { reason: "stock_unavailable" }`, status `cancelled` (the bundled slice 4.5). No release message is sent — nothing was reserved.
- **At-least-once idempotency on both sides** (Workshop § 6.1 requirement): Inventory ignores a duplicate `ReserveStock` for an order already reserved; Orders ignores a `StockReserved`/`StockReservationFailed` for an order whose stream is already terminal or already past that gate.
- `OrderStatusView` gains the `stock_reserved` and `cancelled` statuses and `Apply` methods for the new Order-stream events.
- **Out of scope (named deferrals):** stock release on cancellation (2.3); payment authorization (4.3); confirm (4.4); cancellation on payment decline (4.6) and timeout (4.7); `OrderPaymentTimeout` scheduling and the `OrdersAwaitingPayment*` projection (4.7). The 4.5 cancel sends **no** cross-BC `OrderCancelled` (nothing reserved). No async daemon / Marten event subscriptions (ADR 008).

## Capabilities

### Modified Capabilities

- `order-lifecycle`: the Order aggregate gains stock-reservation behavior on its own stream — it requests reservation when placed, records a granted reservation as a Klefter `StockReserved` (status `stock_reserved`), and, on refusal, records `StockReservationFailed` and cancels with reason `stock_unavailable` (status `cancelled`, the bundled slice 4.5). Both inbound outcomes are idempotent under at-least-once delivery. (Two ADDED requirements.)
- `stock-management`: reserving stock now arrives as a **cross-BC `ReserveStock` message carrying the whole order's lines**, is reserved **all-or-nothing across the lines' SKU streams in one transaction**, and **publishes** the outcome (`StockReserved`/`StockReservationFailed`) back to Orders. Reservation is idempotent against duplicate delivery. (One MODIFIED requirement — the slice-2.2 reserve requirement — plus one ADDED idempotency requirement.)

No new capability — the Orders BC's one-capability-per-aggregate shape (`shopping-cart` + `order-lifecycle`) and Inventory's `stock-management` are unchanged.

## Impact

- **New project `CritterMart.Contracts`** (published language between Orders and Inventory, Customer-Supplier per the context map): the cross-BC message records `ReserveStock` (+ `ReserveStockLine`), `StockReserved`, `StockReservationFailed`. Both services `ProjectReference` it. This is consistent with the existing pattern of both services referencing the shared `ServiceDefaults` project; it does **not** breach "services do not reference each other's projects" (Contracts is not a service). Rationale captured in `design.md`; a follow-up ADR + structural-constraints note is flagged in the retro.
- **`WolverineFx.RabbitMQ`** added to `Directory.Packages.props` (at the Wolverine 6.1.0 line) and referenced by both `CritterMart.Orders` and `CritterMart.Inventory`. Both call `opts.UseRabbitMq(...).AutoProvision()` (conventional routing). The AppHost wires `.WithReference(rabbitmq)` + `.WaitFor(rabbitmq)` onto `orders` and `inventory` (and the stale "slice 2.2" comment on the RabbitMQ resource is corrected).
- **Inventory:** the reserve logic moves from the interim `POST /stock/{sku}/reservations` HTTP route to a `Handle(ReserveStock)` message handler that does the multi-stream atomic reserve and cascades the outcome. The Stock-stream `StockReserved` event keeps its per-SKU shape.
- **Orders:** the `PlaceOrder` endpoint cascades `ReserveStock`; new inbound handlers `Handle(StockReserved)` / `Handle(StockReservationFailed)` append the Order-stream Klefter events (and the 4.5 cancel). `OrderStatusView` + projection extended with the new statuses and `Apply` methods. The Order aggregate's lifecycle frame stays PMvH (ADR 007), but the per-hop trigger is a cascading-message handler, not bespoke PM machinery.
- **Tests:** Wolverine tracked-session tests on both sides (assert the cascaded messages and the Klefter/Stock appends without a broker) plus one RabbitMQ-Testcontainer smoke that exercises the real round-trip. The interim HTTP-reserve tests are replaced by the message-handler tests.
- **Downstream artifacts:** `design.md` + `tasks.md` authored in this same consolidated PR.
