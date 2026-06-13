---
version: v1.0
status: Active
date: 2026-06-13
references:
  - docs/decisions/006-wolverine-http-per-service-no-bff.md
  - docs/decisions/009-polecat-deferred-for-round-one.md
  - docs/decisions/015-vite-react-frontend-stack.md
  - docs/decisions/016-frontend-full-pipeline-ui-first-class.md
  - docs/narratives/002-customer-browse-catalog.md
  - docs/narratives/004-customer-purchase.md
  - docs/workshops/001-crittermart-event-model.md
---

# Pre-Frontend Read-Model Endpoint Audit

> Compiled 2026-06-13, in the `chore: pre-frontend hardening` session, before the round-two
> frontend work begins. This is **research / decision-evidence**, not a decision and not a
> build order. It exists to feed the first frontend modeling session (the ADR 016 workshop
> amendment), where the gaps below get modeled as `event → projection → wireframe` view
> slices and only then implemented.

## Why this exists

[ADR 015](../decisions/015-vite-react-frontend-stack.md) commits the round-two storefront to a
Vite + React SPA that calls the three Wolverine.Http services **directly** (no BFF —
[ADR 006](../decisions/006-wolverine-http-per-service-no-bff.md)). [ADR 016](../decisions/016-frontend-full-pipeline-ui-first-class.md)
predicted that modeling the customer-facing screens "confirms (or exposes gaps in) the read
models the UI binds to (`ProductCatalogView`, `CartView`, `OrderStatusView`)." This audit runs
that check ahead of time against the **two customer journeys already authored** — browse
([Narrative 002](../narratives/002-customer-browse-catalog.md)) and purchase
([Narrative 004](../narratives/004-customer-purchase.md)) — so the first frontend session walks
in knowing which view slices it must model.

The guardrail from ADR 016 governs what happens next: an interaction that **reads a domain
fact** is a *view / query slice* and is modeled in the workshop before it is built. Every gap
below is a read of a domain fact. **No endpoint code was written in this session** — that is
deliberate, not unfinished.

## What exists today

Each customer-facing screen and the query endpoint it would bind to:

| Screen (from the journeys) | Read model | Endpoint today | Status |
| --- | --- | --- | --- |
| Catalog browse / listing | `ProductCatalogView` | `GET /products` (Catalog) — full view per product, keyed by SKU | ✅ Serves the screen |
| Product detail | `ProductCatalogView` | *(none)* — no `GET /products/{sku}` | ⚠️ Gap #2 (low) |
| Cart review | `CartView` | `GET /carts/{cartId}` (Orders) — by cart id | ⛔ Gap #1 (blocking) |
| Order tracking (one order) | `OrderStatusView` | `GET /orders/{orderId}` (Orders) | ✅ Serves the screen |
| Order history ("my orders") | `OrderStatusView` | *(none)* — no list-by-customer | ⚠️ Gap #3 (medium) |
| Orders/carts awaiting (operator) | todo-list projections | `GET /orders/awaiting-payment`, `GET /carts/awaiting-activity` | ✅ Exist (operator-facing, not customer storefront) |

## Gap #1 — open-cart-by-customer read (BLOCKING)

**The cart's write side and read side are keyed differently.** Every cart *command* is
customer-keyed and the server resolves the open cart — Narrative 004 is explicit: *"the commands
carry the customer and the SKU, never a cart id: which cart is theirs is the server's business."*
But the only cart *read* is `GET /carts/{cartId}` (`AddToCart.cs:64`), which needs a `cartId`.
The SPA only learns a `cartId` from an `AddToCart` **response** — so on a **cold load** (the
customer returns, fresh browser session, holding only the hardcoded customer id per
[ADR 009](../decisions/009-polecat-deferred-for-round-one.md)) it cannot render the cart-review
screen. This blocks Moment 1A (editing the cart) and Moment 2 (place order from the review
screen) of the purchase journey.

**The data layer already supports the fix.** `src/CritterMart.Orders/Program.cs:74` declares a
**unique computed index on `CartView.CustomerId` scoped to open carts** — the exact index a
"resolve the customer's one open cart" query needs. The read model is ready; only the endpoint
is missing.

**Recommended modeling.** A view/query slice — provisionally **slice 3.5, "View my open cart"** —
`CartView → GET /carts/mine?customerId=` (or a header-carried identity), returning the single
open cart or `404`. It adds **no new events**; it exposes an existing projection over an
existing index. This is the #1 input to the ADR 016 workshop amendment.

## Gap #2 — product-detail read (LOW)

No `GET /products/{sku}`. A product-detail page is conventional, but `GET /products` already
returns the **full** `ProductCatalogView` for every product (`BrowseProducts.cs:14-19`), so the
SPA can render a detail view from the list payload it already holds. The detail endpoint is a
nicety (deep-linkable URLs, a lighter payload), not a blocker.

**Recommended modeling.** Fold into the catalog browse-and-detail view slice the ADR 016
amendment already plans; implement only if the SPA wants server-side single-product fetch.

## Gap #3 — customer order-list / history (MEDIUM)

No list-by-customer for orders. Single-order tracking works — `PlaceOrder` returns the `orderId`
(`PlaceOrder.cs:85`), and the SPA can follow `GET /orders/{orderId}`. But an "order history"
landing (the customer returns and wants to see their orders) has no endpoint, and `OrderStatusView`
is keyed by `orderId` with no customer-scoped query exposed. The purchase journey's "watch it get
fulfilled" implies a customer returning to check status, so this surfaces in round two even if it
is not strictly day-one.

**Recommended modeling.** An order-tracking view slice in the ADR 016 amendment —
`OrderStatusView → GET /orders?customerId=` (list, newest first). Verify whether
`OrderStatusView` carries `CustomerId` (it should, from `OrderPlaced`); add a computed index if
the list query needs one. May be reasonably deferred to round-two-plus.

## Summary for the next session

1. **Model Gap #1 first** — it is the only blocking gap, and the index already exists. Provisional slice 3.5.
2. Gaps #2 and #3 fold into the catalog and order-tracking view slices the ADR 016 amendment already plans.
3. None of the three needs a new event — all are reads over existing projections. That is the expected shape for view slices (ADR 016's guardrail).
4. CORS is already wired (this session): the SPA's cross-origin calls to all three services will be allowed once the AppHost injects the frontend origin (`Cors:AllowedOrigins`).
