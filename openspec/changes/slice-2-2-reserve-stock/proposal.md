## Why

Slice 2.1 lets Inventory receive stock; slice 2.2 lets stock be **reserved against an order**. This implements the **Inventory-side reservation behavior** — the `StockReserved` event, the `available`/`reserved` projection math, and the insufficient-stock refusal. The cross-bounded-context delivery (Orders sending `ReserveStock` over RabbitMQ and consuming the response) is **slice 4.2**; this slice is triggered via HTTP for now so the behavior is demoable before Orders exists. Narrative 003 v1.1 (Moment 2) is the human sibling.

## What Changes

- Introduce the `ReserveStock` command: reserve a quantity of a SKU's stock against an order.
- On sufficient stock, append `StockReserved` to the SKU's `Stock` stream; the inline `StockLevelView` decrements `available` and increments `reserved` by the reserved quantity.
- On insufficient (or absent) stock, **refuse** the reservation and leave the `Stock` stream **unmodified**.
- Interim trigger: `POST /stock/{sku}/reservations`.

## Capabilities

### New Capabilities

<!-- None. Extends the existing stock-management capability. -->

### Modified Capabilities

- `stock-management`: adds a **Reserve stock** requirement (its second, after Receive stock). `StockReserved` becomes the `Stock` stream's second event kind.

## Impact

- **Event sourcing:** `StockReserved` on the `Stock` stream; the inline `StockLevelViewProjection` gains an `Apply(StockReserved)`. No new document.
- **HTTP surface:** `POST /stock/{sku}/reservations` (interim trigger).
- **Deferred to slice 4.2 (cross-BC, when Orders exists):** RabbitMQ delivery of `ReserveStock` from Orders, the `StockReservationFailed` outbound message, publishing `StockReserved` back to Orders, and the at-least-once duplicate-delivery idempotency guard (Workshop § 6.1's third 2.2 scenario). **Deferred to slice 2.3:** release (`StockReleased`).
- **Downstream artifacts:** `design.md` + `tasks.md` in this same PR (one-PR slice mode).
