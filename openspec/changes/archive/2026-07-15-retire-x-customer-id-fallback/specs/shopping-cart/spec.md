# shopping-cart — delta: retire the X-Customer-Id fallback (identity from `sub` only)

## MODIFIED Requirements

### Requirement: Add an item to the cart

The system SHALL allow the Customer to add an item (a SKU and a quantity) to their cart. When the Customer has no open cart, the system SHALL create a new `Cart` stream keyed by a generated `cartId` and append a `CartCreated` event followed by a `CartItemAdded` event. When the Customer already has an open cart, the system SHALL append a further `CartItemAdded` event to that same cart. The item's name and price SHALL be taken from the product snapshot carried on the command — the cart does not read the Catalog. The system SHALL maintain an inline `CartView` read model whose line items are keyed by SKU: each distinct SKU on the stream appears as exactly one line whose quantity is the sum of that SKU's `CartItemAdded` quantities (less any removals or quantity changes), at the name and price snapshotted by that SKU's first `CartItemAdded`.

The Customer's identity SHALL be the `sub` claim of a validated JWT presented as `Authorization: Bearer` (ADR 023 hard cutover — the authenticated claim the ADR 009 `useCurrentCustomer` seam stood in for; the transitional `X-Customer-Id` header is retired) — not in the route, the request body, or any request header. The command is addressed at `POST /carts/mine/items`, the same identity transport the cart read (`GET /carts/mine`) uses. When the request carries no valid token — missing, tampered, wrong-issuer/audience, or expired — the system SHALL reject the command with `401 Unauthorized` and append no event, before any cart is resolved or created (the endpoint requires authorization, so rejection precedes the handler).

When a new cart is created, the system SHALL also schedule a `CartActivityTimeout` self-message for the configured inactivity window (`Orders:CartActivityTimeout`, default 2 hours), durably persisted so the deadline survives a service restart. Adding to an existing open cart SHALL NOT schedule a further timeout — under the fire-and-check policy, one scheduled timeout per cart suffices, and the fired timeout reads the cart's activity from its own event timestamps.

#### Scenario: Add the first item, creating a new cart

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }` at `POST /carts/mine/items`, authenticated as `customer-X` (a valid Bearer token whose `sub` is `customer-X`)
- **THEN** a new `Cart` stream keyed by a generated `cartId` records `CartCreated { cartId, customerId: "customer-X" }`
- **AND** the same stream appends `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **AND** the `CartView` for that cart shows a single line: `crit-001`, quantity `1`, at the snapshot price `24.99`

#### Scenario: Creating a cart schedules the inactivity timeout

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { ... } }` at `POST /carts/mine/items`, authenticated as `customer-X`
- **THEN** a new `Cart` stream is created as above
- **AND** a `CartActivityTimeout` self-message carrying the new `cartId` is scheduled for the configured inactivity window

#### Scenario: Add a second item to the open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated { cartId, customerId: "customer-X" }` and `CartItemAdded { sku: "crit-001", quantity: 1 }`
- **WHEN** the Customer issues `AddToCart { sku: "crit-002", quantity: 3, productSnapshot: { name: "Nebula Newt", price: 18.00 } }` at `POST /carts/mine/items`, authenticated as `customer-X`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-002", quantity: 3, snapshot: { name: "Nebula Newt", price: 18.00 } }`
- **AND** no new `Cart` stream is created
- **AND** no further `CartActivityTimeout` is scheduled
- **AND** the `CartView` for that cart shows two lines: `crit-001` quantity `1` at `24.99`, and `crit-002` quantity `3` at `18.00`

#### Scenario: Add a SKU that is already in the cart (quantities merge)

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 2, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }` at `POST /carts/mine/items`, authenticated as `customer-X`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-001", quantity: 2 }`
- **AND** the `CartView` for that cart shows a single line for `crit-001` with quantity `3` (the quantities merged)
- **AND** the line's name and price remain those of the first `CartItemAdded`'s snapshot

#### Scenario: Reject an unauthenticated add

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront issues `AddToCart` at `POST /carts/mine/items` with no Bearer token (and an otherwise valid product snapshot), or with an invalid or expired one
- **THEN** the command is rejected with `401 Unauthorized` — the request carries no authenticated identity against which to resolve or create a cart
- **AND** no `Cart` stream is created and no event is appended
- **AND** presenting the retired `X-Customer-Id` header does not change the outcome — the header no longer names a customer

