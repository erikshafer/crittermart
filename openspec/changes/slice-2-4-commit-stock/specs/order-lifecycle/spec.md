## MODIFIED Requirements

### Requirement: Confirm an order when both gates close

The system SHALL confirm an order once both its stock and payment gates are closed, and SHALL commit the reserved stock in the Inventory context. When `PaymentAuthorized` is recorded for an order that already records `StockReserved`, the system SHALL append an `OrderConfirmed` event to the order's stream, and the inline `OrderStatusView` SHALL show status `confirmed`. The system SHALL send a single `CommitStock` message to the Inventory context carrying the order id and the order's lines (each a SKU and quantity, read from the order's own stream), so Inventory can convert the reservation into a permanent commitment. `OrderConfirmed` is the terminal success state; CritterMart models no shipping or delivery beyond confirmation (vision.md non-goal). Because payment authorization only begins after stock is reserved, payment is always the second gate to close, so the confirmation is appended together with `PaymentAuthorized` in the same transaction.

#### Scenario: Confirm an order when both gates close

- **GIVEN** the order `ord-A` stream shows `OrderPlaced` and `StockReserved`
- **WHEN** `PaymentAuthorized { orderId: "ord-A" }` is recorded
- **THEN** the `ord-A` stream appends `OrderConfirmed { orderId: "ord-A" }`
- **AND** the `OrderStatusView` for `ord-A` shows status `confirmed`
- **AND** a single `CommitStock { orderId: "ord-A", lines: [{ sku, quantity }, …] }` message carrying the order's lines is sent to the Inventory context
