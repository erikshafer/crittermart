# shopping-cart Delta — Harmonize cart command identity onto the X-Customer-Id header

## MODIFIED Requirements

### Requirement: Add an item to the cart

The system SHALL allow the Customer to add an item (a SKU and a quantity) to their cart. When the Customer has no open cart, the system SHALL create a new `Cart` stream keyed by a generated `cartId` and append a `CartCreated` event followed by a `CartItemAdded` event. When the Customer already has an open cart, the system SHALL append a further `CartItemAdded` event to that same cart. The item's name and price SHALL be taken from the product snapshot carried on the command — the cart does not read the Catalog. The system SHALL maintain an inline `CartView` read model whose line items are keyed by SKU: each distinct SKU on the stream appears as exactly one line whose quantity is the sum of that SKU's `CartItemAdded` quantities (less any removals or quantity changes), at the name and price snapshotted by that SKU's first `CartItemAdded`.

The Customer's identity SHALL arrive ambiently on the request via the `X-Customer-Id` header — the round-one stubbed customer id behind the ADR 009 `useCurrentCustomer` seam (the stand-in for an authenticated claim) — not in the route or the request body. The command is addressed at `POST /carts/mine/items`, the same identity transport the cart read (`GET /carts/mine`) uses. When no customer identity is supplied (a missing or blank `X-Customer-Id` header), the system SHALL reject the command with `400` and append no event, before any cart is resolved or created.

When a new cart is created, the system SHALL also schedule a `CartActivityTimeout` self-message for the configured inactivity window (`Orders:CartActivityTimeout`, default 2 hours), durably persisted so the deadline survives a service restart. Adding to an existing open cart SHALL NOT schedule a further timeout — under the fire-and-check policy, one scheduled timeout per cart suffices, and the fired timeout reads the cart's activity from its own event timestamps.

#### Scenario: Add the first item, creating a new cart

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }` at `POST /carts/mine/items` with header `X-Customer-Id: customer-X`
- **THEN** a new `Cart` stream keyed by a generated `cartId` records `CartCreated { cartId, customerId: "customer-X" }`
- **AND** the same stream appends `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **AND** the `CartView` for that cart shows a single line: `crit-001`, quantity `1`, at the snapshot price `24.99`

#### Scenario: Creating a cart schedules the inactivity timeout

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { ... } }` at `POST /carts/mine/items` with header `X-Customer-Id: customer-X`
- **THEN** a new `Cart` stream is created as above
- **AND** a `CartActivityTimeout` self-message carrying the new `cartId` is scheduled for the configured inactivity window

#### Scenario: Add a second item to the open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated { cartId, customerId: "customer-X" }` and `CartItemAdded { sku: "crit-001", quantity: 1 }`
- **WHEN** the Customer issues `AddToCart { sku: "crit-002", quantity: 3, productSnapshot: { name: "Nebula Newt", price: 18.00 } }` at `POST /carts/mine/items` with header `X-Customer-Id: customer-X`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-002", quantity: 3, snapshot: { name: "Nebula Newt", price: 18.00 } }`
- **AND** no new `Cart` stream is created
- **AND** no further `CartActivityTimeout` is scheduled
- **AND** the `CartView` for that cart shows two lines: `crit-001` quantity `1` at `24.99`, and `crit-002` quantity `3` at `18.00`

#### Scenario: Add a SKU that is already in the cart (quantities merge)

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 2, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }` at `POST /carts/mine/items` with header `X-Customer-Id: customer-X`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-001", quantity: 2 }`
- **AND** the `CartView` for that cart shows a single line for `crit-001` with quantity `3` (the quantities merged)
- **AND** the line's name and price remain those of the first `CartItemAdded`'s snapshot

#### Scenario: Reject an add with no customer identity

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront issues `AddToCart` at `POST /carts/mine/items` with a missing or blank `X-Customer-Id` header (and an otherwise valid product snapshot)
- **THEN** the command is rejected with `400` — the request carries no identity against which to resolve or create a cart
- **AND** no `Cart` stream is created and no event is appended

### Requirement: Remove an item from the cart

The system SHALL allow the Customer to remove an item (identified by SKU) from their open cart. The system SHALL resolve the Customer's open cart and, when the SKU is present in the cart's `CartView`, append a `CartItemRemoved` event carrying the SKU; the inline `CartView` SHALL no longer show a line for that SKU. When the SKU is not present in the open cart, the system SHALL reject the command with `CartItemNotPresent` and append no event. When the Customer has no open cart, the system SHALL reject the command with `NoOpenCart` and append no event. Removing the last line SHALL leave the cart open and empty; placing an order from an empty cart SHALL be rejected with `CartEmpty`.

The Customer's identity SHALL arrive ambiently on the request via the `X-Customer-Id` header (the ADR 009 `useCurrentCustomer` seam, the stand-in for an authenticated claim), not in the route or the request body; the SKU being removed rides the route. The command is addressed at `DELETE /carts/mine/items/{sku}`, the same identity transport the cart read uses. When no customer identity is supplied (a missing or blank `X-Customer-Id` header), the system SHALL reject the command with `400` and append no event, before the open cart is resolved — distinct from the `409` that means a well-formed command does not fit the cart's state (`NoOpenCart`, `CartItemNotPresent`).

