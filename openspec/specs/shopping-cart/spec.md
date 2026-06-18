# shopping-cart Specification

## Purpose

The `shopping-cart` capability manages the Customer's pre-checkout cart as an event-sourced `Cart` stream (keyed by a generated `cartId`) projected into an inline `CartView` read model. Slice 3.1 covers adding items (cart creation + line append); slice 4.1 covers checking the cart out on order placement (the terminal `CartCheckedOut`, which flips `IsOpen` to false); later slices add remove-item (3.2), change-quantity (3.3), and inactivity abandonment (3.4). The cart never reads the Catalog — product name and price arrive snapshotted on the command and stay authoritative through checkout. This is one of the Orders bounded context's two capabilities; the other is `order-lifecycle` (the Order aggregate).
## Requirements
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

### Requirement: Check out the cart on order placement

The system SHALL terminate the Customer's open cart when an order is placed from it. In the same transaction that records `OrderPlaced` on the new Order stream, the system SHALL append a `CartCheckedOut` event carrying the new `orderId` to the cart's stream, and the inline `CartView` SHALL set `IsOpen` to false. A checked-out cart SHALL no longer be resolved as the customer's open cart, so the customer is free to start a new cart, and a repeat placement against the same cart SHALL find no open cart. The checked-out cart's line items SHALL be retained as readable history.

#### Scenario: Placing an order checks out the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream with one or more line items
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** the `Cart` stream appends `CartCheckedOut { orderId }` in the same transaction as `OrderPlaced`
- **AND** the `CartView` for that cart has `IsOpen` set to false while its line items are retained
- **AND** the customer no longer has an open cart

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

### Requirement: Abandon the cart on inactivity

The system SHALL abandon a Customer's open cart when no cart activity (creating the cart, adding, removing, or changing the quantity of items) has occurred within the configured inactivity window. The fired `CartActivityTimeout` SHALL decide against the cart's own stream (via its folded `CartView`), not against any other read model:

- When the cart is closed (checked out or already abandoned) or unknown, the timeout SHALL append nothing and schedule nothing — a silent, idempotent no-op.
- When the cart's last activity is newer than the inactivity window (activity intervened since the timeout was scheduled), the timeout SHALL append nothing and SHALL reschedule itself for the last activity plus the configured window (fire-and-check).
- When the cart's last activity is at or older than the inactivity window, the system SHALL append a `CartAbandoned` event carrying the reason `inactivity_timeout`, the cart's lines, and its total value. The `CartView` SHALL set `IsOpen` to false while retaining the lines as readable history.

An abandoned cart SHALL no longer be resolved as the customer's open cart, so the customer is free to start a new cart, and edits or checkout against the abandoned cart SHALL be rejected by the existing open-cart resolution.

#### Scenario: An inactive cart is abandoned when the timeout fires

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose last activity (`CartItemAdded`) is older than the configured inactivity window
- **WHEN** the scheduled `CartActivityTimeout` for that cart fires
- **THEN** the `Cart` stream appends `CartAbandoned { reason: "inactivity_timeout", lines, totalValue }`
- **AND** the `CartView` for that cart has `IsOpen` set to false while its line items are retained
- **AND** the customer no longer has an open cart

#### Scenario: Activity intervened — the timeout reschedules instead of abandoning

- **GIVEN** the Customer `customer-X` has an open `Cart` stream whose last activity is newer than the configured inactivity window
- **WHEN** the scheduled `CartActivityTimeout` for that cart fires
- **THEN** no event is appended to the `Cart` stream
- **AND** a new `CartActivityTimeout` is scheduled for the cart's last activity plus the configured window

#### Scenario: A timeout firing on a closed cart is a no-op

- **GIVEN** the Customer `customer-X` has a `Cart` stream that already records a terminal event (`CartCheckedOut` or `CartAbandoned`)
- **WHEN** a `CartActivityTimeout` for that cart fires (including a duplicate delivery)
- **THEN** no event is appended to the `Cart` stream
- **AND** no further `CartActivityTimeout` is scheduled

### Requirement: Track carts awaiting activity

The system SHALL maintain an inline `CartsAwaitingActivity` read model with one row per open cart, carrying the cart's customer and its current abandonment deadline (the cart's last activity plus the configured inactivity window), readable at `GET /carts/awaiting-activity` ordered by soonest deadline. The row SHALL be created when the cart is created, its deadline SHALL advance as activity events fold, and the row SHALL be removed when the cart reaches either terminal event (`CartCheckedOut` or `CartAbandoned`). This read model is the observable face of the abandonment automation; the automation's decision SHALL NOT read it (the Cart stream is the single source of truth).

#### Scenario: An open cart appears on the list with its deadline

- **GIVEN** the Customer `customer-X` creates a cart by adding an item
- **WHEN** the `CartsAwaitingActivity` read model is queried at `GET /carts/awaiting-activity`
- **THEN** it contains one row for the new cart with the customer id and a deadline of the add time plus the configured inactivity window

#### Scenario: Cart activity advances the visible deadline

- **GIVEN** the Customer `customer-X` has an open cart on the `CartsAwaitingActivity` list
- **WHEN** the Customer adds, removes, or changes the quantity of an item in that cart
- **THEN** the row's deadline advances to the new activity's time plus the configured inactivity window

#### Scenario: A terminal event removes the row

- **GIVEN** the Customer `customer-X` has an open cart on the `CartsAwaitingActivity` list
- **WHEN** the cart reaches a terminal event — `CartCheckedOut` (order placed) or `CartAbandoned` (inactivity)
- **THEN** the row for that cart is removed from the `CartsAwaitingActivity` read model

