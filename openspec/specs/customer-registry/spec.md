# customer-registry Specification

## Purpose
TBD - created by archiving change customer-registry. Update Purpose after archive.
## Requirements
### Requirement: Register a customer into the registry

The system SHALL allow a customer to be registered into the Identity registry, carrying an email and a display name. On registration the system SHALL mint an opaque customer id and a `registeredAt` timestamp, persist a `Customer` row, and SHALL cascade a `CustomerRegistered` integration event through the EF-Core transactional outbox **in the same transaction** as the row insert — published after the commit succeeds. The `Customer` row is the source of truth; `CustomerRegistered` is an outbound notification, not a stream event.

#### Scenario: Register a new customer

- **GIVEN** no customer exists for email `ada@example.com`
- **WHEN** the storefront issues `RegisterCustomer { email: "ada@example.com", displayName: "Ada Lovelace" }` at `POST /customers`
- **THEN** the system persists a `Customer` row with a server-minted id and `registeredAt`
- **AND** the response is `201 Created` with `Location: /customers/{id}`
- **AND** `CustomerRegistered { customerId, email, displayName }` is enrolled in the EF-Core outbox in the same transaction and published after the commit succeeds

### Requirement: Customer emails are unique in the registry

The system SHALL reject any attempt to register a customer whose email already exists in the registry. Email comparison SHALL be **normalized** — trimmed and lowercased — so that casing or surrounding whitespace cannot defeat the guard (`Ada@Example.com` and `ada@example.com` are the same customer). The rejection SHALL be idempotent: no new `Customer` row is created, and no `CustomerRegistered` event is published. Uniqueness SHALL be enforced both at the application layer (returning `CustomerAlreadyRegistered` as `409 Conflict`) and by a unique database index on the normalized email column.

#### Scenario: Reject a duplicate email (case-insensitive)

- **GIVEN** a customer already exists for email `ada@example.com`
- **WHEN** the storefront issues `RegisterCustomer { email: "Ada@Example.com", displayName: "Ada L." }`
- **THEN** the command is rejected with `CustomerAlreadyRegistered` (`409 Conflict`)
- **AND** no new `Customer` row is created
- **AND** no `CustomerRegistered` event is published
- **AND** the existing customer row is unchanged

### Requirement: Resolve a customer by id

The system SHALL expose `GET /customers/{id}` so that a caller can resolve a registered customer by id, returning the customer's `id`, `email`, `displayName`, and `registeredAt`. This is the Identity bounded context's Open-Host Service read for the storefront. Resolving an unknown id SHALL return `404 Not Found`. Resolution is read-only: it SHALL NOT mutate any row and SHALL NOT record any event.

#### Scenario: Resolve a registered customer

- **GIVEN** a customer `{ id: "c-1", email: "ada@example.com", displayName: "Ada Lovelace", registeredAt }` is registered
- **WHEN** the caller requests `GET /customers/c-1`
- **THEN** the response is `200 OK` with `{ id: "c-1", email: "ada@example.com", displayName: "Ada Lovelace", registeredAt }`

#### Scenario: Resolve an unknown customer

- **GIVEN** no customer with id `nope` exists
- **WHEN** the caller requests `GET /customers/nope`
- **THEN** the response is `404 Not Found`

