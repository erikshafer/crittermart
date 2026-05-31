## ADDED Requirements

### Requirement: Cancel an order when payment is declined

The system SHALL cancel an order when its payment is declined, and SHALL release the stock that was reserved for it. When a `PaymentAuthFailed` is recorded for an order at the payment gate (status `stock_reserved`), the system SHALL append an `OrderCancelled` event carrying reason `payment_declined` to that order's stream — in the same transaction as the `PaymentAuthFailed` — and the inline `OrderStatusView` SHALL show status `cancelled`. Because stock was reserved before the payment gate was reached, the system SHALL send a single `ReleaseStock` message to the Inventory context carrying the order id and the order's lines (each a SKU and quantity, read from the order's own stream). The system SHALL be idempotent: the cancellation is applied only while the order is at the payment gate; when a payment decision arrives for an order already terminal or unknown, the system SHALL append no further event and SHALL send no `ReleaseStock` message.

#### Scenario: Cancel an order whose payment was declined

- **GIVEN** the order `ord-C` has been placed and its stream shows `OrderPlaced`, `StockReserved`, and (just recorded) `PaymentAuthFailed { reason: "declined" }`
- **WHEN** the aggregate decision runs in the same handler that recorded the decline
- **THEN** the `ord-C` stream appends `OrderCancelled { orderId: "ord-C", reason: "payment_declined" }`
- **AND** the `OrderStatusView` for `ord-C` shows status `cancelled`
- **AND** a single `ReleaseStock { orderId: "ord-C", lines: [{ sku, quantity }, …] }` message carrying the order's lines is sent to the Inventory context

#### Scenario: Ignore a duplicate or late payment decision after cancellation

- **GIVEN** the order `ord-C` stream is already terminal (`OrderCancelled { reason: "payment_declined" }`)
- **WHEN** a second declined payment decision arrives for `ord-C`
- **THEN** no further event is appended to the `ord-C` stream
- **AND** no `ReleaseStock` message is sent to the Inventory context