### Requirement: Remove an item from the cart

The system SHALL allow the Customer to remove an item (identified by SKU) from their open cart. The system SHALL resolve the Customer's open cart and, when the SKU is present in the cart's `CartView`, append a `CartItemRemoved` event carrying the SKU; the inline `CartView` SHALL no longer show a line for that SKU. When the SKU is not present in the open cart, the system SHALL reject the command with `CartItemNotPresent` and append no event. When the Customer has no open cart, the system SHALL reject the command with `NoOpenCart` and append no event. Removing the last line SHALL leave the cart open and empty; placing an order from an empty cart SHALL be rejected with `CartEmpty`.

The Customer's identity SHALL be the `sub` claim of a validated JWT presented as `Authorization: Bearer` (ADR 023 hard cutover — the authenticated claim the ADR 009 seam stood in for; the transitional `X-Customer-Id` header is retired) — not in the route, the request body, or any request header; the SKU being removed rides the route. The command is addressed at `DELETE /carts/mine/items/{sku}`, the same identity transport the cart read uses. When the request carries no valid token, the system SHALL reject the command with `401 Unauthorized` and append no event, before the open cart is resolved — distinct from the `409` that means a well-formed command does not fit the cart's state (`NoOpenCart`, `CartItemNotPresent`).

#### Scenario: Remove an item that is in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows lines for `crit-001` and `crit-002`
- **WHEN** the Customer issues `RemoveCartItem { sku: "crit-001" }` at `DELETE /carts/mine/items/crit-001`, authenticated as `customer-X`
- **THEN** the `Cart` stream appends `CartItemRemoved { sku: "crit-001" }`
- **AND** the `CartView` for that cart shows only the `crit-002` line

#### Scenario: Remove an item that is not in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows only a line for `crit-002`
- **WHEN** the Customer issues `RemoveCartItem { sku: "crit-001" }` at `DELETE /carts/mine/items/crit-001`, authenticated as `customer-X`
- **THEN** the command is rejected with `CartItemNotPresent`
- **AND** no event is appended to the `Cart` stream

#### Scenario: Removing the last item leaves an open, empty cart that cannot be checked out

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows a single line for `crit-001`
- **WHEN** the Customer issues `RemoveCartItem { sku: "crit-001" }` (`DELETE /carts/mine/items/crit-001`, authenticated as `customer-X`) and then `PlaceOrder { customerId: "customer-X" }`
- **THEN** the `Cart` stream appends `CartItemRemoved { sku: "crit-001" }` and the `CartView` shows no lines while remaining open
- **AND** the `PlaceOrder` command is rejected with `CartEmpty` and no Order stream is created

#### Scenario: Reject an unauthenticated remove

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront issues `RemoveCartItem` at `DELETE /carts/mine/items/crit-001` with no Bearer token, or with an invalid or expired one
- **THEN** the command is rejected with `401 Unauthorized` — the request carries no authenticated identity against which to resolve a cart
- **AND** no event is appended to any `Cart` stream

### Requirement: Change a cart item's quantity

The system SHALL allow the Customer to change the quantity of an item (identified by SKU) in their open cart to a new positive quantity. The system SHALL resolve the Customer's open cart and, when the SKU is present in the cart's `CartView`, append a `CartItemQuantityChanged` event carrying the SKU and the new quantity; the inline `CartView` line for that SKU SHALL show the new quantity at its existing snapshotted name and price. When the new quantity is not positive, the system SHALL reject the command and append no event — removing an item is expressed through `RemoveCartItem`, not a zero quantity. When the SKU is not present in the open cart, the system SHALL reject the command with `CartItemNotPresent` and append no event. When the Customer has no open cart, the system SHALL reject the command with `NoOpenCart` and append no event.

The Customer's identity SHALL be the `sub` claim of a validated JWT presented as `Authorization: Bearer` (ADR 023 hard cutover — the authenticated claim the ADR 009 seam stood in for; the transitional `X-Customer-Id` header is retired) — not in the route, the request body, or any request header; the SKU being changed rides the route and the new absolute quantity rides the body. The command is addressed at `POST /carts/mine/items/{sku}/quantity`, the same identity transport the cart read uses. When the request carries no valid token, the system SHALL reject the command with `401 Unauthorized` and append no event, before the quantity guard or open-cart resolution runs.

