## ADDED Requirements

### Requirement: Cancel an order on payment timeout

The system SHALL set a payment deadline for every placed order, and SHALL cancel an order that has not reached a terminal state when that deadline passes. When an order is placed (`OrderPlaced`), the system SHALL schedule an `OrderPaymentTimeout` self-message to be delivered after a configurable duration (`Orders:PaymentTimeout`, default 10 minutes). When the `OrderPaymentTimeout` fires for an order whose stream is **not** terminal, the system SHALL append an `OrderCancelled` event carrying reason `payment_timeout` to that order's stream, the inline `OrderStatusView` SHALL show status `cancelled`, and the system SHALL send a single `ReleaseStock` message to the Inventory context carrying the order id and the order's lines — **regardless** of whether the order's own stream records a `StockReserved` grant, because the grant reply may have been lost in transit while Inventory holds the reservation; the Inventory context's per-SKU reservation guard makes the release a no-op wherever nothing is actually held. When the `OrderPaymentTimeout` fires for an order whose stream is terminal (`OrderConfirmed` or `OrderCancelled`), the system SHALL append no event and SHALL send no message — losing the race to a settled order is the timer's normal fate. The system SHALL be idempotent under duplicate delivery of the timeout message.

#### Scenario: Placing an order schedules a payment deadline

- **GIVEN** the Customer `customer-X` has an open cart with at least one line
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }` and `OrderPlaced` is recorded for the new order `ord-T`
- **THEN** an `OrderPaymentTimeout { orderId: "ord-T" }` self-message is scheduled for delivery after the configured payment-timeout duration

#### Scenario: Cancel an order stuck at the payment gate when the deadline passes

- **GIVEN** the order `ord-T` stream shows `OrderPlaced` and `StockReserved`, and no `PaymentAuthorized` has been recorded
- **WHEN** the scheduled `OrderPaymentTimeout { orderId: "ord-T" }` fires
- **THEN** the `ord-T` stream appends `OrderCancelled { orderId: "ord-T", reason: "payment_timeout" }`
- **AND** the `OrderStatusView` for `ord-T` shows status `cancelled`
- **AND** a single `ReleaseStock { orderId: "ord-T", lines: [{ sku, quantity }, …] }` message carrying the order's lines is sent to the Inventory context

#### Scenario: Cancel an order that never heard back from Inventory

- **GIVEN** the order `ord-U` stream shows only `OrderPlaced` (status `awaiting_confirmation` — Inventory's reservation reply never arrived)
- **WHEN** the scheduled `OrderPaymentTimeout { orderId: "ord-U" }` fires
- **THEN** the `ord-U` stream appends `OrderCancelled { orderId: "ord-U", reason: "payment_timeout" }`
- **AND** the `OrderStatusView` for `ord-U` shows status `cancelled`
- **AND** a single `ReleaseStock { orderId: "ord-U", lines: [{ sku, quantity }, …] }` message is still sent to the Inventory context, so a reservation Inventory granted but Orders never learned of is released rather than leaked

#### Scenario: The timeout is a no-op on a confirmed order

- **GIVEN** the order `ord-A` stream is terminal (`OrderPlaced`, `StockReserved`, `PaymentAuthorized`, `OrderConfirmed`)
- **WHEN** the scheduled `OrderPaymentTimeout { orderId: "ord-A" }` fires after the confirmation
- **THEN** no further event is appended to the `ord-A` stream
- **AND** no `ReleaseStock` message is sent to the Inventory context

#### Scenario: Duplicate timeout delivery is a no-op

- **GIVEN** the order `ord-T` stream is already terminal (`OrderCancelled { reason: "payment_timeout" }`)
- **WHEN** a duplicate `OrderPaymentTimeout { orderId: "ord-T" }` arrives
- **THEN** no further event is appended to the `ord-T` stream
- **AND** no `ReleaseStock` message is sent to the Inventory context

### Requirement: Track orders awaiting payment

The system SHALL maintain an inline `OrdersAwaitingPayment` read model — the todo-list of the payment-deadline automation — holding one row per order that has not yet reached a terminal state. When `OrderPlaced` is recorded, the system SHALL create a row carrying the order id, customer id, order total, and the payment deadline. When a terminal event (`OrderConfirmed` or `OrderCancelled`, any reason) is recorded for an order, the system SHALL delete that order's row. The read model SHALL be queryable, listing every order currently awaiting its terminal state. The timeout handler SHALL NOT depend on this read model for its cancellation decision — the order's own stream is the single source of truth; the read model is the observable face of the automation.

#### Scenario: A placed order appears in the awaiting-payment list

- **GIVEN** the Customer places an order `ord-T` with total `103.98`
- **WHEN** the `OrderPlaced` event is recorded
- **THEN** the `OrdersAwaitingPayment` read model holds a row for `ord-T` carrying the customer id, total `103.98`, and the payment deadline

#### Scenario: A confirmed order's row is removed

- **GIVEN** the `OrdersAwaitingPayment` read model holds a row for `ord-A`
- **WHEN** `OrderConfirmed { orderId: "ord-A" }` is recorded
- **THEN** the `OrdersAwaitingPayment` read model no longer holds a row for `ord-A`

#### Scenario: A cancelled order's row is removed

- **GIVEN** the `OrdersAwaitingPayment` read model holds a row for `ord-T`
- **WHEN** `OrderCancelled { orderId: "ord-T" }` is recorded — by timeout, payment decline, or stock failure
- **THEN** the `OrdersAwaitingPayment` read model no longer holds a row for `ord-T`
