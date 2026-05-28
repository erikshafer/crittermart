## ADDED Requirements

### Requirement: Receive stock for a SKU

The system SHALL allow the Operator to record a stock receipt for a SKU. On a receipt the system SHALL append a `StockReceived` event to the SKU's `Stock` stream, creating the stream on the first receipt and appending on subsequent receipts. The system SHALL maintain a `StockLevelView` read model whose available quantity reflects the sum of all receipts, leaving any existing reservations unchanged.

#### Scenario: Receive stock for a new SKU

- **GIVEN** no `Stock` stream exists for SKU `crit-001`
- **WHEN** the Operator issues `ReceiveStock { sku: "crit-001", quantity: 100 }`
- **THEN** the `Stock` stream for `crit-001` records `StockReceived { quantity: 100 }`
- **AND** the `StockLevelView` for `crit-001` shows available `100` and reserved `0`

#### Scenario: Receive additional stock onto an existing SKU

- **GIVEN** the `Stock` stream for `crit-001` already records `StockReceived { quantity: 100 }`
- **WHEN** the Operator issues `ReceiveStock { sku: "crit-001", quantity: 50 }`
- **THEN** the `Stock` stream appends a second `StockReceived { quantity: 50 }`
- **AND** the `StockLevelView` for `crit-001` shows available `150` and reserved `0`
