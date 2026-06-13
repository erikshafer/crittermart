## ADDED Requirements

### Requirement: Commit reserved stock on order confirmation

The system SHALL commit an order's reserved stock in response to a `CommitStock` message received from the Orders context over the message transport, carrying the order id and one or more lines (each a SKU and quantity). For each line whose SKU `Stock` stream holds a reservation for that order, the system SHALL append a `StockCommitted` event carrying the SKU, order id, and quantity to that stream, and update the `StockLevelView` so reserved stock decreases and committed stock increases by the committed quantity and the order id is removed from the SKU's reservations. The commit is per-SKU independent (not all-or-nothing), mirroring the release path. The system SHALL be idempotent under at-least-once delivery: when a line's SKU stream holds no reservation for that order — because the stock was never reserved, or was already committed by a prior delivery — the system SHALL append no event for that SKU and SHALL leave its stream and view unchanged. After every fold, the invariant `Available + Reserved + Committed = ΣStockReceived` SHALL hold.

#### Scenario: Commit the reservation held for a confirmed order

- **GIVEN** the `Stock` stream for `crit-001` shows available `98`, reserved `2`, and committed `0`, with a reservation held against `ord-A`
- **WHEN** `CommitStock { orderId: "ord-A", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `crit-001` stream appends `StockCommitted { sku: "crit-001", orderId: "ord-A", quantity: 2 }`
- **AND** the `StockLevelView` for `crit-001` shows available `98`, reserved `0`, and committed `2`
- **AND** `ord-A` no longer appears among the SKU's reservations

#### Scenario: Ignore a commit for a SKU holding no reservation for the order

- **GIVEN** the `Stock` stream for `crit-001` holds no reservation against `ord-A` (never reserved, or already committed)
- **WHEN** `CommitStock { orderId: "ord-A", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `crit-001` stream is not modified (no `StockCommitted` for `ord-A`)
- **AND** the `StockLevelView` for `crit-001` is unchanged

#### Scenario: Duplicate CommitStock does not double-commit

- **GIVEN** the `Stock` stream for `crit-001` already records `StockCommitted { orderId: "ord-A" }` and `ord-A` no longer appears in the SKU's reservations
- **WHEN** a second `CommitStock { orderId: "ord-A", lines: [{ sku: "crit-001", quantity: 2 }] }` is received
- **THEN** the `crit-001` stream is not modified (no second `StockCommitted` for `ord-A`)
- **AND** the `StockLevelView` for `crit-001` is unchanged
