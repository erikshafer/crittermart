# Design: List my orders — a customer-keyed order list

## Context

The storefront can track a single order by id (W4, `GET /orders/{orderId}`), but not list a Customer's orders. The [pre-frontend endpoint audit](../../../docs/research/pre-frontend-endpoint-audit.md) logged this as **Gap #3** and Narrative 005 names it deferred. This slice closes it: a customer-keyed read across the Customer's orders + the "My Orders" list screen.

Crucially, this is **not** a new projection. Every placed order already has an inline `OrderStatusView` document carrying `customerId`, `status`, `total`, and `placedAt` (slice 025). The list is a filtered query over those documents — the same shape the codebase already uses for the catalog list (`GET /products` → `Query<Product>()`, slice 1.2) and the open-cart read (`GET /carts/mine` → `Query<CartView>().Where(customerId)`, slice 3.5).

## Goals / Non-Goals

**Goals:**
- A Customer can retrieve all their orders, newest-first, scoped strictly to their identity.
- The "My Orders" screen lists those orders, each row linking into the existing W4 track screen.
- Reuse the existing `OrderStatusView` contract end to end (backend document, frontend Zod schema) — one order read shape, list and detail alike.

**Non-Goals:**
- No new event, command, projection, aggregate, or read model — a filtered query over the existing inline `OrderStatusView`.
- No new order *summary* DTO — the existing view shape is already exactly right (Decision 1).
- No pagination / filtering / sorting controls (round one; the list is small — one demo customer's handful of orders). A later slice can add them if the order count grows.
- No live push — consistent with W4, the list converges by TanStack Query refetch, not a socket (ADR 015 R5). (No `refetchInterval` here, though — a list is entered, read once, and revisited; only the by-id track screen polls.)

## Decisions

### Decision 1 — Reuse `OrderStatusView`; no new projection, no summary DTO (settled by precedent, not forked)

The endpoint returns `IReadOnlyList<OrderStatusView>` — the existing inline-projected document — filtered by `customerId`. No `CustomerOrdersView` multi-stream projection, no slim `OrderSummary` record.

**Why, and why this was not a user fork:** two worked precedents in the same codebase settle it. `GET /products` (slice 1.2) is a *list* read implemented as `Query<Product>().ToListAsync()` over documents, and `GET /carts/mine` (slice 3.5) is a *customer-keyed* read implemented as `Query<CartView>().Where(v => v.CustomerId == id)` over an inline projection. The "My Orders" list is exactly their composition (`FirstOrDefault` → `ToList`). A dedicated multi-stream `CustomerOrdersView` would duplicate state `OrderStatusView` already holds per order and add a projection to keep consistent, for no gain. A slim summary DTO would only be warranted if a field needed renaming (the reason `BrowseProducts` projects `Product` → `ProductCatalogView` is the `id`→`sku` rename); here nothing needs renaming, and reusing the view keeps the list row and the W4 detail screen on one contract. The minor cost — the list ships each order's `lines` over the wire — is negligible for a teaching storefront and buys end-to-end contract reuse.

**Alternative rejected:** a multi-stream `CustomerOrdersView` projection keyed by `customerId`. More moving parts, duplicated data, a second projection to maintain — over-ceremony the project does not earn.

### Decision 2 — Header-keyed `/orders/mine`, superseding the workshop's `?customerId=` sketch

The route is `GET /orders/mine` with identity in the `X-Customer-Id` header, **not** the `GET /orders?customerId=` query-string form the workshop Gap #3 names.

**Why:** it mirrors `GET /carts/mine` exactly — the only other customer-scoped read in the service — and rides the SPA identity seam (`useCurrentCustomer` → `X-Customer-Id`, ADR 009), the single Polecat-promotion swap point that swaps the header for a claim with call sites unchanged. It keeps identity out of the URL: a Customer cannot read another's orders by editing a query param (the identity-hygiene reasoning `/carts/mine` already used). The `?customerId=` form was a modeling-time sketch that predates the `/carts/mine` convention (slice 3.5, ADR 009). This was the one genuine fork in the slice — an outward API contract that also nudges the parked cart-identity-harmonization candidate — and it was **confirmed with the owner** before authoring; the workshop § 5.1 record gets a faithfulness note recording the supersession.

### Decision 3 — A non-unique customer index on `OrderStatusView`, mirroring the cart read-model index

`opts.Schema.For<OrderStatusView>().Index(x => x.CustomerId)` — a plain (non-unique) index.

**Why:** the customer-keyed query (`Where(v => v.CustomerId == id)`) is a read-model lookup by a non-id field; the index serves it. It is the exact mirror of `Program.cs:100`'s `Schema.For<CartView>().Index(x => x.CustomerId)` (the cart read model's customer index serving `GET /carts/mine`). Uniqueness is **not** wanted here — a Customer has many orders (unlike the cart's "one open cart per customer" invariant, which is a *write-side* unique partial index on the `Cart` aggregate, `Program.cs:92`). The order index is purely the read model's lookup affordance.

### Decision 4 — `IResult` with an inline blank-header guard, mirroring `ViewMyCart`

The endpoint returns `IResult`: `Results.BadRequest(...)` when the header is missing/blank, else `Results.Ok(orders)`.

**Why:** this is a faithful sibling of `ViewMyCartEndpoint` (`ViewMyCart.cs`), which returns `IResult` with an inline `if (string.IsNullOrWhiteSpace(customerId)) return Results.BadRequest(...)` guard before the query. The idiomatic Wolverine default for a guard is a separate `Validate` method returning `ProblemDetails`, but for a teaching codebase the stronger signal is that the new customer-keyed read reads as the twin of the existing one — same transport, same guard shape, same return type. Harmonizing the two onto a `Validate` shape (if desired) is a separate cross-cutting tidy, not this slice's job.

### Decision 5 — Newest-first ordering (`placedAt` descending)

The list is ordered `OrderByDescending(v => v.PlacedAt)`.

**Why:** the natural reading order for "My Orders" is most-recent-first (the wireframe mock and every commerce order-history screen). `placedAt` exists on every `OrderStatusView` from genesis (slice 025), so the ordering needs no new data. Ordering server-side keeps the contract honest (the SHALL specifies newest-first) rather than leaving it to client sort.

## Faithfulness notes (workshop divergences, for the in-PR amendment)

1. **Gap #3 is now shipped.** Workshop § 5.1 W4 records "'My Orders' list (`GET /orders?customerId=`) is Gap #3 — named and deferred"; this change ships it. A § 5.1 amendment (and the Narrative 005 Moment + "What the Customer does not yet see" flip) records it as built — authored **in this PR**, not deferred.
2. **The route shape is `/orders/mine`, not `?customerId=`** (Decision 2). The amendment records the supersession so the workshop's modeling-time sketch is not mistaken for the shipped contract.

## Risks / Trade-offs

- **[Each list item carries its `lines`]** Reusing `OrderStatusView` means the list ships every order's line items, which a slim summary DTO would omit (Decision 1). For a teaching storefront with a small order count this is negligible, and it buys end-to-end contract reuse (list and detail on one schema). If the payload ever matters, a `select`-projected summary is a localized follow-up.
- **[No pagination]** The list is unbounded. Round one has one demo customer with a handful of orders; an unbounded list is fine. A real catalog of orders would need pagination — named as a non-goal, not a hidden gap.
- **[Ephemeral dev streams]** Aspire Postgres is ephemeral (fresh DB each boot) and the integration suite seeds its own streams, so the new index applies on startup with no migration concern.

## Open Questions

*(none — the route-shape fork was confirmed with the owner before authoring; the projection-reuse and index choices were settled by the `BrowseProducts` / `ViewMyCart` precedents)*
