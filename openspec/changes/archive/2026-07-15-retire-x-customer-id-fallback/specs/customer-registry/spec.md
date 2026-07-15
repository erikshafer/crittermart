# customer-registry — delta: retire the X-Customer-Id fallback (identity from `sub` only)

## MODIFIED Requirements

### Requirement: Verify a token at a resource server

A resource server (Orders) SHALL configure JWT bearer authentication with Identity's **public key distributed
as configuration** and SHALL validate every presented `Authorization: Bearer` token **fully offline** —
signature against the public key, issuer, audience, and lifetime — with **no HTTP call into Identity**,
per-request or at startup. When a request carries a valid, unexpired token, the resource server SHALL treat
the token's `sub` claim as the authenticated customer id — the **sole** customer trust boundary. When a
request to a customer-keyed endpoint carries a missing, tampered, wrong-issuer, wrong-audience, or expired
token, the resource server SHALL reject it locally with `401 Unauthorized`; customer-keyed endpoints SHALL
require authorization, so the rejection precedes any handler. The resource server SHALL NOT accept any
request header as a customer identity: the layered cutover's transitional dev-only `X-Customer-Id` fallback
is retired, and presenting that header SHALL have no effect on identity resolution.

#### Scenario: A valid token authenticates the request as its subject

- **GIVEN** Orders is configured with Identity's public key (config), and a request carries `Authorization: Bearer <valid, unexpired JWT for c-1>`
- **WHEN** the request reaches a customer-keyed Orders endpoint
- **THEN** the token is validated offline (signature, issuer, audience, lifetime)
- **AND** the request proceeds as customer `c-1`, sourced from the token's `sub` claim
- **AND** no HTTP call is made into Identity

#### Scenario: An invalid, expired, or missing token is rejected locally

- **GIVEN** a request carrying a missing, tampered, wrong-issuer/audience, or expired token
- **WHEN** it reaches a customer-keyed Orders endpoint
- **THEN** the response is `401 Unauthorized`, decided locally
- **AND** no HTTP call is made into Identity

#### Scenario: The retired X-Customer-Id header no longer resolves identity

- **GIVEN** a request to a customer-keyed Orders endpoint carrying no Bearer token but an `X-Customer-Id: c-1` header
- **WHEN** it reaches the endpoint
- **THEN** the response is `401 Unauthorized` — the header names no customer and no fallback path exists
- **AND** no event is appended and no state is changed for `c-1`
