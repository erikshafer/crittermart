# order-lifecycle Specification

## Purpose

The `order-lifecycle` capability manages the Order aggregate's event-sourced stream (keyed by a generated `orderId`) from placement to its terminal state, projected into an inline `OrderStatusView` read model. Slice 4.1 covers placing an order from the cart (`OrderPlaced`, status `awaiting_confirmation`); later slices fold cross-BC stock reservation (4.2), stubbed payment authorization (4.3), confirmation when both gates close (4.4, cascading `CommitStock` to Inventory per slice 2.4), and cancellation on stock failure / payment decline / payment timeout (4.5–4.7) onto the same stream — the Order aggregate acting as its own process manager (Process Manager via Handlers, ADR 007). The terminal state is `OrderConfirmed` or `OrderCancelled`; CritterMart models no shipping or delivery. This is one of the Orders bounded context's two capabilities; the other is `shopping-cart` (the Cart aggregate).
## Requirements
### Requirement: Place an order from the cart

The system SHALL allow the Customer to place an order from their open cart. When the Customer has an open cart with at least one line, the system SHALL create a new `Order` stream keyed by a generated `orderId` and append an `OrderPlaced` event carrying the customer id, the cart's line items (SKU, quantity, and snapshotted name and price), and a total equal to the sum of each line's quantity multiplied by its snapshotted price. The system SHALL maintain an inline `OrderStatusView` read model whose status, line items, and total reflect the `OrderPlaced` event, with status `awaiting_confirmation`. When the Customer has no open cart, the system SHALL reject the command and create no `Order` stream. When the Customer's open cart has no lines, the system SHALL reject the command and create no `Order` stream. The order's lines and total are taken from the cart's snapshot and are authoritative — the order does not read the Catalog.

#### Scenario: Place an order from an open cart

- **GIVEN** the Customer `customer-X` has an open cart with `crit-001` quantity `2` at `24.99` and `crit-002` quantity `3` at `18.00`
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** a new `Order` stream keyed by a generated `orderId` records `OrderPlaced { orderId, customerId: "customer-X", items: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }, { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.00 }], total: 103.98 }`
- **AND** the `OrderStatusView` for that order shows status `awaiting_confirmation`, the two lines, and total `103.98`

#### Scenario: Reject placement when the customer has no open cart

- **GIVEN** the Customer `customer-Y` has no open cart
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-Y" }`
- **THEN** the command is rejected with a `409` response
- **AND** no `Order` stream is created

#### Scenario: Reject a second placement after checkout

