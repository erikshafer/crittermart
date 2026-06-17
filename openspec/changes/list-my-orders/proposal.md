# Proposal: List my orders — a customer-keyed order list (`GET /orders/mine`)

## Why

The storefront tracks **one** order at a time. W4 (`OrderStatusPage`) reads `GET /orders/{orderId}` and polls it to its terminal state, but a Customer who wants to see *all* their orders at once finds no such screen. Narrative 005 names this twice — Moment 5's closing line ("A Customer who wants to see all their orders at once finds no such screen yet") and the "What the Customer does not yet see" → "No 'My Orders' history" bullet — and the [pre-frontend endpoint audit](../../../docs/research/pre-frontend-endpoint-audit.md) logged it as **Gap #3** (named and deferred; single-order tracking covered the round-one storefront).

This change closes Gap #3: a customer-keyed read across the Customer's orders, plus the "My Orders" list screen that binds it. It is the leading remaining *feature* of the round-two storefront — it extends the journey from "track this order" to "see my orders."

The data already exists. Every placed order has an inline `OrderStatusView` document carrying `customerId`, `status`, `total`, and (slice 025) `placedAt`. The list is a **filtered query over those existing documents** — no new event, command, projection, or aggregate. This mirrors how `GET /carts/mine` resolves the Customer's open cart by identity over the existing `CartView` (slice 3.5), and how `GET /products` lists the catalog by querying `Product` documents (slice 1.2). The codebase already has the shape; this slice applies it to orders.

## What Changes

- **New read endpoint `GET /orders/mine`** (Orders service): resolves the Customer by identity in the **`X-Customer-Id` header** and returns every `OrderStatusView` whose `customerId` matches, ordered **newest-first** (`placedAt` descending). The full order view shape `{ id, customerId, status, lines, total, placedAt, cancelReason }` is returned per order (the existing `OrderStatusView` contract, reused). A Customer with no orders gets an empty list (`200 []`); a request with no identity header gets `400`.
- **A non-unique index on `OrderStatusView.CustomerId`** (`Orders/Program.cs`) serves the customer-keyed query — the exact mirror of the `CartView.CustomerId` read-model index (`Program.cs:100`).
- **No new event, command, projection, aggregate, or index-bearing read model** — `OrderStatusView` is already the canonical order read shape; the list is a `Query<OrderStatusView>().Where(v => v.CustomerId == id)` over it.
- **Frontend "My Orders" list screen** (`client/src/orders/`): a `/orders` route + `MyOrdersPage` rendering the customer's orders newest-first, each row linking into the W4 `/orders/$orderId` track screen; a customer-keyed query (`orderKeys.mine(customerId)` + `myOrdersQueryOptions`, mirroring `cartKeys.mine`); the response parsed through `z.array(OrderStatusViewSchema)` (the schema reused wholesale — Convention 2); and an AppShell "My Orders" nav link.

### Route-shape decision — `/orders/mine` (header-keyed), superseding the Gap #3 `?customerId=` sketch

The workshop names Gap #3 as `GET /orders?customerId=` (a query-string sketch from modeling time). This change ships it as **`GET /orders/mine`** (header-keyed) instead, consciously superseding that sketch. The decision is settled by precedent + ADR, not put to the owner as an open fork (it was confirmed with the owner before authoring): it mirrors `GET /carts/mine` exactly (the only existing customer-scoped read in the service), rides the SPA identity seam (`useCurrentCustomer` → `X-Customer-Id`, ADR 009 — the single Polecat-promotion swap point), and keeps identity out of the URL (a customer cannot read another's orders by editing a query param — the same identity-hygiene reasoning `/carts/mine` used). The workshop § 5.1 record gets a faithfulness note recording the supersession.

## Capabilities

### New Capabilities

*(none — the delta extends the existing `order-lifecycle` capability, per the one-capability-per-aggregate shape)*

### Modified Capabilities

- `order-lifecycle`: **1 ADDED requirement** (*List a customer's own orders*). The Order aggregate, its events, and the `OrderStatusView` projection are unchanged in behavior; this requirement adds a customer-keyed read over the documents the projection already maintains.

## Impact

- **Code**: `src/CritterMart.Orders/Features/ListMyOrders.cs` (new `GET /orders/mine` endpoint); `src/CritterMart.Orders/Program.cs` (one `Schema.For<OrderStatusView>().Index(x => x.CustomerId)` line). No change to `OrderStatusView`, any event, any handler, or the Order aggregate.
- **Tests**: `tests/CritterMart.Orders.Tests/ListMyOrdersTests.cs` (newest-first ordering; strict customer scoping; empty list `200 []`; missing-header `400`) — mirrors `ViewMyCartTests`.
- **Frontend**: `client/src/orders/` — `MyOrdersPage.tsx` + query/schema additions (`orderKeys.mine`, `myOrdersQueryOptions`, `fetchMyOrders`, `OrderListSchema`); `client/src/router.tsx` (`/orders` route); `client/src/components/AppShell.tsx` (nav link). Vitest specs for the schema + query + page.
- **No cross-BC impact**, no contract changes to existing endpoints, no broker topology changes, no new packages, no new projection. The read stays inside Orders, over the existing inline `OrderStatusView`.
- **Out of band (post-merge tidy)**: `openspec archive list-my-orders` (syncs the ADDED requirement into `openspec/specs/order-lifecycle/spec.md`, 9 → 10). The workshop § 5.1 faithfulness note (Gap #3 shipped + route-shape supersession) and the Narrative 005 Moment are authored **in this change's PR** (design-return content folded into the slice), not deferred.
