## Context

Slice 1.2 adds a read-only `GET /products` endpoint that surfaces the published catalog. The Catalog service, its Marten document store, and the `ProductCatalogView` read shape already exist (slice 1.1); this slice writes only the query and the endpoint. It is the near-trivial end of ADR 011's design-grain spectrum — slice 1.1's `design.md` carried genuine decisions (audit stream, duplicate rejection, OTel deferral); this one records only how the already-decided read shape is realized, and is kept deliberately short.

Cross-cutting decisions are inherited by reference: Wolverine.Http per service (**ADR 006**), Marten document store / no event sourcing for Catalog (**ADR 002**, Workshop 001 § 2), and the stack/codegen posture (**ADR 012**). The load-bearing inheritance is slice 1.1 `design.md` **Decision 1**: `ProductCatalogView` is a query over `Product` documents, **not** a Marten `IProjection`. This slice realizes that decision.

## Goals / Non-Goals

**Goals:**
- A `GET /products` endpoint returning every published product as a `ProductCatalogView` (SKU, name, description, current price).
- Honor the proposal's `sku` field by projecting through `ProductCatalogView` (which renames the document `Id` → `Sku`).

**Non-Goals:**
- No detail endpoint, paging, filtering, sorting, or search (not in the contract; Narrative 002).
- No new projection, document, or write path. No failure path (query slice).
- No live stock, recommendations, or real-time price (Narrative 002 non-events).

## Decisions

### 1. Realize `ProductCatalogView` as a materialized query, projected — not raw-document streaming

The endpoint queries the `Product` document store (`IQuerySession.Query<Product>()`), materializes the results, and projects each to `ProductCatalogView` via the existing `FromDocument`.

*Why X over Y:* `ProductCatalogView` renames the document's `Id` to `Sku` — the proposal's response contract uses `sku`, not the raw document `id`. Streaming the `Product` documents directly to JSON (the Wolverine.Http + Marten streaming path) would serialize `Id`, leaking the document key shape and breaking the `sku` contract. Projecting through the existing view record keeps the response faithful to the proposal and reuses the read shape slice 1.1 already defined. At round-one single-seller scale, materializing the full listing is unproblematic.

### 2. Read-only `IQuerySession`

The endpoint takes an injected `IQuerySession` (read-only), not `IDocumentSession`.

*Why:* it signals the slice's read-only nature, needs no transaction, and the `AutoApplyTransactions` middleware is a no-op for a pure read. Matches the workshop's `*(query)*` marking.

### 3. Empty catalog returns an empty list

When no products are published, `GET /products` returns `200` with an empty array — not `404`.

*Why:* "the catalog is empty" is a valid browse result, not a missing resource. The customer asked for the listing and received it; it happens to have zero items.

## Risks / Trade-offs

- **Unbounded listing (no paging)** → acceptable for a round-one single-seller catalog; paging is a future concern explicitly out of scope (Narrative 002). Revisit only if the catalog grows beyond a trivially-returnable size.
- **In-memory projection of all `Product` documents** → fine at round-one scale; if the catalog ever grows large, a compiled query or server-side projection-to-view is the optimization, but that is premature now.
