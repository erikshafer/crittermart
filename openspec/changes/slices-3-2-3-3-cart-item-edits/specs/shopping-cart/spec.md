# shopping-cart Delta — Slices 3.2 + 3.3 Cart Item Edits

## MODIFIED Requirements

### Requirement: Add an item to the cart

The system SHALL allow the Customer to add an item (a SKU and a quantity) to their cart. When the Customer has no open cart, the system SHALL create a new `Cart` stream keyed by a generated `cartId` and append a `CartCreated` event followed by a `CartItemAdded` event. When the Customer already has an open cart, the system SHALL append a further `CartItemAdded` event to that same cart. The item's name and price SHALL be taken from the product snapshot carried on the command — the cart does not read the Catalog. The system SHALL maintain an inline `CartView` read model whose line items are keyed by SKU: each distinct SKU on the stream appears as exactly one line whose quantity is the sum of that SKU's `CartItemAdded` quantities (less any removals or quantity changes), at the name and price snapshotted by that SKU's first `CartItemAdded`.

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

#### Scenario: Add a SKU that is already in the cart (quantities merge)

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-001", quantity: 2, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-001", quantity: 2 }`
- **AND** the `CartView` for that cart shows a single line for `crit-001` with quantity `3` (the quantities merged)
- **AND** the line's name and price remain those of the first `CartItemAdded`'s snapshot

## ADDED Requirements

### Requirement: Remove an item from the cart

The system SHALL allow the Customer to remove an item (identified by SKU) from their open cart. The system SHALL resolve the Customer's open cart and, when the SKU is present in the cart's `CartView`, append a `CartItemRemoved` event carrying the SKU; the inline `CartView` SHALL no longer show a line for that SKU. When the SKU is not present in the open cart, the system SHALL reject the command with `CartItemNotPresent` and append no event. When the Customer has no open cart, the system SHALL reject the command with `NoOpenCart` and append no event. Removing the last line SHALL leave the cart open and empty; placing an order from an empty cart SHALL be rejected with `CartEmpty`.

#### Scenario: Remove an item that is in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows lines for `crit-001` and `crit-002`
- **WHEN** the Customer issues `RemoveCartItem { customerId: "customer-X", sku: "crit-001" }`
- **THEN** the `Cart` stream appends `CartItemRemoved { sku: "crit-001" }`
- **AND** the `CartView` for that cart shows only the `crit-002` line

#### Scenario: Remove an item that is not in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows only a line for `crit-002`
- **WHEN** the Customer issues `RemoveCartItem { customerId: "customer-X", sku: "crit-001" }`
- **THEN** the command is rejected with `CartItemNotPresent`
- **AND** no event is appended to the `Cart` stream

#### Scenario: Removing the last item leaves an open, empty cart that cannot be checked out

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows a single line for `crit-001`
- **WHEN** the Customer issues `RemoveCartItem { customerId: "customer-X", sku: "crit-001" }` and then `PlaceOrder { customerId: "customer-X" }`
- **THEN** the `Cart` stream appends `CartItemRemoved { sku: "crit-001" }` and the `CartView` shows no lines while remaining open
- **AND** the `PlaceOrder` command is rejected with `CartEmpty` and no Order stream is created

### Requirement: Change a cart item's quantity

The system SHALL allow the Customer to change the quantity of an item (identified by SKU) in their open cart to a new positive quantity. The system SHALL resolve the Customer's open cart and, when the SKU is present in the cart's `CartView`, append a `CartItemQuantityChanged` event carrying the SKU and the new quantity; the inline `CartView` line for that SKU SHALL show the new quantity at its existing snapshotted name and price. When the new quantity is not positive, the system SHALL reject the command and append no event — removing an item is expressed through `RemoveCartItem`, not a zero quantity. When the SKU is not present in the open cart, the system SHALL reject the command with `CartItemNotPresent` and append no event. When the Customer has no open cart, the system SHALL reject the command with `NoOpenCart` and append no event.

#### Scenario: Change the quantity of an item in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows `crit-001` with quantity `1` at price `24.99`
- **WHEN** the Customer issues `ChangeCartItemQuantity { customerId: "customer-X", sku: "crit-001", newQuantity: 3 }`
- **THEN** the `Cart` stream appends `CartItemQuantityChanged { sku: "crit-001", quantity: 3 }`
- **AND** the `CartView` for that cart shows `crit-001` with quantity `3` at the unchanged price `24.99`

#### Scenario: Reject a non-positive quantity

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows `crit-001` with quantity `1`
- **WHEN** the Customer issues `ChangeCartItemQuantity { customerId: "customer-X", sku: "crit-001", newQuantity: 0 }`
- **THEN** the command is rejected (removing an item is `RemoveCartItem`, not a zero quantity)
- **AND** no event is appended to the `Cart` stream

#### Scenario: Reject a quantity change for an item not in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows only a line for `crit-002`
- **WHEN** the Customer issues `ChangeCartItemQuantity { customerId: "customer-X", sku: "crit-001", newQuantity: 3 }`
- **THEN** the command is rejected with `CartItemNotPresent`
- **AND** no event is appended to the `Cart` stream
