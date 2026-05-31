## MODIFIED Requirements

### Requirement: Reserve stock against an order

The system SHALL reserve stock for an order in response to a `ReserveStock` message received from the Orders context over the message transport, carrying the order id and one or more lines (each a SKU and quantity). When **every** line has sufficient available stock, the system SHALL append a `StockReserved` event to each line's SKU `Stock` stream **in a single transaction** — all lines reserved together or none at all — update each `StockLevelView` so available stock decreases and reserved stock increases by the reserved quantity, and publish a `StockReserved` message for the order back to the Orders context. When **any** line has insufficient (or no) available stock, the system SHALL refuse the entire reservation, SHALL NOT modify any `Stock` stream, and SHALL publish a `StockReservationFailed` message (carrying the order id and a reason) back to the Orders context.

#### Scenario: Reserve available stock for every line

- **GIVEN** the `Stock` stream for `crit-001` shows available `100` and reserved `0`, and `crit-002` shows available `50` and reserved `0`
- **WHEN** `ReserveStock { orderId: "ord-A", lines: [{ sku: "crit-001", quantity: 2 }, { sku: "crit-002", quantity: 3 }] }` is received
- **THEN** the `crit-001` stream appends `StockReserved { orderId: "ord-A", quantity: 2 }` and the `crit-002` stream appends `StockReserved { orderId: "ord-A", quantity: 3 }` in one transaction
- **AND** the `StockLevelView` shows `crit-001` available `98` reserved `2`, and `crit-002` available `47` reserved `3`
- **AND** a `StockReserved` message for `ord-A` is published back to the Orders context

#### Scenario: Refuse the whole reservation when any line is short

- **GIVEN** the `Stock` stream for `crit-001` shows available `100`, and `crit-002` shows available `1`
- **WHEN** `ReserveStock { orderId: "ord-B", lines: [{ sku: "crit-001", quantity: 2 }, { sku: "crit-002", quantity: 3 }] }` is received
- **THEN** no `Stock` stream is modified (neither `crit-001` nor `crit-002` appends a `StockReserved`)
- **AND** the `StockLevelView` is unchanged for both SKUs
- **AND** a `StockReservationFailed { orderId: "ord-B", reason: "insufficient" }` message is published back to the Orders context

## ADDED Requirements

### Requirement: Reserve stock idempotently under at-least-once delivery

The system SHALL be idempotent when a `ReserveStock` message is delivered more than once for the same order. When a `ReserveStock` arrives for an order that already holds a reservation on the relevant `Stock` streams, the system SHALL NOT append a second `StockReserved` for that order and SHALL NOT change stock levels. The system MAY re-publish the `StockReserved` outcome to the Orders context (Orders is itself idempotent against the duplicate), and SHALL otherwise leave the streams unchanged.

#### Scenario: Duplicate ReserveStock does not double-reserve

- **GIVEN** the `Stock` stream for `crit-001` already records `StockReserved { orderId: "ord-A", quantity: 2 }`
- **WHEN** a second `ReserveStock { orderId: "ord-A", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `Stock` stream is not modified (no second `StockReserved` for `ord-A`)
- **AND** the `StockLevelView` for `crit-001` still shows available `98` and reserved `2`
