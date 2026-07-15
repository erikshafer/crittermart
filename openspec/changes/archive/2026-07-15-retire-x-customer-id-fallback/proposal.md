# Retire the dev-only X-Customer-Id fallback (hard cutover to `sub`-only identity)

## Why

Real authentication shipped in slices 5.8–5.11 (ADR 023) with a deliberately **layered** cutover: the JWT
`sub` claim became the trust boundary, but the round-one `X-Customer-Id` header survived as a dev-only
fallback so the seeder, the existing test suites, and `demo-traffic.ps1` kept working inside that PR. That
fallback was explicitly DEBT-tracked for removal (retro implementations/037 Outstanding). While it exists,
any caller can still name an arbitrary customer with a plain request header — the trust boundary is only as
strong as its weakest accepted transport. The frontend already migrated (PR #130 sends `Authorization:
Bearer` only), so nothing production-shaped depends on the header anymore.

## What Changes

- **BREAKING** — Orders' six customer-keyed endpoints (`POST /carts/mine/items`, `GET /carts/mine`,
  `DELETE /carts/mine/items/{sku}`, `POST /carts/mine/items/{sku}/quantity`, `POST /orders`,
  `GET /orders/mine`) no longer accept the `X-Customer-Id` header. Identity comes from the validated JWT's
  `sub` claim **only**.
- **BREAKING** — the endpoints are now blanket-`[Authorize]`'d: a request with **no** token is rejected
  `401 Unauthorized` (previously `400` "missing header"). Absent credentials are an authentication failure,
  not a malformed request, now that the token is the only identity transport. This also resolves a latent
  spec contradiction: `customer-registry` already required 401 for a missing token, while
  `shopping-cart`/`order-lifecycle` asserted 400 for a missing header.
- The `CustomerIdentity.TryResolve` four-case resolver collapses to a single guaranteed-present claim read
  behind `[Authorize]`.
- Test suites (Orders + CrossBc) authenticate with real dev-key-minted JWTs via a shared `JwtTestTokens`
  seam; `demo-traffic.ps1` and the runbook's manual blocks register a throwaway shopper and log in for a
  Bearer token.

## Capabilities

### New Capabilities

_None — this change is a subtraction._

### Modified Capabilities

- `customer-registry`: "Verify a token at a resource server" loses its transitional MAY-accept-fallback
  clause and fallback scenario; `sub` is the sole trust boundary and a token-less request is 401.
- `shopping-cart`: cart command/read requirements (add, remove, change-quantity, view-my-cart) stop naming
  the `X-Customer-Id` header; scenarios authenticate as a customer via Bearer token, and the
  "no identity → 400" scenarios become "no token → 401".
- `order-lifecycle`: place-order and list-my-orders requirements likewise swap header identity for the
  authenticated `sub` claim, and the "no identity header → 400" scenario becomes "no token → 401".

## Impact

- **Code**: `src/CritterMart.Orders/Auth/CustomerIdentity.cs` (resolver collapse), the six endpoint files
  under `src/CritterMart.Orders/Features/`, `src/CritterMart.Orders/Program.cs` (comments only — the
  JwtBearer/authorization wiring already existed).
- **Tests**: new shared `tests/CritterMart.TestSupport` project (JWT minting seam); every Orders + CrossBc
  scenario that sent the header now sends `Authorization: Bearer`; the layered-cutover fallback test is
  inverted to assert rejection.
- **Demo tooling**: `docs/demo-traffic.ps1` (register→login→Bearer per run), `docs/demo-runbook.md`
  command blocks, `src/CritterMart.Seeding/Program.cs` (stale comment; the passwordless `customer-demo`
  registration is unchanged).
- **Not affected**: the SPA (already Bearer-only), Catalog/Inventory/Identity service code, the
  anonymous Orders reads (`/carts/{cartId}`, `/orders/{orderId}`, the awaiting-* automation lists).
