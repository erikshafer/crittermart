## ADDED Requirements

### Requirement: Register a customer with credentials

The system SHALL expose `POST /register` accepting `RegisterWithCredentials { email, displayName, password }`.
When the email is not already registered and the password satisfies the configured ASP.NET Core Identity
password policy, the system SHALL create an ASP.NET Core Identity user (password hashed via
`UserManager.CreateAsync`) **and** a registry `Customer` row carrying the **same** string id, SHALL enroll
`CustomerRegistered { customerId, email, displayName }` in the EF-Core transactional outbox **in the same
transaction**, and SHALL respond `201 Created` with `Location: /customers/{id}`. Email SHALL be normalized
(`Trim().ToLowerInvariant()`) exactly as `RegisterCustomer` normalizes it. When a customer/user already exists
for the normalized email, the system SHALL reject the command with `409 CustomerAlreadyRegistered` — no user,
no row, no event; idempotent. When the password fails the configured policy, the system SHALL reject the
command with `400` carrying the Identity validation errors — no user, no row, no event. Registration SHALL be
all-or-nothing: the Identity user and the `Customer` row are created together or not at all. The pre-existing
`POST /customers` (`RegisterCustomer`, no password) SHALL remain available for admin/seeder-provisioned
customers.

#### Scenario: Register with credentials creates a user and a customer row

- **GIVEN** no customer or user exists for email `ada@example.com`
- **WHEN** `RegisterWithCredentials { email: "ada@example.com", displayName: "Ada Lovelace", password: "<valid>" }` is issued to `POST /register`
- **THEN** an ASP.NET Core Identity user is created with the password hashed
- **AND** a `Customer` row is written with the same string id as the Identity user
- **AND** `CustomerRegistered { customerId, email: "ada@example.com", displayName: "Ada Lovelace" }` is enrolled in the EF-Core outbox in the same transaction
- **AND** the response is `201 Created` with `Location: /customers/{id}`

#### Scenario: Reject a duplicate email

- **GIVEN** a customer/user already exists for email `ada@example.com`
- **WHEN** `RegisterWithCredentials { email: "Ada@Example.com", … }` is issued again
- **THEN** the command is rejected with `409 CustomerAlreadyRegistered`
- **AND** no Identity user is created, no `Customer` row is written, and no `CustomerRegistered` event is published

#### Scenario: Reject a password that fails policy

- **GIVEN** a password that fails the configured ASP.NET Core Identity password policy
- **WHEN** `RegisterWithCredentials { email: "grace@example.com", displayName: "Grace Hopper", password: "weak" }` is issued
- **THEN** the command is rejected with `400` carrying the Identity validation errors
- **AND** no Identity user is created, no `Customer` row is written, and no `CustomerRegistered` event is published

### Requirement: Log in and issue a JWT

The system SHALL expose `POST /login` accepting `LogIn { email, password }`. When an ASP.NET Core Identity
user exists for the normalized email and `SignInManager.CheckPasswordSignInAsync` succeeds, the system SHALL
mint and return a standard JSON Web Token whose `sub` claim is the customer id, carrying the configured
issuer and audience and an expiry set by the configured access-token lifetime, signed with Identity's
asymmetric **private** key (RSA). The token SHALL NOT be persisted. When the email is unknown **or** the
password is wrong, the system SHALL respond `401 Unauthorized` with **no** token and SHALL NOT distinguish the
two cases (no user enumeration).

#### Scenario: Successful login issues a signed JWT

- **GIVEN** customer `c-1` is registered with email `ada@example.com` and a known password
- **WHEN** `LogIn { email: "ada@example.com", password: "<correct>" }` is issued to `POST /login`
- **THEN** the response is `200 OK` carrying a JWT whose `sub` claim is `c-1`
- **AND** the JWT carries the configured issuer, audience, and an expiry, and is signed with Identity's private RSA key
- **AND** no row is changed and the token is not persisted

#### Scenario: Bad credentials are rejected without enumeration

- **GIVEN** either no user exists for `nobody@example.com`, or `ada@example.com` exists with a different password
- **WHEN** `LogIn { … }` is issued with those credentials
- **THEN** the response is `401 Unauthorized` with no token
- **AND** the response body does not distinguish "unknown email" from "wrong password"

### Requirement: Verify a token at a resource server

A resource server (Orders) SHALL configure JWT bearer authentication with Identity's **public key distributed
as configuration** and SHALL validate every presented `Authorization: Bearer` token **fully offline** —
signature against the public key, issuer, audience, and lifetime — with **no HTTP call into Identity**,
per-request or at startup. When a request carries a valid, unexpired token, the resource server SHALL treat
the token's `sub` claim as the authenticated customer id and SHALL use it as the trust boundary in place of
`X-Customer-Id`. When a request to a customer-keyed endpoint carries a missing, tampered, wrong-issuer,
wrong-audience, or expired token **and** no accepted fallback identity, the resource server SHALL reject it
locally with `401 Unauthorized`. During the layered cutover, the resource server MAY accept an `X-Customer-Id`
header as a **dev-only fallback** for the authenticated customer id when no Bearer token is presented; this
fallback is transitional and is not the trust boundary.

#### Scenario: A valid token authenticates the request as its subject

- **GIVEN** Orders is configured with Identity's public key (config), and a request carries `Authorization: Bearer <valid, unexpired JWT for c-1>`
- **WHEN** the request reaches a customer-keyed Orders endpoint
- **THEN** the token is validated offline (signature, issuer, audience, lifetime)
- **AND** the request proceeds as customer `c-1`, sourced from the token's `sub` claim
- **AND** no HTTP call is made into Identity

#### Scenario: An invalid or expired token is rejected locally

- **GIVEN** a request carrying a missing, tampered, wrong-issuer/audience, or expired token, and no fallback identity
- **WHEN** it reaches a customer-keyed Orders endpoint
- **THEN** the response is `401 Unauthorized`, decided locally
- **AND** no HTTP call is made into Identity

#### Scenario: The dev-only X-Customer-Id fallback still resolves identity during the cutover

- **GIVEN** a request to a customer-keyed Orders endpoint carrying no Bearer token but an `X-Customer-Id: c-1` header
- **WHEN** it reaches the endpoint
- **THEN** the request proceeds as customer `c-1` from the fallback header
- **AND** this path is a transitional dev-only compat shim, not the trust boundary

### Requirement: Log out

Because the issued JWT is stateless, the system SHALL treat logout as **client-side token discard**: the
frontend discards the held token and the `useCurrentCustomer` seam returns to unauthenticated, so subsequent
requests carry no bearer and customer-keyed endpoints respond `401`. The system SHALL NOT maintain a
server-side session or token denylist in this increment; a discarded token remains cryptographically valid
until it expires (server-side revocation is a deferred future increment).

#### Scenario: Logout discards the token client-side

- **GIVEN** customer `c-1` holds a valid JWT and is authenticated in the SPA
- **WHEN** the customer logs out
- **THEN** the client discards the token and `useCurrentCustomer` returns to unauthenticated
- **AND** subsequent requests to customer-keyed endpoints carry no bearer and respond `401`
- **AND** no server-side session or denylist state is maintained
