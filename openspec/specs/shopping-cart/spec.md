# shopping-cart Specification

## Purpose

The `shopping-cart` capability manages the Customer's pre-checkout cart as an event-sourced `Cart` stream (keyed by a generated `cartId`) projected into an inline `CartView` read model. Slice 3.1 covers adding items (cart creation + line append); slice 4.1 covers checking the cart out on order placement (the terminal `CartCheckedOut`, which flips `IsOpen` to false); later slices add remove-item (3.2), change-quantity (3.3), and inactivity abandonment (3.4). The cart never reads the Catalog — product name and price arrive snapshotted on the command and stay authoritative through checkout. This is one of the Orders bounded context's two capabilities; the other is `order-lifecycle` (the Order aggregate).
## Requirements
### Requirement: Add an item to the cart

The system SHALL allow the Customer to add an item (a SKU and a quantity) to their cart. When the Customer has no open cart, the system SHALL create a new `Cart` stream keyed by a generated `cartId` and append a `CartCreated` event followed by a `CartItemAdded` event. When the Customer already has an open cart, the system SHALL append a further `CartItemAdded` event to that same cart. The item's name and price SHALL be taken from the product snapshot carried on the command — the cart does not read the Catalog. The system SHALL maintain an inline `CartView` read model whose line items are keyed by SKU: each distinct SKU on the stream appears as exactly one line whose quantity is the sum of that SKU's `CartItemAdded` quantities (less any removals or quantity changes), at the name and price snapshotted by that SKU's first `CartItemAdded`.

When a new cart is created, the system SHALL also schedule a `CartActivityTimeout` self-message for the configured inactivity window (`Orders:CartActivityTimeout`, default 2 hours), durably persisted so the deadline survives a service restart. Adding to an existing open cart SHALL NOT schedule a further timeout — under the fire-and-check policy, one scheduled timeout per cart suffices, and the fired timeout reads the cart's activity from its own event timestamps.

#### Scenario: Add the first item, creating a new cart

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **THEN** a new `Cart` stream keyed by a generated `cartId` records `CartCreated { cartId, customerId: "customer-X" }`
- **AND** the same stream appends `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **AND** the `CartView` for that cart shows a single line: `crit-001`, quantity `1`, at the snapshot price `24.99`

#### Scenario: Creating a cart schedules the inactivity timeout

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-001", quantity: 1, productSnapshot: { ... } }`
- **THEN** a new `Cart` stream is created as above
- **AND** a `CartActivityTimeout` self-message carrying the new `cartId` is scheduled for the configured inactivity window

#### Scenario: Add a second item to the open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated { cartId, customerId: "customer-X" }` and `CartItemAdded { sku: "crit-001", quantity: 1 }`
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-002", quantity: 3, productSnapshot: { name: "Nebula Newt", price: 18.00 } }`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-002", quantity: 3, snapshot: { name: "Nebula Newt", price: 18.00 } }`
- **AND** no new `Cart` stream is created
- **AND** no further `CartActivityTimeout` is scheduled
- **AND** the `CartView` for that cart shows two lines: `crit-001` quantity `1` at `24.99`, and `crit-002` quantity `3` at `18.00`

#### Scenario: Add a SKU that is already in the cart (quantities merge)

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartItemAdded { sku: "crit-001", quantity: 1, snapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **WHEN** the Customer issues `AddToCart { customerId: "customer-X", sku: "crit-001", quantity: 2, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`
- **THEN** the same `Cart` stream appends `CartItemAdded { sku: "crit-001", quantity: 2 }`
- **AND** the `CartView` for that cart shows a single line for `crit-001` with quantity `3` (the quantities merged)
- **AND** the line's name and price remain those of the first `CartItemAdded`'s snapshot

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

