# stock-management Specification

## Purpose
TBD - created by archiving change slice-2-1-receive-stock. Update Purpose after archive.
## Requirements
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

### Requirement: Reserve stock against an order

The system SHALL reserve a quantity of a SKU's available stock against an order. When sufficient stock is available, the system SHALL append a `StockReserved` event to the SKU's `Stock` stream and update the `StockLevelView` so that available stock decreases and reserved stock increases by the reserved quantity. When the SKU has insufficient (or no) available stock, the system SHALL refuse the reservation and SHALL NOT modify the `Stock` stream.

#### Scenario: Reserve available stock

- **GIVEN** the `Stock` stream for `crit-001` shows available `100` and reserved `0`
- **WHEN** `ReserveStock { orderId: "ord-A", sku: "crit-001", quantity: 2 }` is received
- **THEN** the `Stock` stream appends `StockReserved { orderId: "ord-A", quantity: 2 }`
- **AND** the `StockLevelView` for `crit-001` shows available `98` and reserved `2`

#### Scenario: Refuse a reservation that exceeds available stock

- **GIVEN** the `Stock` stream for `crit-001` shows available `1` and reserved `0`
- **WHEN** `ReserveStock { orderId: "ord-B", sku: "crit-001", quantity: 2 }` is received
- **THEN** the reservation is refused
- **AND** the `Stock` stream is not modified (no `StockReserved` is appended)
- **AND** the `StockLevelView` for `crit-001` still shows available `1` and reserved `0`