#### Scenario: Change the quantity of an item in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows `crit-001` with quantity `1` at price `24.99`
- **WHEN** the Customer issues `ChangeCartItemQuantity { newQuantity: 3 }` at `POST /carts/mine/items/crit-001/quantity`, authenticated as `customer-X`
- **THEN** the `Cart` stream appends `CartItemQuantityChanged { sku: "crit-001", quantity: 3 }`
- **AND** the `CartView` for that cart shows `crit-001` with quantity `3` at the unchanged price `24.99`

#### Scenario: Reject a non-positive quantity

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows `crit-001` with quantity `1`
- **WHEN** the Customer issues `ChangeCartItemQuantity { newQuantity: 0 }` at `POST /carts/mine/items/crit-001/quantity`, authenticated as `customer-X`
- **THEN** the command is rejected (removing an item is `RemoveCartItem`, not a zero quantity)
- **AND** no event is appended to the `Cart` stream

#### Scenario: Reject a quantity change for an item not in the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose `CartView` shows only a line for `crit-002`
- **WHEN** the Customer issues `ChangeCartItemQuantity { newQuantity: 3 }` at `POST /carts/mine/items/crit-001/quantity`, authenticated as `customer-X`
- **THEN** the command is rejected with `CartItemNotPresent`
- **AND** no event is appended to the `Cart` stream

#### Scenario: Reject an unauthenticated quantity change

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront issues `ChangeCartItemQuantity { newQuantity: 3 }` at `POST /carts/mine/items/crit-001/quantity` with no Bearer token, or with an invalid or expired one
- **THEN** the command is rejected with `401 Unauthorized` — the request carries no authenticated identity against which to resolve a cart
- **AND** no event is appended to any `Cart` stream

### Requirement: Read the Customer's open cart

The system SHALL expose the Customer's single open cart as a read over the existing `CartView` read model, resolved by the Customer's identity rather than by `cartId`. The Customer's identity SHALL be the `sub` claim of a validated JWT presented as `Authorization: Bearer` (ADR 023 hard cutover — the authenticated claim the ADR 009 `useCurrentCustomer` seam stood in for; the transitional `X-Customer-Id` header is retired) — not in the route or the request body.

- When the identity resolves to a Customer who has exactly one open `CartView` (the partial-unique open-cart index guarantees at most one), the system SHALL return that `CartView` with its SKU-keyed line items at their snapshotted names and prices.
- When the identity resolves to a Customer who has no open cart — none was ever created, or the most recent cart is `CartCheckedOut` (placed an order) or `CartAbandoned` (inactivity) — the system SHALL respond `404`. This is a "no open cart" signal the storefront renders as an empty cart, not an error condition.
- When the request carries no valid token — missing, tampered, wrong-issuer/audience, or expired — the system SHALL reject the request with `401 Unauthorized`, kept distinct from the `404` that means "no open cart".

This read appends no event and reads no read model other than `CartView`. It is the customer-keyed read counterpart to the customer-keyed *write* side every cart command already uses (slices 3.1–3.3 resolve the open cart by customer; this exposes the same resolution as a read), closing the pre-frontend audit's blocking Gap #1: without it the cart-review screen (wireframe W2) cannot render on a cold load, when the storefront holds only the authenticated customer and no `cartId`.

#### Scenario: Return the Customer's open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated`, `CartItemAdded { sku: "crit-001" }`, and `CartItemAdded { sku: "crit-002" }` (no `CartCheckedOut` or `CartAbandoned`)
- **WHEN** the storefront requests `GET /carts/mine`, authenticated as `customer-X` (a valid Bearer token whose `sub` is `customer-X`)
- **THEN** the single open `CartView` for `customer-X` is returned, with two SKU-keyed lines (`crit-001`, `crit-002`) at their snapshot prices
- **AND** no event is appended to the `Cart` stream

#### Scenario: A Customer with no open cart gets a 404

- **GIVEN** the Customer `customer-X` has no open cart — either none was ever created, or the most recent cart is `CartCheckedOut` or `CartAbandoned`
- **WHEN** the storefront requests `GET /carts/mine`, authenticated as `customer-X`
- **THEN** the response is `404` ("no open cart"), not an error
- **AND** no event is appended to the `Cart` stream

#### Scenario: An unauthenticated request is rejected

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront requests `GET /carts/mine` with no Bearer token, or with an invalid or expired one
- **THEN** the response is `401 Unauthorized` — the request carries no authenticated identity to resolve a cart
- **AND** no event is appended to any `Cart` stream
