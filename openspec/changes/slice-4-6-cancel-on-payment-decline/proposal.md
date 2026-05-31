## Why

Slice 4.3 recorded a declined payment as a `PaymentAuthFailed` Klefter commit and **deliberately stopped there** — the order's visible status stayed `stock_reserved` and the already-reserved stock stayed held (a named deferral). Slice 4.6 turns that non-terminal state **terminal**: the Order aggregate cancels itself on decline and, because stock *was* reserved (unlike the 4.5 stock-failure path, where nothing was reserved), it must **release** that reservation back to Inventory.

This is the project's **first cancellation that crosses a bounded-context boundary back**: Orders publishes a release request to Inventory over RabbitMQ, and Inventory compensates by releasing the reservation. It therefore **bundles Inventory slice 2.3** (release reserved stock) — 4.6 is unverifiable end-to-end without the Inventory side, exactly as slice 4.2 bundled both sides of the reserve round-trip. Narrative 004's Moment 5 is this proposal's human-readable sibling.

## What Changes

- **Orders (4.6).** `PaymentDecisionHandler`'s decline branch — today a single `PaymentAuthFailed` append — now also appends `OrderCancelled { reason: "payment_declined" }` (the same in-handler aggregate-decision shape slice 4.5 used for stock failure) and **cascades a single `ReleaseStock { orderId, lines }` message** carrying the order's lines (read from the Order stream's own `OrderStatusView.Lines`). `OrderStatusView` settles on `cancelled`. The existing stream-state guard (`status == stock_reserved`) makes this idempotent: a duplicate decision, or one for an order already terminal / unknown, is a silent no-op and cascades nothing.
- **`CancelReason.PaymentDeclined`** (`"payment_declined"`) is added to the Orders `CancelReason` constants; the existing `OrderCancelled` event and its `Apply` fold are reused unchanged (the cancellation status transition already exists from 4.5).
- **Inventory (2.3).** A new `ReleaseStockHandler` consumes `Contracts.ReleaseStock` (auto-listened by conventional routing, mirroring `ReserveStockHandler`). For each line it loads the SKU's `StockLevelView` and, **only if that order holds a reservation on the SKU** (`Reservations.Contains(orderId)`), appends a `StockReleased { sku, orderId, quantity }` event. A line for which no reservation exists (duplicate delivery, already released) is a per-SKU silent no-op. `StockLevelViewProjection` gains `Apply(StockReleased)`: available `+= quantity`, reserved `-= quantity`, and the order id is removed from `Reservations`.
- **Published language (ADR 014).** The cross-BC contract is a new `CritterMart.Contracts.ReleaseStock { orderId, lines: [{ sku, quantity }] }` — symmetric with `ReserveStock`, keeping Inventory's wire language about *stock* rather than *orders*. This is a **deliberate divergence** from Workshop § 2.3/4.6, which wrote the message as a published `OrderCancelled { orderId }` event; the rationale is recorded in `design.md` (Decision 1).
- **Reuse for 4.7.** The `ReleaseStock` contract and `ReleaseStockHandler` are built so the deferred payment-timeout slice (4.7) publishes the **same** message with no change to Inventory.
- **Out of scope (named deferrals):** cancellation on payment timeout (4.7) and its `OrderPaymentTimeout` scheduling + `OrdersAwaitingPayment*` projection. Committing reserved stock on confirmation (no `StockCommitted` — reserved stock simply stays reserved on `OrderConfirmed`; a future-ADR candidate per Workshop § 8). No async daemon / Marten event subscriptions (ADR 008). No real payment integration (stubbed — vision.md).

## Capabilities

### Modified Capabilities

- `order-lifecycle`: the Order aggregate gains terminal cancellation on payment decline — when a `PaymentAuthFailed` is recorded at the payment gate it appends `OrderCancelled { reason: "payment_declined" }` (status `cancelled`) and publishes a `ReleaseStock` message to Inventory carrying the order's lines, idempotently under duplicate delivery. (One ADDED requirement: cancel an order when payment is declined.)
- `stock-management`: Inventory gains the cross-BC release path — on a `ReleaseStock` message it releases each line's reservation (available rises, reserved falls, the order id is dropped from the SKU's reservations), idempotently skipping any SKU the order does not hold. (One ADDED requirement: release reserved stock on cancellation.)

No new capability — the Orders (`shopping-cart` + `order-lifecycle`) and Inventory (`stock-management`) BC shapes are unchanged.

## Impact

- **Orders.** New contract `CritterMart.Contracts/ReleaseStock.cs` (`ReleaseStock`, `ReleaseStockLine`). `Order/OrderCancelled.cs` gains `CancelReason.PaymentDeclined`. `PaymentDecisionHandler.Handle` returns `ReleaseStock?` — decline appends `OrderCancelled` and cascades the release; approve/guard paths return `null` (verified against Wolverine docs: a `null` return suppresses the cascade).
- **Inventory.** New event `Stock/StockReleased.cs` (`StockReleased(string Sku, string OrderId, int Quantity)`). New handler `Features/ReleaseStock.cs` (`ReleaseStockHandler`, mirrors `ReserveStockHandler`). `StockLevelViewProjection` gains `Apply(StockReleased)`.
- **No new packages, no new project, no new broker topology.** `ReleaseStock` rides existing conventional routing — no Orders-local handler, so it routes to the broker; Inventory auto-listens because it handles it.
- **Tests:** Orders tracked-session decline-cancel (declining `IPaymentProvider` → stream `…PaymentAuthFailed + OrderCancelled`, status `cancelled`, one `ReleaseStock` cascaded with the order's lines); Inventory tracked-session release (reservation present → `StockReleased`, view restored), idempotent no-op (duplicate `ReleaseStock` / never-reserved SKU → unchanged), and the delayed-`StockReserved` reordering case; an extended cross-BC smoke proving the decline→release round-trip over the real broker.
- **Downstream artifacts:** `design.md` + `tasks.md` in this consolidated PR; Narrative 004 → v1.4 (Moment 5); prompt + retro `010`.