### Requirement: Report on cart abandonment

The system SHALL maintain a `CartAbandonmentReport` read model aggregating `CartAbandoned` events across all Cart streams into one document per calendar day (UTC) of abandonment, carrying the count of carts abandoned that day, the total value abandoned, and the per-SKU abandonment counts. The projection SHALL be registered with an async lifecycle and SHALL NOT require a running async daemon (ADR 008: no daemon for round one): the report SHALL be materialized by an on-demand projection rebuild, and nothing on the demo's hot path SHALL depend on the report being current.

#### Scenario: A rebuild materializes the daily report from abandoned carts

- **GIVEN** two carts were abandoned on the same calendar day, one holding `crit-001` × 2 at total `49.98` and one holding `crit-002` × 1 at total `18.00`
- **AND** the `CartAbandonmentReport` has never been materialized (no daemon runs)
- **WHEN** an on-demand rebuild of the `CartAbandonmentReport` projection is executed
- **THEN** the report document for that day shows `abandonedCartCount: 2`, `totalValueAbandoned: 67.98`, and per-SKU counts `{ crit-001: 1, crit-002: 1 }`

#### Scenario: Checked-out carts do not appear in the report

- **GIVEN** a cart that was checked out (`CartCheckedOut`) and a cart that was abandoned (`CartAbandoned`) on the same calendar day
- **WHEN** an on-demand rebuild of the `CartAbandonmentReport` projection is executed
- **THEN** the report document for that day counts only the abandoned cart

### Requirement: Read the Customer's open cart

The system SHALL expose the Customer's single open cart as a read over the existing `CartView` read model, resolved by the Customer's identity rather than by `cartId`. The Customer's identity SHALL arrive ambiently on the request — the `X-Customer-Id` header, the round-one stubbed customer id behind the ADR 009 `useCurrentCustomer` seam (the stand-in for an authenticated claim) — not in the route or the request body.

- When the identity resolves to a Customer who has exactly one open `CartView` (the partial-unique open-cart index guarantees at most one), the system SHALL return that `CartView` with its SKU-keyed line items at their snapshotted names and prices.
- When the identity resolves to a Customer who has no open cart — none was ever created, or the most recent cart is `CartCheckedOut` (placed an order) or `CartAbandoned` (inactivity) — the system SHALL respond `404`. This is a "no open cart" signal the storefront renders as an empty cart, not an error condition.
- When no customer identity is supplied (a missing or blank `X-Customer-Id` header), the system SHALL reject the request with `400` — the request carries no identity against which to resolve a cart.

This read appends no event and reads no read model other than `CartView`. It is the customer-keyed read counterpart to the customer-keyed *write* side every cart command already uses (slices 3.1–3.3 resolve the open cart by customer; this exposes the same resolution as a read), closing the pre-frontend audit's blocking Gap #1: without it the cart-review screen (wireframe W2) cannot render on a cold load, when the storefront holds only the stubbed customer id and no `cartId`.

#### Scenario: Return the Customer's open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated`, `CartItemAdded { sku: "crit-001" }`, and `CartItemAdded { sku: "crit-002" }` (no `CartCheckedOut` or `CartAbandoned`)
- **WHEN** the storefront requests `GET /carts/mine` with header `X-Customer-Id: customer-X`
- **THEN** the single open `CartView` for `customer-X` is returned, with two SKU-keyed lines (`crit-001`, `crit-002`) at their snapshot prices
- **AND** no event is appended to the `Cart` stream

#### Scenario: A Customer with no open cart gets a 404

- **GIVEN** the Customer `customer-X` has no open cart — either none was ever created, or the most recent cart is `CartCheckedOut` or `CartAbandoned`
- **WHEN** the storefront requests `GET /carts/mine` with header `X-Customer-Id: customer-X`
- **THEN** the response is `404` ("no open cart"), not an error
- **AND** no event is appended to the `Cart` stream

#### Scenario: A request with no customer identity is rejected

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront requests `GET /carts/mine` with a missing or blank `X-Customer-Id` header
- **THEN** the response is `400` — the request carries no identity to resolve a cart
- **AND** no event is appended to any `Cart` stream

### Requirement: Reject an add-to-cart command with no usable product snapshot

The system SHALL reject an `AddToCart` command that carries no usable product snapshot with `400` (a malformed command) and SHALL append no event. The cart never reads the Catalog — the product snapshot (name and price) the storefront composed is a cart line's only source of product truth — so a command with no usable snapshot has nothing from which to build a line. A product snapshot is unusable when it is **absent** (no `productSnapshot` on the command), its **name is blank**, or its **price is negative**. This guard SHALL run before the Customer's open cart is resolved or created, so a malformed command never starts a new `Cart` stream and never appends a `CartItemAdded` event — the malformed command is stopped at the boundary, never becoming cart history.

This is a malformed-*input* rejection, distinct from the domain-state rejections on the cart's edit path (`CartItemNotPresent`, `NoOpenCart`): those refuse a well-formed command that does not fit the cart's current state; this refuses a command that is not well-formed at all.

#### Scenario: Reject an add with no product snapshot

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1 }` with no `productSnapshot`
- **THEN** the command is rejected with `400`
- **AND** no `Cart` stream is created for `customer-X` and no event is appended

#### Scenario: Reject an add whose snapshot has a blank name

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { name: "", price: 24.99 } }`
- **THEN** the command is rejected with `400`
- **AND** no event is appended to any `Cart` stream

#### Scenario: Reject an add whose snapshot has a negative price

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: -1.00 } }`
- **THEN** the command is rejected with `400`
- **AND** no event is appended to any `Cart` stream