- **GIVEN** the Customer `customer-X` has already placed an order from their cart (the cart is checked out and no longer open)
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }` again
- **THEN** the command is rejected with a `409` response
- **AND** no second `Order` stream is created

### Requirement: Reserve stock for a placed order

The system SHALL request stock reservation as soon as an order is placed, and SHALL record a granted reservation on the order's own stream. When an order is placed (`OrderPlaced`), the system SHALL send a single `ReserveStock` message to the Inventory context carrying the order id and the order's lines (each a SKU and quantity). When Inventory confirms the reservation by returning a `StockReserved` for that order, the system SHALL append a `StockReserved` event — a Klefter local commit — to the order's stream, and the inline `OrderStatusView` SHALL show status `stock_reserved`. The system SHALL be idempotent under at-least-once delivery: when a `StockReserved` arrives for an order whose stream is already terminal, or that already records `StockReserved`, the system SHALL append no further event and SHALL leave the stream and view unchanged.

#### Scenario: Placing an order requests stock reservation

- **GIVEN** an order is placed with lines `crit-001` quantity `2` and `crit-002` quantity `3`
- **WHEN** the `OrderPlaced` event is recorded
- **THEN** a single `ReserveStock { orderId, lines: [{ sku: "crit-001", quantity: 2 }, { sku: "crit-002", quantity: 3 }] }` message is sent to the Inventory context

#### Scenario: Record a granted reservation as a Klefter commit

- **GIVEN** the order `ord-A` has been placed and its stream shows `OrderPlaced`
- **WHEN** Inventory returns `StockReserved { orderId: "ord-A" }`
- **THEN** the `ord-A` stream appends `StockReserved { orderId: "ord-A" }`
- **AND** the `OrderStatusView` for `ord-A` shows status `stock_reserved`

#### Scenario: Ignore a duplicate or late StockReserved

- **GIVEN** the order `ord-A` stream already records `StockReserved` (or is already terminal)
- **WHEN** a second `StockReserved { orderId: "ord-A" }` arrives
- **THEN** no further event is appended to the `ord-A` stream
- **AND** the `OrderStatusView` for `ord-A` is unchanged

### Requirement: Cancel an order when stock cannot be reserved

The system SHALL cancel an order when Inventory cannot reserve its stock. When Inventory returns a `StockReservationFailed` for an order, the system SHALL append a `StockReservationFailed` event — a Klefter local commit — and then an `OrderCancelled` event carrying reason `stock_unavailable` to that order's stream, and the inline `OrderStatusView` SHALL show status `cancelled`. The system SHALL send no stock-release message to Inventory, because no reservation was made (the reservation is all-or-nothing, so a refusal reserved nothing). The system SHALL be idempotent: when a `StockReservationFailed` arrives for an order whose stream is already terminal, the system SHALL append no further event.

#### Scenario: Cancel an order whose stock is unavailable

- **GIVEN** the order `ord-B` has been placed and its stream shows `OrderPlaced`
- **WHEN** Inventory returns `StockReservationFailed { orderId: "ord-B", reason: "insufficient" }`
- **THEN** the `ord-B` stream appends `StockReservationFailed { orderId: "ord-B", reason: "insufficient" }` and then `OrderCancelled { orderId: "ord-B", reason: "stock_unavailable" }`
- **AND** the `OrderStatusView` for `ord-B` shows status `cancelled`
- **AND** no stock-release message is sent to Inventory

#### Scenario: Ignore a late StockReservationFailed on a terminal order

- **GIVEN** the order `ord-B` stream is already terminal (`OrderCancelled`)
- **WHEN** a duplicate `StockReservationFailed { orderId: "ord-B" }` arrives
- **THEN** no further event is appended to the `ord-B` stream

### Requirement: Authorize payment for a reserved order

The system SHALL authorize payment as soon as an order's stock is reserved, and SHALL record the provider's decision on the order's own stream. When a `StockReserved` Klefter commit is recorded, the system SHALL send a single `AuthorizePayment` request carrying the order id and the order total to a stubbed in-process payment provider. When the provider approves, the system SHALL append a `PaymentAuthorized` event — a Klefter local commit carrying the provider's auth code and the authorized amount (the order total) — to the order's stream. When the provider declines, the system SHALL append a `PaymentAuthFailed` event carrying the decline reason and SHALL NOT confirm the order; the order's visible status SHALL remain `stock_reserved` until the cancellation-on-decline slice (4.6) turns it terminal. The system SHALL be idempotent: the decision is applied only while the order is at the payment gate (`stock_reserved`, payment not yet decided); a decision for an order already authorized, terminal, or unknown SHALL append no further event.

#### Scenario: Authorize payment for a reserved order

- **GIVEN** the order `ord-A` has been placed and its stream shows `OrderPlaced` and `StockReserved` (total `103.98`)
- **WHEN** the stubbed provider approves the `AuthorizePayment { orderId: "ord-A", amount: 103.98 }` request
- **THEN** the `ord-A` stream appends `PaymentAuthorized { orderId: "ord-A", authCode: "stub-…", amount: 103.98 }`

#### Scenario: A declined payment is recorded but does not confirm

- **GIVEN** the order `ord-C` has been placed and its stream shows `OrderPlaced` and `StockReserved`
- **WHEN** the stubbed provider declines the `AuthorizePayment` request
- **THEN** the `ord-C` stream appends `PaymentAuthFailed { orderId: "ord-C", reason: "declined" }`
- **AND** no `OrderConfirmed` event is appended
- **AND** the `OrderStatusView` for `ord-C` still shows status `stock_reserved`

#### Scenario: Ignore a duplicate payment decision

- **GIVEN** the order `ord-A` stream already records `PaymentAuthorized` (or is already terminal)
- **WHEN** a second payment decision arrives for `ord-A`
- **THEN** no further event is appended to the `ord-A` stream

### Requirement: Confirm an order when both gates close

The system SHALL confirm an order once both its stock and payment gates are closed, and SHALL commit the reserved stock in the Inventory context. When `PaymentAuthorized` is recorded for an order that already records `StockReserved`, the system SHALL append an `OrderConfirmed` event to the order's stream, and the inline `OrderStatusView` SHALL show status `confirmed`. The system SHALL send a single `CommitStock` message to the Inventory context carrying the order id and the order's lines (each a SKU and quantity, read from the order's own stream), so Inventory can convert the reservation into a permanent commitment. `OrderConfirmed` is the terminal success state; CritterMart models no shipping or delivery beyond confirmation (vision.md non-goal). Because payment authorization only begins after stock is reserved, payment is always the second gate to close, so the confirmation is appended together with `PaymentAuthorized` in the same transaction.

#### Scenario: Confirm an order when both gates close

- **GIVEN** the order `ord-A` stream shows `OrderPlaced` and `StockReserved`
- **WHEN** `PaymentAuthorized { orderId: "ord-A" }` is recorded
- **THEN** the `ord-A` stream appends `OrderConfirmed { orderId: "ord-A" }`
- **AND** the `OrderStatusView` for `ord-A` shows status `confirmed`
- **AND** a single `CommitStock { orderId: "ord-A", lines: [{ sku, quantity }, …] }` message carrying the order's lines is sent to the Inventory context

### Requirement: Cancel an order when payment is declined

The system SHALL cancel an order when its payment is declined, and SHALL release the stock that was reserved for it. When a `PaymentAuthFailed` is recorded for an order at the payment gate (status `stock_reserved`), the system SHALL append an `OrderCancelled` event carrying reason `payment_declined` to that order's stream — in the same transaction as the `PaymentAuthFailed` — and the inline `OrderStatusView` SHALL show status `cancelled`. Because stock was reserved before the payment gate was reached, the system SHALL send a single `ReleaseStock` message to the Inventory context carrying the order id and the order's lines (each a SKU and quantity, read from the order's own stream). The system SHALL be idempotent: the cancellation is applied only while the order is at the payment gate; when a payment decision arrives for an order already terminal or unknown, the system SHALL append no further event and SHALL send no `ReleaseStock` message.

#### Scenario: Cancel an order whose payment was declined

- **GIVEN** the order `ord-C` has been placed and its stream shows `OrderPlaced`, `StockReserved`, and (just recorded) `PaymentAuthFailed { reason: "declined" }`
- **WHEN** the aggregate decision runs in the same handler that recorded the decline
- **THEN** the `ord-C` stream appends `OrderCancelled { orderId: "ord-C", reason: "payment_declined" }`
- **AND** the `OrderStatusView` for `ord-C` shows status `cancelled`
- **AND** a single `ReleaseStock { orderId: "ord-C", lines: [{ sku, quantity }, …] }` message carrying the order's lines is sent to the Inventory context

#### Scenario: Ignore a duplicate or late payment decision after cancellation

- **GIVEN** the order `ord-C` stream is already terminal (`OrderCancelled { reason: "payment_declined" }`)
- **WHEN** a second declined payment decision arrives for `ord-C`
- **THEN** no further event is appended to the `ord-C` stream
- **AND** no `ReleaseStock` message is sent to the Inventory context

### Requirement: Cancel an order on payment timeout

The system SHALL set a payment deadline for every placed order, and SHALL cancel an order that has not reached a terminal state when that deadline passes. When an order is placed (`OrderPlaced`), the system SHALL schedule an `OrderPaymentTimeout` self-message to be delivered after a configurable duration (`Orders:PaymentTimeout`, default 10 minutes). When the `OrderPaymentTimeout` fires for an order whose stream is **not** terminal, the system SHALL append an `OrderCancelled` event carrying reason `payment_timeout` to that order's stream, the inline `OrderStatusView` SHALL show status `cancelled`, and the system SHALL send a single `ReleaseStock` message to the Inventory context carrying the order id and the order's lines — **regardless** of whether the order's own stream records a `StockReserved` grant, because the grant reply may have been lost in transit while Inventory holds the reservation; the Inventory context's per-SKU reservation guard makes the release a no-op wherever nothing is actually held. When the `OrderPaymentTimeout` fires for an order whose stream is terminal (`OrderConfirmed` or `OrderCancelled`), the system SHALL append no event and SHALL send no message — losing the race to a settled order is the timer's normal fate. The system SHALL be idempotent under duplicate delivery of the timeout message.

#### Scenario: Placing an order schedules a payment deadline

- **GIVEN** the Customer `customer-X` has an open cart with at least one line
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }` and `OrderPlaced` is recorded for the new order `ord-T`
- **THEN** an `OrderPaymentTimeout { orderId: "ord-T" }` self-message is scheduled for delivery after the configured payment-timeout duration

