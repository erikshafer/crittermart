# shopping-cart Specification

## Purpose

The `shopping-cart` capability manages the Customer's pre-checkout cart as an event-sourced `Cart` stream (keyed by a generated `cartId`) projected into an inline `CartView` read model. Slice 3.1 covers adding items (cart creation + line append); slice 4.1 covers checking the cart out on order placement (the terminal `CartCheckedOut`, which flips `IsOpen` to false); later slices add remove-item (3.2), change-quantity (3.3), and inactivity abandonment (3.4). The cart never reads the Catalog — product name and price arrive snapshotted on the command and stay authoritative through checkout. This is one of the Orders bounded context's two capabilities; the other is `order-lifecycle` (the Order aggregate).

## Requirements
### Requirement: Add an item to the cart

The system SHALL allow the Customer to add an item (a SKU and a quantity) to their cart. When the Customer has no open cart, the system SHALL create a new `Cart` stream keyed by a generated `cartId` and append a `CartCreated` event followed by a `CartItemAdded` event. When the Customer already has an open cart, the system SHALL append a further `CartItemAdded` event to that same cart. The item's name and price SHALL be taken from the product snapshot carried on the command — the cart does not read the Catalog. The system SHALL maintain an inline `CartView` read model whose line items reflect the SKU, quantity, and snapshotted name and price of every `CartItemAdded` event on the stream.

#### Scenario: Add the first item, creating a new cart

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **THEN** a new `Cart` stream keyed by a generated `cartId` records `CartCreated { cartId, customerId: "customer-X" }`
- **AND** the same stream appends `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **AND** the `CartView` for that cart shows a single line: `crit-001`, quantity `1`, at the snapshot price `24.99`

#### Scenario: Add a second item to the open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated { cartId, customerId: "customer-X" }` and `CartItemAdded { sku: "crit-001", quantity: 1 }`
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-002", quantity: 3, productSnapshot: { name: "Nebula Newt", price: 18.00 } }`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-002", quantity: 3, snapshot: { name: "Nebula Newt", price: 18.00 } }`
- **AND** no new `Cart` stream is created
- **AND** the `CartView` for that cart shows two lines: `crit-001` quantity `1` at `24.99`, and `crit-002` quantity `3` at `18.00`

### Requirement: Check out the cart on order placement

The system SHALL terminate the Customer's open cart when an order is placed from it. In the same transaction that records `OrderPlaced` on the new Order stream, the system SHALL append a `CartCheckedOut` event carrying the new `orderId` to the cart's stream, and the inline `CartView` SHALL set `IsOpen` to false. A checked-out cart SHALL no longer be resolved as the customer's open cart, so the customer is free to start a new cart, and a repeat placement against the same cart SHALL find no open cart. The checked-out cart's line items SHALL be retained as readable history.

#### Scenario: Placing an order checks out the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream with one or more line items
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** the `Cart` stream appends `CartCheckedOut { orderId }` in the same transaction as `OrderPlaced`
- **AND** the `CartView` for that cart has `IsOpen` set to false while its line items are retained
- **AND** the customer no longer has an open cart