#### Scenario: Remove an item that is in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows lines for `crit-001` and `crit-002`
- **WHEN** the Customer issues `RemoveCartItem { sku: "crit-001" }` at `DELETE /carts/mine/items/crit-001` with header `X-Customer-Id: customer-X`
- **THEN** the `Cart` stream appends `CartItemRemoved { sku: "crit-001" }`
- **AND** the `CartView` for that cart shows only the `crit-002` line

#### Scenario: Remove an item that is not in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows only a line for `crit-002`
- **WHEN** the Customer issues `RemoveCartItem { sku: "crit-001" }` at `DELETE /carts/mine/items/crit-001` with header `X-Customer-Id: customer-X`
- **THEN** the command is rejected with `CartItemNotPresent`
- **AND** no event is appended to the `Cart` stream

#### Scenario: Removing the last item leaves an open, empty cart that cannot be checked out

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows a single line for `crit-001`
- **WHEN** the Customer issues `RemoveCartItem { sku: "crit-001" }` (`DELETE /carts/mine/items/crit-001`, header `X-Customer-Id: customer-X`) and then `PlaceOrder { customerId: "customer-X" }`
- **THEN** the `Cart` stream appends `CartItemRemoved { sku: "crit-001" }` and the `CartView` shows no lines while remaining open
- **AND** the `PlaceOrder` command is rejected with `CartEmpty` and no Order stream is created

#### Scenario: Reject a remove with no customer identity

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront issues `RemoveCartItem` at `DELETE /carts/mine/items/crit-001` with a missing or blank `X-Customer-Id` header
- **THEN** the command is rejected with `400` — the request carries no identity against which to resolve a cart
- **AND** no event is appended to any `Cart` stream

### Requirement: Change a cart item's quantity

The system SHALL allow the Customer to change the quantity of an item (identified by SKU) in their open cart to a new positive quantity. The system SHALL resolve the Customer's open cart and, when the SKU is present in the cart's `CartView`, append a `CartItemQuantityChanged` event carrying the SKU and the new quantity; the inline `CartView` line for that SKU SHALL show the new quantity at its existing snapshotted name and price. When the new quantity is not positive, the system SHALL reject the command and append no event — removing an item is expressed through `RemoveCartItem`, not a zero quantity. When the SKU is not present in the open cart, the system SHALL reject the command with `CartItemNotPresent` and append no event. When the Customer has no open cart, the system SHALL reject the command with `NoOpenCart` and append no event.

The Customer's identity SHALL arrive ambiently on the request via the `X-Customer-Id` header (the ADR 009 `useCurrentCustomer` seam, the stand-in for an authenticated claim), not in the route or the request body; the SKU being changed rides the route and the new absolute quantity rides the body. The command is addressed at `POST /carts/mine/items/{sku}/quantity`, the same identity transport the cart read uses. When no customer identity is supplied (a missing or blank `X-Customer-Id` header), the system SHALL reject the command with `400` and append no event, before the quantity guard or open-cart resolution runs.

#### Scenario: Change the quantity of an item in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows `crit-001` with quantity `1` at price `24.99`
- **WHEN** the Customer issues `ChangeCartItemQuantity { newQuantity: 3 }` at `POST /carts/mine/items/crit-001/quantity` with header `X-Customer-Id: customer-X`
- **THEN** the `Cart` stream appends `CartItemQuantityChanged { sku: "crit-001", quantity: 3 }`
- **AND** the `CartView` for that cart shows `crit-001` with quantity `3` at the unchanged price `24.99`

#### Scenario: Reject a non-positive quantity

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows `crit-001` with quantity `1`
- **WHEN** the Customer issues `ChangeCartItemQuantity { newQuantity: 0 }` at `POST /carts/mine/items/crit-001/quantity` with header `X-Customer-Id: customer-X`
- **THEN** the command is rejected (removing an item is `RemoveCartItem`, not a zero quantity)
- **AND** no event is appended to the `Cart` stream

#### Scenario: Reject a quantity change for an item not in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows only a line for `crit-002`
- **WHEN** the Customer issues `ChangeCartItemQuantity { newQuantity: 3 }` at `POST /carts/mine/items/crit-001/quantity` with header `X-Customer-Id: customer-X`
- **THEN** the command is rejected with `CartItemNotPresent`
- **AND** no event is appended to the `Cart` stream

#### Scenario: Reject a quantity change with no customer identity

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront issues `ChangeCartItemQuantity { newQuantity: 3 }` at `POST /carts/mine/items/crit-001/quantity` with a missing or blank `X-Customer-Id` header
- **THEN** the command is rejected with `400` — the request carries no identity against which to resolve a cart
- **AND** no event is appended to any `Cart` stream
