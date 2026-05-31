## ADDED Requirements

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
