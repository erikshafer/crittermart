# customer-registry Specification

## Purpose
TBD - created by archiving change customer-registry. Update Purpose after archive.
## Requirements
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

### Requirement: Open an email-change saga on request

The system SHALL open an `EmailChange` saga when a `RequestEmailChange { customerId, newEmail }` command is
issued for a customer with no open `EmailChange` saga. The system SHALL reject the command with
`404 CustomerNotFound` when no `Customer` row exists for `customerId` — no saga is opened. The system SHALL
reject the command with `409 EmailAlreadyRegistered` when `newEmail` (normalized) is already the registered
email of a *different* customer — no saga is opened. Otherwise, the system SHALL start an `EmailChange` saga
keyed by `customerId`, recording the normalized `newEmail` as `PendingEmail`, and SHALL schedule an
`EmailChangeTimeout` for the configured deadline. The `Customer` row SHALL remain unchanged until a
subsequent confirmation. When a `RequestEmailChange` is issued for a customer with an already-open
`EmailChange` saga, the system SHALL update the saga's `PendingEmail` to the new value and SHALL NOT
schedule a second `EmailChangeTimeout` — the deadline scheduled by the original request continues to govern
the confirm window (Wolverine provides no scheduled-message cancellation, so a second timeout would leave
the first still armed and firing early against a "reset" window).

#### Scenario: Open an email-change saga

- **GIVEN** customer `c-1` is registered with email `ada@example.com` and has no open `EmailChange` saga
- **WHEN** `RequestEmailChange { customerId: "c-1", newEmail: "ada.new@example.com" }` is issued
- **THEN** an `EmailChange` saga opens keyed by `c-1` with `PendingEmail = "ada.new@example.com"`
- **AND** an `EmailChangeTimeout` is scheduled for the configured deadline
- **AND** `Customer.Email` for `c-1` remains `ada@example.com`

#### Scenario: Reject a request for an unknown customer

- **GIVEN** no customer exists with id `ghost-1`
- **WHEN** `RequestEmailChange { customerId: "ghost-1", newEmail: "new@example.com" }` is issued
- **THEN** the command is rejected with `404 CustomerNotFound`
- **AND** no `EmailChange` saga is opened

#### Scenario: Reject a request for an email already registered to another customer

- **GIVEN** customer `c-2` is registered with email `taken@example.com`
- **WHEN** customer `c-1` issues `RequestEmailChange { customerId: "c-1", newEmail: "taken@example.com" }`
- **THEN** the command is rejected with `409 EmailAlreadyRegistered`
- **AND** no `EmailChange` saga is opened

#### Scenario: Re-request while a change is already pending updates the pending email, not the deadline

- **GIVEN** customer `c-1` has an open `EmailChange` saga with `PendingEmail = "ada.new@example.com"`,
  opened at `t0` with `EmailChangeTimeout` scheduled for `t0 + deadline`
- **WHEN** `c-1` issues a second `RequestEmailChange { customerId: "c-1", newEmail: "ada.newer@example.com" }`
  at `t1` where `t0 < t1 < t0 + deadline`
- **THEN** the saga's `PendingEmail` becomes `"ada.newer@example.com"`
- **AND** the original `EmailChangeTimeout` (still armed for `t0 + deadline`) is left unchanged — no second
  timeout is scheduled

### Requirement: Confirm an email change within the window

The system SHALL apply a pending email change when a `ConfirmEmailChange { customerId }` command is issued
while an `EmailChange` saga is open for that customer and its `PendingEmail` is not already registered to a
different customer: the system SHALL set `Customer.Email` to the normalized `PendingEmail` and SHALL
complete the saga (`MarkCompleted()`). When `PendingEmail` has since been claimed by another customer's
registration or email change, the system SHALL reject the command with `409 EmailChangeConflict` and SHALL
leave the saga open — no row change is made. When a `ConfirmEmailChange` is issued for a customer with no
open `EmailChange` saga (the window already expired, or a prior confirmation already completed it), the
system SHALL treat it as a silent no-op.

#### Scenario: Confirm within the window applies the change

- **GIVEN** customer `c-1` has an open `EmailChange` saga with `PendingEmail = "ada.new@example.com"` and
  the deadline has not passed
- **WHEN** `ConfirmEmailChange { customerId: "c-1" }` is issued
- **THEN** `Customer.Email` for `c-1` becomes `ada.new@example.com`
- **AND** the `EmailChange` saga completes (`MarkCompleted()`)

#### Scenario: Confirm after the window expired is a no-op

- **GIVEN** customer `c-1`'s `EmailChange` saga already completed via timeout
- **WHEN** `ConfirmEmailChange { customerId: "c-1" }` is issued
- **THEN** no saga is found for `c-1` and the command is a silent no-op
- **AND** `Customer.Email` for `c-1` is unaffected

#### Scenario: Confirm conflicts with an email claimed during the window

- **GIVEN** customer `c-1` has an open `EmailChange` saga with `PendingEmail = "ada.new@example.com"`, and
  a different customer has since registered or changed into that email
- **WHEN** `ConfirmEmailChange { customerId: "c-1" }` is issued
- **THEN** the command is rejected with `409 EmailChangeConflict`
- **AND** the `EmailChange` saga remains open (not completed)
- **AND** `Customer.Email` for `c-1` is unchanged

### Requirement: Drop an email change on timeout

The system SHALL enforce the email-change confirmation deadline via the `EmailChangeTimeout` scheduled when
the `EmailChange` saga opens. When an `EmailChangeTimeout` is delivered for a customer whose `EmailChange`
saga is still open, the system SHALL drop the pending change (no `Customer` row is modified) and SHALL
complete the saga (`MarkCompleted()`). When an `EmailChangeTimeout` is delivered for a customer whose saga
has already completed (confirmed, or already timed out), it SHALL be a silent no-op, since the messaging
runtime provides no scheduled-message cancellation.

#### Scenario: Timeout with no confirmation drops the pending change

- **GIVEN** customer `c-1` has an open `EmailChange` saga with `PendingEmail = "ada.new@example.com"` and no
  `ConfirmEmailChange` arrived before the deadline
- **WHEN** the scheduled `EmailChangeTimeout` is delivered
- **THEN** the `EmailChange` saga completes (`MarkCompleted()`)
- **AND** `Customer.Email` for `c-1` remains unchanged

#### Scenario: Timeout after the saga already resolved is a no-op

- **GIVEN** customer `c-1`'s `EmailChange` saga already completed (a `ConfirmEmailChange` resolved it)
- **WHEN** the previously-scheduled `EmailChangeTimeout` is delivered anyway
- **THEN** no saga is found for `c-1` and the timeout is a silent no-op
- **AND** `Customer.Email` for `c-1` (already updated by the confirm) is unaffected