#### Scenario: Cancel an order stuck at the payment gate when the deadline passes

- **GIVEN** the order `ord-T` stream shows `OrderPlaced` and `StockReserved`, and no `PaymentAuthorized` has been recorded
- **WHEN** the scheduled `OrderPaymentTimeout { orderId: "ord-T" }` fires
- **THEN** the `ord-T` stream appends `OrderCancelled { orderId: "ord-T", reason: "payment_timeout" }`
- **AND** the `OrderStatusView` for `ord-T` shows status `cancelled`
- **AND** a single `ReleaseStock { orderId: "ord-T", lines: [{ sku, quantity }, …] }` message carrying the order's lines is sent to the Inventory context

#### Scenario: Cancel an order that never heard back from Inventory

- **GIVEN** the order `ord-U` stream shows only `OrderPlaced` (status `awaiting_confirmation` — Inventory's reservation reply never arrived)
- **WHEN** the scheduled `OrderPaymentTimeout { orderId: "ord-U" }` fires
- **THEN** the `ord-U` stream appends `OrderCancelled { orderId: "ord-U", reason: "payment_timeout" }`
- **AND** the `OrderStatusView` for `ord-U` shows status `cancelled`
- **AND** a single `ReleaseStock { orderId: "ord-U", lines: [{ sku, quantity }, …] }` message is still sent to the Inventory context, so a reservation Inventory granted but Orders never learned of is released rather than leaked

#### Scenario: The timeout is a no-op on a confirmed order

- **GIVEN** the order `ord-A` stream is terminal (`OrderPlaced`, `StockReserved`, `PaymentAuthorized`, `OrderConfirmed`)
- **WHEN** the scheduled `OrderPaymentTimeout { orderId: "ord-A" }` fires after the confirmation
- **THEN** no further event is appended to the `ord-A` stream
- **AND** no `ReleaseStock` message is sent to the Inventory context

#### Scenario: Duplicate timeout delivery is a no-op

- **GIVEN** the order `ord-T` stream is already terminal (`OrderCancelled { reason: "payment_timeout" }`)
- **WHEN** a duplicate `OrderPaymentTimeout { orderId: "ord-T" }` arrives
- **THEN** no further event is appended to the `ord-T` stream
- **AND** no `ReleaseStock` message is sent to the Inventory context

### Requirement: Track orders awaiting payment

The system SHALL maintain an inline `OrdersAwaitingPayment` read model — the todo-list of the payment-deadline automation — holding one row per order that has not yet reached a terminal state. When `OrderPlaced` is recorded, the system SHALL create a row carrying the order id, customer id, order total, and the payment deadline. When a terminal event (`OrderConfirmed` or `OrderCancelled`, any reason) is recorded for an order, the system SHALL delete that order's row. The read model SHALL be queryable, listing every order currently awaiting its terminal state. The timeout handler SHALL NOT depend on this read model for its cancellation decision — the order's own stream is the single source of truth; the read model is the observable face of the automation.

#### Scenario: A placed order appears in the awaiting-payment list

- **GIVEN** the Customer places an order `ord-T` with total `103.98`
- **WHEN** the `OrderPlaced` event is recorded
- **THEN** the `OrdersAwaitingPayment` read model holds a row for `ord-T` carrying the customer id, total `103.98`, and the payment deadline

#### Scenario: A confirmed order's row is removed

- **GIVEN** the `OrdersAwaitingPayment` read model holds a row for `ord-A`
- **WHEN** `OrderConfirmed { orderId: "ord-A" }` is recorded
- **THEN** the `OrdersAwaitingPayment` read model no longer holds a row for `ord-A`

#### Scenario: A cancelled order's row is removed

- **GIVEN** the `OrdersAwaitingPayment` read model holds a row for `ord-T`
- **WHEN** `OrderCancelled { orderId: "ord-T" }` is recorded — by timeout, payment decline, or stock failure
- **THEN** the `OrdersAwaitingPayment` read model no longer holds a row for `ord-T`

### Requirement: Surface placement time and cancellation reason in the order view

The system SHALL surface, in the inline `OrderStatusView` read model, the time the order was placed and — once the order is cancelled — the reason it was cancelled, in addition to the order's status, line items, and total. The view SHALL carry a `placedAt` timestamp set at genesis to the append time of the order's `OrderPlaced` event (event metadata, not a new event field), present for every order from the moment it is placed. The view SHALL carry a `cancelReason` field that is null while the order has not been cancelled and, once an `OrderCancelled` event is recorded on the stream, carries that event's reason — one of `stock_unavailable`, `payment_declined`, or `payment_timeout`. This enrichment SHALL preserve the existing `OrderStatusView` wire shape `{ id, customerId, status, lines, total }` as a superset: `placedAt` and `cancelReason` are added alongside the existing fields, none of which is removed or renamed, so existing consumers (the W3 place-order read and the W4 tracking screen) are unaffected. The enrichment appends no event, sends no message, and reads no stream other than the order's own.

#### Scenario: A placed order's view carries its placement time

- **GIVEN** the Customer places an order `ord-A` and `OrderPlaced { orderId: "ord-A" }` is appended to its stream
- **WHEN** the `OrderStatusView` for `ord-A` is read
- **THEN** its `placedAt` equals the append time of the `OrderPlaced` event
- **AND** its `cancelReason` is null (the order is not cancelled)
- **AND** its `status`, line items, and `total` are unchanged from the existing place-order behavior

#### Scenario: A cancelled order's view carries the cancellation reason

- **GIVEN** the order `ord-B` stream shows `OrderPlaced` and then `OrderCancelled { orderId: "ord-B", reason: "stock_unavailable" }`
- **WHEN** the `OrderStatusView` for `ord-B` is read
- **THEN** its `status` is `cancelled`
- **AND** its `cancelReason` is `stock_unavailable`
- **AND** its `placedAt` still equals the append time of the `OrderPlaced` event

#### Scenario: Each cancellation route is surfaced by its own reason

- **GIVEN** three orders cancelled by the three routes — `OrderCancelled { reason: "stock_unavailable" }` (stock failure, slice 4.5), `OrderCancelled { reason: "payment_declined" }` (payment decline, slice 4.6), and `OrderCancelled { reason: "payment_timeout" }` (payment timeout, slice 4.7)
- **WHEN** each order's `OrderStatusView` is read
- **THEN** each view's `cancelReason` carries the reason of its own `OrderCancelled` event — `stock_unavailable`, `payment_declined`, and `payment_timeout` respectively

#### Scenario: An active order's view carries a null cancel reason

- **GIVEN** the order `ord-A` stream shows `OrderPlaced`, `StockReserved`, `PaymentAuthorized`, and `OrderConfirmed` (a confirmed, never-cancelled order)
- **WHEN** the `OrderStatusView` for `ord-A` is read
- **THEN** its `status` is `confirmed`
- **AND** its `cancelReason` is null — only a cancellation sets it

### Requirement: List a customer's own orders

The system SHALL allow the Customer to retrieve the list of their own orders, resolved by identity. When the Customer requests their orders authenticated by a validated JWT whose `sub` claim is their customer id (`Authorization: Bearer` — ADR 023 hard cutover; the transitional `X-Customer-Id` header is retired), the system SHALL return every `OrderStatusView` whose `customerId` matches the requesting Customer, ordered newest-first by placement time (`placedAt` descending), each carrying the full order view shape `{ id, customerId, status, lines, total, placedAt, cancelReason }`. The list SHALL include the Customer's orders in every lifecycle state — active (`awaiting_confirmation`, `stock_reserved`, `payment_authorized`) and terminal (`confirmed`, `cancelled`) alike — with each cancelled order carrying its `cancelReason`. When the Customer has no orders, the system SHALL return an empty list with a `200` response, not an error. The system SHALL scope the result strictly to the requesting Customer — an order belonging to another Customer SHALL NOT appear. When the request carries no valid token — missing, tampered, wrong-issuer/audience, or expired — the system SHALL reject it with a `401 Unauthorized` response, kept distinct from the empty-list case. The read appends no event, sends no message, creates no new projection, and reads no stream other than the Customer's own orders — it is a customer-keyed query over the existing inline `OrderStatusView` documents, served by a non-unique index on `OrderStatusView.customerId`.

#### Scenario: List my orders newest-first

- **GIVEN** the Customer `customer-X` has placed two orders, `ord-1` (placed earlier) and `ord-2` (placed later)
- **WHEN** the Customer requests `GET /orders/mine`, authenticated as `customer-X` (a valid Bearer token whose `sub` is `customer-X`)
- **THEN** the response is `200` with a list of two `OrderStatusView` items
- **AND** the list is ordered newest-first — `ord-2` precedes `ord-1`

#### Scenario: The list includes terminal orders with their cancel reason

- **GIVEN** the Customer `customer-X` has one `confirmed` order and one `cancelled` order whose `OrderCancelled` reason is `stock_unavailable`
- **WHEN** the Customer requests `GET /orders/mine`, authenticated as `customer-X`
- **THEN** the response lists both orders
- **AND** the `confirmed` order's `cancelReason` is null and the `cancelled` order's `cancelReason` is `stock_unavailable`

#### Scenario: Scope the result strictly to the requesting customer

- **GIVEN** the Customer `customer-X` has placed an order and the Customer `customer-Y` has placed a different order
- **WHEN** `customer-X` requests `GET /orders/mine`, authenticated as `customer-X`
- **THEN** the response lists only `customer-X`'s order
- **AND** `customer-Y`'s order does not appear

#### Scenario: A customer with no orders gets an empty list

- **GIVEN** the Customer `customer-Z` has never placed an order
- **WHEN** the Customer requests `GET /orders/mine`, authenticated as `customer-Z`
- **THEN** the response is `200` with an empty list `[]`
- **AND** the response is not an error

#### Scenario: Reject an unauthenticated request

- **GIVEN** a request to `GET /orders/mine` that carries no Bearer token (or an invalid or expired one)
- **WHEN** the system handles the request
- **THEN** the request is rejected with a `401 Unauthorized` response
- **AND** no order list is returned

### Requirement: Consume `CustomerRegistered` and maintain a local customer read model (slice 5.4)

The Orders service SHALL subscribe to the `CustomerRegistered` Published-Language event arriving
from the Identity service over RabbitMQ, and SHALL upsert a consumer-LOCAL `LocalCustomerView`
document for each customer it receives. When a `CustomerRegistered { customerId, email, displayName }`
message is handled, the system SHALL store (insert or update) a `LocalCustomerView` document keyed
by `customerId` and carrying `displayName` in Orders' own Marten document store — no synchronous
call into the Identity service is made (ADR 001 forbids sync service-to-service HTTP). The local
model is eventually consistent with Identity: a `CustomerRegistered` may arrive after the customer's
first order is placed, so callers MUST degrade gracefully when the local model is absent. The
`CustomerRegistered` type SHALL live in `CritterMart.Contracts` (the Published-Language shared
assembly); this change is when it graduates from its previous Identity-internal location. Orders'
Wolverine conventional routing handles the RabbitMQ subscription automatically — no explicit topology
configuration is needed beyond the handler itself.

#### Scenario: Receiving `CustomerRegistered` upserts the local customer model

- **GIVEN** no `LocalCustomerView` exists for `customerId: "c-1"`
- **WHEN** `CustomerRegistered { customerId: "c-1", email: "ada@example.com", displayName: "Ada Lovelace" }` arrives from RabbitMQ
- **THEN** a `LocalCustomerView { Id: "c-1", DisplayName: "Ada Lovelace" }` document is upserted in the Orders Marten store
- **AND** no synchronous call to the Identity service is made

#### Scenario: `CustomerRegistered` is idempotent (upsert, not insert-only)

- **GIVEN** a `LocalCustomerView` already exists for `customerId: "c-1"` with `DisplayName: "Ada"`
- **WHEN** `CustomerRegistered { customerId: "c-1", displayName: "Ada Lovelace" }` arrives again (e.g., a redelivery)
- **THEN** the `LocalCustomerView` for `c-1` is updated (`DisplayName: "Ada Lovelace"`) — no duplicate, no error

---

### Requirement: Resolve customer identity at read time (slice 5.3)

The Orders service SHALL enrich order responses with the customer's display name, resolved from the
consumer-local `LocalCustomerView` populated by slice 5.4. When `GET /orders/{orderId}` is
requested, the system SHALL load the `OrderStatusView` by `orderId` AND attempt to load the
`LocalCustomerView` for that order's `customerId`. The system SHALL return an enriched response
`{ id, customerId, status, lines, total, placedAt, cancelReason, customerName }` where `customerName`
is the `LocalCustomerView.DisplayName` when the local model is present, or `null` when the customer
is not yet known — the eventually-consistent degradation. The system SHALL apply the same enrichment
to `GET /orders/mine`: each order in the list SHALL carry `customerName` from the same local model
(one model load per list request, not per order, since all orders in a customer's list share the
same `customerId`). The `OrderStatusView` projection SHALL remain unchanged; the enrichment is
applied by the endpoint, not the projection, so no events are re-processed and no new stream is read.

#### Scenario: `GET /orders/{orderId}` returns the customer's display name when the local model is present

- **GIVEN** a `LocalCustomerView { Id: "c-1", DisplayName: "Ada Lovelace" }` exists in Orders
- **AND** an order `ord-A` was placed by customer `c-1`
- **WHEN** `GET /orders/ord-A` is requested
- **THEN** the response is `200` with `{ ..., customerName: "Ada Lovelace" }`

#### Scenario: `GET /orders/{orderId}` degrades gracefully when the local model is absent

- **GIVEN** no `LocalCustomerView` exists for `customerId: "c-2"` (not yet received)
- **AND** an order `ord-B` was placed by customer `c-2`
- **WHEN** `GET /orders/ord-B` is requested
- **THEN** the response is `200` with `{ ..., customerName: null }`
- **AND** no call to the Identity service is made

#### Scenario: `GET /orders/mine` enriches the list with the customer's display name

- **GIVEN** a `LocalCustomerView { Id: "c-1", DisplayName: "Ada Lovelace" }` exists in Orders
- **AND** customer `c-1` has placed two orders
- **WHEN** `GET /orders/mine` is requested, authenticated as `c-1` (a valid Bearer token whose `sub` is `c-1`)
- **THEN** the response is `200` with a list of two orders, each carrying `customerName: "Ada Lovelace"`

