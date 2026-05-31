## ADDED Requirements

### Requirement: Authorize payment for a reserved order

The system SHALL authorize payment as soon as an order's stock is reserved, and SHALL record the provider's decision on the order's own stream. When a `StockReserved` Klefter commit is recorded, the system SHALL send a single `AuthorizePayment` request carrying the order id and the order total to a stubbed in-process payment provider. When the provider approves, the system SHALL append a `PaymentAuthorized` event — a Klefter local commit carrying the provider's auth code and the authorized amount (the order total) — to the order's stream. When the provider declines, the system SHALL append a `PaymentAuthFailed` event carrying the decline reason and SHALL NOT confirm the order; the order's visible status SHALL remain `stock_reserved` until the cancellation-on-decline slice (4.6) turns it terminal. The system SHALL be idempotent: the decision is applied only while the order is at the payment gate (`stock_reserved`, payment not yet decided); a decision for an order already authorized, terminal, or unknown SHALL append no further event.

#### Scenario: Authorize payment for a reserved order

- **GIVEN** the order `ord-A` has been placed and its stream shows `OrderPlaced` and `StockReserved` (total `103.98`)
- **WHEN** the stubbed provider approves the `AuthorizePayment { orderId: "ord-A", amount: 103.98 }` request
- **THEN** the `ord-A` stream appends `PaymentAuthorized { orderId: "ord-A", authCode: "stub-…", amount: 103.98 }`

#### Scenario: A declined payment is recorded but does not confirm

- **GIVEN** the order `ord-C` has been placed and its stream shows `OrderPlaced` and `StockReserved`
- **WHEN** the stubbed provider declines the `AuthorizePayment` request
- **THEN** the `ord-C` stream appends `PaymentAuthFailed { orderId: "ord-C", reason: "declined" }`
- **AND** no `OrderConfirmed` event is appended
- **AND** the `OrderStatusView` for `ord-C` still shows status `stock_reserved`

#### Scenario: Ignore a duplicate payment decision

- **GIVEN** the order `ord-A` stream already records `PaymentAuthorized` (or is already terminal)
- **WHEN** a second payment decision arrives for `ord-A`
- **THEN** no further event is appended to the `ord-A` stream

### Requirement: Confirm an order when both gates close

The system SHALL confirm an order once both its stock and payment gates are closed. When `PaymentAuthorized` is recorded for an order that already records `StockReserved`, the system SHALL append an `OrderConfirmed` event to the order's stream, and the inline `OrderStatusView` SHALL show status `confirmed`. `OrderConfirmed` is the terminal success state; CritterMart models no shipping or delivery beyond confirmation (vision.md non-goal). Because payment authorization only begins after stock is reserved, payment is always the second gate to close, so the confirmation is appended together with `PaymentAuthorized` in the same transaction.

#### Scenario: Confirm an order when both gates close

- **GIVEN** the order `ord-A` stream shows `OrderPlaced` and `StockReserved`
- **WHEN** `PaymentAuthorized { orderId: "ord-A" }` is recorded
- **THEN** the `ord-A` stream appends `OrderConfirmed { orderId: "ord-A" }`
- **AND** the `OrderStatusView` for `ord-A` shows status `confirmed`
