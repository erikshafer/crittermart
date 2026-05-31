## ADDED Requirements

### Requirement: Release reserved stock on cancellation

The system SHALL release an order's reserved stock in response to a `ReleaseStock` message received from the Orders context over the message transport, carrying the order id and one or more lines (each a SKU and quantity). For each line whose SKU `Stock` stream holds a reservation for that order, the system SHALL append a `StockReleased` event carrying the SKU, order id, and quantity to that stream, and update the `StockLevelView` so available stock increases and reserved stock decreases by the released quantity and the order id is removed from the SKU's reservations. The release is per-SKU independent (not all-or-nothing). The system SHALL be idempotent under at-least-once delivery and reordering: when a line's SKU stream holds no reservation for that order — because the reservation was never granted, or was already released by a prior delivery — the system SHALL append no event for that SKU and SHALL leave its stream and view unchanged.

#### Scenario: Release the reservation held for a cancelled order

- **GIVEN** the `Stock` stream for `crit-001` shows available `98` and reserved `2`, with a reservation held against `ord-C`
- **WHEN** `ReleaseStock { orderId: "ord-C", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `crit-001` stream appends `StockReleased { sku: "crit-001", orderId: "ord-C", quantity: 2 }`
- **AND** the `StockLevelView` for `crit-001` shows available `100` and reserved `0`
- **AND** `ord-C` no longer appears among the SKU's reservations

#### Scenario: Ignore a release for a SKU holding no reservation for the order

- **GIVEN** the `Stock` stream for `crit-001` holds no reservation against `ord-C` (never granted, or already released)
- **WHEN** `ReleaseStock { orderId: "ord-C", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `crit-001` stream is not modified (no `StockReleased` for `ord-C`)
- **AND** the `StockLevelView` for `crit-001` is unchanged

#### Scenario: A delayed grant does not break release correctness

- **GIVEN** the `Stock` stream for `crit-001` shows a reservation held against `ord-C` (the grant landed, though its reply to Orders was delayed crossing the broker)
- **WHEN** `ReleaseStock { orderId: "ord-C", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `crit-001` stream appends `StockReleased { sku: "crit-001", orderId: "ord-C", quantity: 2 }` and the reservation is released
- **AND** the release is correct regardless of the order in which the grant reply and the cancellation crossed the broker
