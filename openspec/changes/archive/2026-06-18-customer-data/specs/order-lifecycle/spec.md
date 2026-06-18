# order-lifecycle Delta — customer data integration (slices 5.3 + 5.4)

## ADDED Requirements

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
- **WHEN** `GET /orders/mine` is requested with `X-Customer-Id: c-1`
- **THEN** the response is `200` with a list of two orders, each carrying `customerName: "Ada Lovelace"`
