# order-lifecycle — delta: retire the X-Customer-Id fallback (identity from `sub` only)

## MODIFIED Requirements

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
