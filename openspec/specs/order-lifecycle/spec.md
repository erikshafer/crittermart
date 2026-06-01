# order-lifecycle Specification

## Purpose

The `order-lifecycle` capability manages the Order aggregate's event-sourced stream (keyed by a generated `orderId`) from placement to its terminal state, projected into an inline `OrderStatusView` read model. Slice 4.1 covers placing an order from the cart (`OrderPlaced`, status `awaiting_confirmation`); later slices fold cross-BC stock reservation (4.2), stubbed payment authorization (4.3), confirmation when both gates close (4.4), and cancellation on stock failure / payment decline / payment timeout (4.5–4.7) onto the same stream — the Order aggregate acting as its own process manager (Process Manager via Handlers, ADR 007). The terminal state is `OrderConfirmed` or `OrderCancelled`; CritterMart models no shipping or delivery. This is one of the Orders bounded context's two capabilities; the other is `shopping-cart` (the Cart aggregate).
## Requirements
### Requirement: Place an order from the cart

The system SHALL allow the Customer to place an order from their open cart. When the Customer has an open cart with at least one line, the system SHALL create a new `Order` stream keyed by a generated `orderId` and append an `OrderPlaced` event carrying the customer id, the cart's line items (SKU, quantity, and snapshotted name and price), and a total equal to the sum of each line's quantity multiplied by its snapshotted price. The system SHALL maintain an inline `OrderStatusView` read model whose status, line items, and total reflect the `OrderPlaced` event, with status `awaiting_confirmation`. When the Customer has no open cart, the system SHALL reject the command and create no `Order` stream. When the Customer's open cart has no lines, the system SHALL reject the command and create no `Order` stream. The order's lines and total are taken from the cart's snapshot and are authoritative — the order does not read the Catalog.

#### Scenario: Place an order from an open cart

- **GIVEN** the Customer `customer-X` has an open cart with `crit-001` quantity `2` at `24.99` and `crit-002` quantity `3` at `18.00`
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** a new `Order` stream keyed by a generated `orderId` records `OrderPlaced { orderId, customerId: "customer-X", items: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }, { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.00 }], total: 103.98 }`
- **AND** the `OrderStatusView` for that order shows status `awaiting_confirmation`, the two lines, and total `103.98`

#### Scenario: Reject placement when the customer has no open cart

- **GIVEN** the Customer `customer-Y` has no open cart
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-Y" }`
- **THEN** the command is rejected with a `409` response
- **AND** no `Order` stream is created

#### Scenario: Reject a second placement after checkout

- **GIVEN** the Customer `customer-X` has already placed an order from their cart (the cart is checked out and no longer open)
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }` again
- **THEN** the command is rejected with a `409` response
- **AND** no second `Order` stream is created

### Requirement: Reserve stock for a placed order

The system SHALL request stock reservation as soon as an order is placed, and SHALL record a granted reservation on the order's own stream. When an order is placed (`OrderPlaced`), the system SHALL send a single `ReserveStock` message to the Inventory context carrying the order id and the order's lines (each a SKU and quantity). When Inventory confirms the reservation by returning a `StockReserved` for that order, the system SHALL append a `StockReserved` event — a Klefter local commit — to the order's stream, and the inline `OrderStatusView` SHALL show status `stock_reserved`. The system SHALL be idempotent under at-least-once delivery: when a `StockReserved` arrives for an order whose stream is already terminal, or that already records `StockReserved`, the system SHALL append no further event and SHALL leave the stream and view unchanged.

#### Scenario: Placing an order requests stock reservation

- **GIVEN** an order is placed with lines `crit-001` quantity `2` and `crit-002` quantity `3`
- **WHEN** the `OrderPlaced` event is recorded
- **THEN** a single `ReserveStock { orderId, lines: [{ sku: "crit-001", quantity: 2 }, { sku: "crit-002", quantity: 3 }] }` message is sent to the Inventory context

#### Scenario: Record a granted reservation as a Klefter commit

- **GIVEN** the order `ord-A` has been placed and its stream shows `OrderPlaced`
- **WHEN** Inventory returns `StockReserved { orderId: "ord-A" }`
- **THEN** the `ord-A` stream appends `StockReserved { orderId: "ord-A" }`
- **AND** the `OrderStatusView` for `ord-A` shows status `stock_reserved`

#### Scenario: Ignore a duplicate or late StockReserved

- **GIVEN** the order `ord-A` stream already records `StockReserved` (or is already terminal)
- **WHEN** a second `StockReserved { orderId: "ord-A" }` arrives
- **THEN** no further event is appended to the `ord-A` stream
- **AND** the `OrderStatusView` for `ord-A` is unchanged

### Requirement: Cancel an order when stock cannot be reserved

The system SHALL cancel an order when Inventory cannot reserve its stock. When Inventory returns a `StockReservationFailed` for an order, the system SHALL append a `StockReservationFailed` event — a Klefter local commit — and then an `OrderCancelled` event carrying reason `stock_unavailable` to that order's stream, and the inline `OrderStatusView` SHALL show status `cancelled`. The system SHALL send no stock-release message to Inventory, because no reservation was made (the reservation is all-or-nothing, so a refusal reserved nothing). The system SHALL be idempotent: when a `StockReservationFailed` arrives for an order whose stream is already terminal, the system SHALL append no further event.

#### Scenario: Cancel an order whose stock is unavailable

- **GIVEN** the order `ord-B` has been placed and its stream shows `OrderPlaced`
- **WHEN** Inventory returns `StockReservationFailed { orderId: "ord-B", reason: "insufficient" }`
- **THEN** the `ord-B` stream appends `StockReservationFailed { orderId: "ord-B", reason: "insufficient" }` and then `OrderCancelled { orderId: "ord-B", reason: "stock_unavailable" }`
- **AND** the `OrderStatusView` for `ord-B` shows status `cancelled`
- **AND** no stock-release message is sent to Inventory

#### Scenario: Ignore a late StockReservationFailed on a terminal order

- **GIVEN** the order `ord-B` stream is already terminal (`OrderCancelled`)
- **WHEN** a duplicate `StockReservationFailed { orderId: "ord-B" }` arrives
- **THEN** no further event is appended to the `ord-B` stream

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

