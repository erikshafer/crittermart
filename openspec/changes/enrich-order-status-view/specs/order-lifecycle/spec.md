# order-lifecycle Delta — Enrich OrderStatusView (placedAt + cancellation reason)

## ADDED Requirements

### Requirement: Surface placement time and cancellation reason in the order view

The system SHALL surface, in the inline `OrderStatusView` read model, the time the order was placed and — once the order is cancelled — the reason it was cancelled, in addition to the order's status, line items, and total. The view SHALL carry a `placedAt` timestamp set at genesis to the append time of the order's `OrderPlaced` event (event metadata, not a new event field), present for every order from the moment it is placed. The view SHALL carry a `cancelReason` field that is null while the order has not been cancelled and, once an `OrderCancelled` event is recorded on the stream, carries that event's reason — one of `stock_unavailable`, `payment_declined`, or `payment_timeout`. This enrichment SHALL preserve the existing `OrderStatusView` wire shape `{ id, customerId, status, lines, total }` as a superset: `placedAt` and `cancelReason` are added alongside the existing fields, none of which is removed or renamed, so existing consumers (the W3 place-order read and the W4 tracking screen) are unaffected. The enrichment appends no event, sends no message, and reads no stream other than the order's own.

#### Scenario: A placed order's view carries its placement time

- **GIVEN** the Customer places an order `ord-A` and `OrderPlaced { orderId: "ord-A" }` is appended to its stream
- **WHEN** the `OrderStatusView` for `ord-A` is read
- **THEN** its `placedAt` equals the append time of the `OrderPlaced` event
- **AND** its `cancelReason` is null (the order is not cancelled)
- **AND** its `status`, line items, and `total` are unchanged from the existing place-order behavior

#### Scenario: A cancelled order's view carries the cancellation reason

- **GIVEN** the order `ord-B` stream shows `OrderPlaced` and then `OrderCancelled { orderId: "ord-B", reason: "stock_unavailable" }`
- **WHEN** the `OrderStatusView` for `ord-B` is read
- **THEN** its `status` is `cancelled`
- **AND** its `cancelReason` is `stock_unavailable`
- **AND** its `placedAt` still equals the append time of the `OrderPlaced` event

#### Scenario: Each cancellation route is surfaced by its own reason

- **GIVEN** three orders cancelled by the three routes — `OrderCancelled { reason: "stock_unavailable" }` (stock failure, slice 4.5), `OrderCancelled { reason: "payment_declined" }` (payment decline, slice 4.6), and `OrderCancelled { reason: "payment_timeout" }` (payment timeout, slice 4.7)
- **WHEN** each order's `OrderStatusView` is read
- **THEN** each view's `cancelReason` carries the reason of its own `OrderCancelled` event — `stock_unavailable`, `payment_declined`, and `payment_timeout` respectively

#### Scenario: An active order's view carries a null cancel reason

- **GIVEN** the order `ord-A` stream shows `OrderPlaced`, `StockReserved`, `PaymentAuthorized`, and `OrderConfirmed` (a confirmed, never-cancelled order)
- **WHEN** the `OrderStatusView` for `ord-A` is read
- **THEN** its `status` is `confirmed`
- **AND** its `cancelReason` is null — only a cancellation sets it
