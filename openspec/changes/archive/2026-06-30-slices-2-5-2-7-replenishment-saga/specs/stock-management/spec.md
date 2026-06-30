## ADDED Requirements

### Requirement: Open a replenishment saga when a reservation cannot be filled

The system SHALL open a `Replenishment` saga when a `ReserveStock` is refused for insufficient available stock. On the refusal path — whose existing behavior the system SHALL preserve unchanged (no `Stock` stream is modified and a `StockReservationFailed` message is published back to the Orders context) — the system SHALL additionally emit a `BackorderDetected { sku, shortfall }` for each SKU that was short. A `BackorderDetected` SHALL start a `Replenishment` saga keyed by the SKU when none is open, recording the outstanding shortfall, publishing a `RequestRestock { sku, quantity }` supplier-notification message, and scheduling a `ReplenishTimeout { sku }`. When a `Replenishment` saga is already open for the SKU, the system SHALL update its outstanding shortfall to the greater of the current and the new shortfall, and SHALL NOT start a second saga or schedule a second timeout (idempotent under at-least-once redelivery). The saga's state SHALL live in saga storage and SHALL NOT be appended to any `Stock` stream.

#### Scenario: Open a replenishment saga on a shortfall

- **GIVEN** no `Replenishment` saga is open for SKU `crit-001`, whose `Stock` stream shows available `1`
- **WHEN** Inventory handles `ReserveStock { orderId: "ord-B", lines: [{ sku: "crit-001", quantity: 2 }] }` and finds `crit-001` short
- **THEN** a `Replenishment` saga is started keyed by `crit-001` with outstanding `1`
- **AND** a `RequestRestock { sku: "crit-001", quantity: 1 }` message is published and a `ReplenishTimeout { sku: "crit-001" }` is scheduled
- **AND** the `crit-001` `Stock` stream is not modified and a `StockReservationFailed { orderId: "ord-B", reason: "insufficient" }` message is published back to the Orders context

#### Scenario: Re-detected shortfall updates an open saga idempotently

- **GIVEN** a `Replenishment` saga is open for `crit-001` with outstanding `1`
- **WHEN** a second refused `ReserveStock` yields `BackorderDetected { sku: "crit-001", shortfall: 3 }`
- **THEN** the open saga's outstanding becomes `3` (the greater of `1` and `3`)
- **AND** no second `Replenishment` saga is started and no second `ReplenishTimeout` is scheduled

### Requirement: Resolve a replenishment saga when stock is restocked

The system SHALL publish a `RestockArrived { sku, quantity }` message on every stock receipt (slice 2.1), and SHALL route it to the `Replenishment` saga open for that SKU. When the received quantity covers the saga's outstanding shortfall, the saga SHALL complete (`MarkCompleted`, deleting its state). When the received quantity only partially covers the outstanding shortfall, the saga SHALL reduce its outstanding shortfall by the received quantity and SHALL remain open, awaiting a further receipt or its timeout, and SHALL NOT re-issue `RequestRestock`. When no `Replenishment` saga is open for the SKU, the `RestockArrived` SHALL be a silent no-op. A `RestockArrived` SHALL NOT append any event to a `Stock` stream; the receipt's `StockReceived` event (slice 2.1) is unchanged.

#### Scenario: Restock that covers the shortfall completes the saga

- **GIVEN** a `Replenishment` saga is open for `crit-001` with outstanding `1`
- **WHEN** the Operator issues `ReceiveStock { sku: "crit-001", quantity: 100 }`, raising a `RestockArrived { sku: "crit-001", quantity: 100 }`
- **THEN** the saga completes (`MarkCompleted`) and its state is deleted from saga storage
- **AND** the `crit-001` `Stock` stream records `StockReceived { quantity: 100 }` exactly as slice 2.1 specifies (the saga adds no stream event)

#### Scenario: Restock for a SKU with no open saga is a no-op

- **GIVEN** no `Replenishment` saga is open for `crit-002`
- **WHEN** a `RestockArrived { sku: "crit-002", quantity: 50 }` is delivered
- **THEN** no saga is found for `crit-002` and the message is a silent no-op

#### Scenario: Partial restock reduces the shortfall and the saga stays open

- **GIVEN** a `Replenishment` saga is open for `crit-001` with outstanding `10`
- **WHEN** a `RestockArrived { sku: "crit-001", quantity: 4 }` is delivered
- **THEN** the saga reduces its outstanding to `6` and remains open
- **AND** no `RequestRestock` is re-issued

### Requirement: Escalate a replenishment saga that is not replenished in time

The system SHALL enforce a replenishment deadline via the `ReplenishTimeout` scheduled when the saga opens. When a `ReplenishTimeout` is delivered for a SKU whose `Replenishment` saga is still open, the saga SHALL escalate — recording or publishing an operator-facing alert that the SKU went unreplenished — and SHALL complete (`MarkCompleted`, deleting its state). When a `ReplenishTimeout` is delivered for a SKU whose saga has already completed (its shortfall was covered, or a prior timeout already escalated), it SHALL be a silent no-op, since the messaging runtime provides no scheduled-message cancellation.

#### Scenario: Timeout with the shortfall still outstanding escalates and completes

- **GIVEN** a `Replenishment` saga is open for `crit-001` with outstanding `1`
- **WHEN** the scheduled `ReplenishTimeout { sku: "crit-001" }` is delivered
- **THEN** the saga records an operator-facing "unreplenished SKU" escalation for `crit-001`
- **AND** the saga completes (`MarkCompleted`) and its state is deleted

#### Scenario: Timeout after the saga already resolved is a no-op

- **GIVEN** the `Replenishment` saga for `crit-001` already completed (a covering restock resolved it) and was deleted
- **WHEN** the previously-scheduled `ReplenishTimeout { sku: "crit-001" }` is delivered anyway
- **THEN** no saga is found for `crit-001` and the timeout is a silent no-op
