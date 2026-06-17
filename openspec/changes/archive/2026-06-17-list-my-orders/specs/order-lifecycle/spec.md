# order-lifecycle Delta — List my orders (customer-keyed order list)

## ADDED Requirements

### Requirement: List a customer's own orders

The system SHALL allow the Customer to retrieve the list of their own orders, resolved by identity. When the Customer requests their orders carrying their identity in the `X-Customer-Id` header, the system SHALL return every `OrderStatusView` whose `customerId` matches the requesting Customer, ordered newest-first by placement time (`placedAt` descending), each carrying the full order view shape `{ id, customerId, status, lines, total, placedAt, cancelReason }`. The list SHALL include the Customer's orders in every lifecycle state — active (`awaiting_confirmation`, `stock_reserved`, `payment_authorized`) and terminal (`confirmed`, `cancelled`) alike — with each cancelled order carrying its `cancelReason`. When the Customer has no orders, the system SHALL return an empty list with a `200` response, not an error. The system SHALL scope the result strictly to the requesting Customer — an order belonging to another Customer SHALL NOT appear. When the request carries no identity (a missing or blank `X-Customer-Id` header), the system SHALL reject it with a `400` response, kept distinct from the empty-list case. The read appends no event, sends no message, creates no new projection, and reads no stream other than the Customer's own orders — it is a customer-keyed query over the existing inline `OrderStatusView` documents, served by a non-unique index on `OrderStatusView.customerId`.

#### Scenario: List my orders newest-first

- **GIVEN** the Customer `customer-X` has placed two orders, `ord-1` (placed earlier) and `ord-2` (placed later)
- **WHEN** the Customer requests `GET /orders/mine` with header `X-Customer-Id: customer-X`
- **THEN** the response is `200` with a list of two `OrderStatusView` items
- **AND** the list is ordered newest-first — `ord-2` precedes `ord-1`

#### Scenario: The list includes terminal orders with their cancel reason

- **GIVEN** the Customer `customer-X` has one `confirmed` order and one `cancelled` order whose `OrderCancelled` reason is `stock_unavailable`
- **WHEN** the Customer requests `GET /orders/mine` with header `X-Customer-Id: customer-X`
- **THEN** the response lists both orders
- **AND** the `confirmed` order's `cancelReason` is null and the `cancelled` order's `cancelReason` is `stock_unavailable`

#### Scenario: Scope the result strictly to the requesting customer

- **GIVEN** the Customer `customer-X` has placed an order and the Customer `customer-Y` has placed a different order
- **WHEN** `customer-X` requests `GET /orders/mine` with header `X-Customer-Id: customer-X`
- **THEN** the response lists only `customer-X`'s order
- **AND** `customer-Y`'s order does not appear

#### Scenario: A customer with no orders gets an empty list

- **GIVEN** the Customer `customer-Z` has never placed an order
- **WHEN** the Customer requests `GET /orders/mine` with header `X-Customer-Id: customer-Z`
- **THEN** the response is `200` with an empty list `[]`
- **AND** the response is not an error

#### Scenario: Reject a request with no identity header

- **GIVEN** a request to `GET /orders/mine` that carries no `X-Customer-Id` header (or a blank one)
- **WHEN** the system handles the request
- **THEN** the request is rejected with a `400` response
- **AND** no order list is returned
