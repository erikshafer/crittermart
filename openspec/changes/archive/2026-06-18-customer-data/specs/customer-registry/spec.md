# customer-registry Delta — customer data integration (seeder explicit-id support)

## MODIFIED Requirements

### Requirement: Register a customer into the registry

The system SHALL allow the storefront or a trusted caller to register a new customer. When a
`RegisterCustomer` command carrying a valid email address and a display name is issued to
`POST /customers`, the system SHALL insert a new `Customer` row — with the normalized email
(`Trim().ToLowerInvariant()`), the display name, and a server-set `registeredAt` timestamp — and
SHALL respond `201 Created` with `Location: /customers/{id}` and a body carrying the minted `id`.
When a `RegisterCustomer` command carries an explicit `id` field, the system SHALL use that value
as the customer's `id` verbatim rather than minting a server UUID; when the `id` field is absent
or `null`, the system SHALL mint a server UUID as before. The system SHALL publish a
`CustomerRegistered { customerId, email, displayName }` event to the EF-Core transactional outbox
**in the same transaction** as the row insert, so the event is published only after the commit
succeeds. When a customer with the same normalized email already exists, the system SHALL reject
the command with `409 CustomerAlreadyRegistered` — no row is written and no event is published.
The email uniqueness guarantee SHALL be enforced at both the application layer (a pre-handle guard)
and the database layer (a unique index on the normalized `email` column, applied as idempotent
startup DDL).

#### Scenario: Register a new customer (server-minted id)

- **GIVEN** no customer exists for email `ada@example.com`
- **WHEN** `RegisterCustomer { email: "ada@example.com", displayName: "Ada Lovelace" }` is issued (no `id` field)
- **THEN** a `Customer` row is written with a server-minted UUID as `id`
- **AND** the response is `201 Created` with `Location: /customers/{id}` and body `{ id }`
- **AND** `CustomerRegistered { customerId, email: "ada@example.com", displayName: "Ada Lovelace" }` is enrolled in the EF-Core outbox in the same transaction

#### Scenario: Register a new customer with an explicit id

- **GIVEN** no customer exists for email `demo@crittermart.com`
- **WHEN** `RegisterCustomer { email: "demo@crittermart.com", displayName: "Demo Customer", id: "customer-demo" }` is issued
- **THEN** a `Customer` row is written with `id = "customer-demo"` (the caller-supplied value, not a UUID)
- **AND** the response is `201 Created` with `Location: /customers/customer-demo` and body `{ id: "customer-demo" }`
- **AND** `CustomerRegistered { customerId: "customer-demo", ... }` is enrolled in the EF-Core outbox

#### Scenario: Reject a duplicate email

- **GIVEN** a customer already exists for email `ada@example.com`
- **WHEN** `RegisterCustomer { email: "ada@example.com", ... }` is issued again (with or without an explicit `id`)
- **THEN** the command is rejected with `409 CustomerAlreadyRegistered`
- **AND** no new row is written and no event is published
