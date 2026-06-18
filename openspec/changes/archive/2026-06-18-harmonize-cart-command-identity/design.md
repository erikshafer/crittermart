# Design — Harmonize cart command identity

## Context

Three cart commands key identity off the route (`/carts/{customerId}/…`); the cart read keys it off the `X-Customer-Id` header (`/carts/mine`). The split was a deliberate deferral logged in slices 3.1–3.3 and in the frontend SKILL Convention 4 ("logged for a future harmonization tidy … until then, do not 'fix' these call sites"). This change is that tidy. It is a transport change only — no event, projection, index, or domain rule moves.

## Decisions

### Decision 1 — Route shape: `/carts/mine/*` (mirror the read)

The commands adopt the literal `mine` segment the read already uses:

- `POST /carts/mine/items`
- `POST /carts/mine/items/{sku}/quantity`
- `DELETE /carts/mine/items/{sku}`

`mine` is a literal segment, so it wins ASP.NET Core route precedence over `/carts/{cartId}` (the same precedence that already lets `/carts/mine` and `/carts/awaiting-activity` win). "mine" reads as "the authenticated customer's cart" — identity is the principal's, never restated in the URL. Rejected: keeping `/carts/{customerId}/…` but ignoring the route param (a dead path segment is more confusing than none).

### Decision 2 — Blank-header → 400 guard inline in the handler

Each handler binds `[FromHeader(Name = "X-Customer-Id")] string? customerId` and guards `string.IsNullOrWhiteSpace(customerId)` → `400`, exactly mirroring `ViewMyCart` (the proven template). The guard is inline in the endpoint, not in `AddToCart`'s `Validate` ProblemDetails method: the snapshot `Validate` is a payload-shape check, while the header guard is an identity check shaped like the read's — keeping them as siblings of the read's guard is clearer than overloading `Validate` with a header parameter. A `400` (no identity) stays distinct from the existing `409` (`NoOpenCart` / `CartItemNotPresent` — a well-formed command that does not fit the cart's state).

### Decision 3 — Scope: cart commands only; place-order deferred

Place-order (`POST /orders { customerId }`) is a fourth, body-keyed transport, but it lives in the `order-lifecycle` capability and crosses a capability boundary. Harmonizing it (empty-body POST + the `PlaceOrder` record losing `CustomerId` + the checkout narrative moment + `usePlaceOrder`) is a separate, cleanly-scoped fast-follow. This change keeps to one capability (`shopping-cart`), matching the one-capability-per-change granularity.

### Decision 4 — `{sku}` stays on the route

For change-quantity and remove, only *identity* moves to the header; the `{sku}` being acted on stays a route parameter. The `{sku}`-on-the-route + body shape still mirrors Catalog's change-price (`POST /products/{sku}/price`) — a command-shaped POST to the thing being changed. Only the customer key, which was never the resource being addressed, leaves the path.

## Risks / trade-offs

- **Breaking route change.** Every live caller (tests, the SPA, the demo runbook/traffic/OTel docs) must move in lockstep — handled in this one PR. Frozen historical records (prompts, retros, archived changes) are intentionally left untouched.
- **No new domain coverage.** This is correctness/consistency polish, not new behavior; the GWT scenarios are unchanged except for the added missing-identity rejection.
