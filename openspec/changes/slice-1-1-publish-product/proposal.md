## Why

CritterMart's storefront has no products until the Seller can publish them. Workshop 001 slice 1.1 (`PublishProduct`) is the first vertical slice and the blueprint-architecture step: it stands up the Catalog service skeleton and proves the per-slice triangle (narrative → OpenSpec proposal → implementation prompt) end-to-end before the harder cross-BC Orders slices. Catalog is deliberately the "when CRUD is fine" teaching example — a Marten document store, with lifecycle moments captured as events for audit rather than for state reconstruction.

## What Changes

- Introduce the `PublishProduct` command: the Seller publishes a product onto the storefront catalog with a SKU, name, description, and price.
- Record a `ProductPublished` lifecycle moment for audit (not state reconstruction — the `Product` document is the source of truth).
- Persist a `Product` document and surface it through the `ProductCatalogView` read model that the customer-facing storefront listing reads.
- Reject a duplicate SKU with `ProductAlreadyPublished` (idempotent failure — no second document, no shadow lifecycle moment).
- Stand up the Catalog service skeleton (Wolverine.Http entry, Marten document store, inline projection) as the bootstrap for this and subsequent Catalog slices.

No cross-bounded-context integration: per the context map, Catalog has no outbound BC-level events in round one. Publishing a product fires no message to Orders or Inventory.

## Capabilities

### New Capabilities

- `product-catalog`: Publishing products to the storefront catalog and exposing them through the catalog read model. Slice 1.1 introduces the publish requirement; later Catalog slices (1.2 browse, 1.3 change price) will add requirements to this same capability.

### Modified Capabilities

<!-- None. This is the first capability in the project; no existing specs/ to modify. -->

## Impact

- **New service:** the Catalog service under `src/` (first code in the repo), backed by Marten on the shared PostgreSQL with a Catalog-owned schema.
- **Persistence:** a `Product` document plus an inline `ProductCatalogView` projection (no async daemon — per ADR 008, inline projections for round one).
- **HTTP surface:** a Wolverine.Http endpoint accepting `PublishProduct`; no synchronous service-to-service calls (none needed — Catalog is isolated in round one).
- **Identity:** the Seller/operator identity is stubbed per ADR 009; the command carries the actor as if from a real identity system.
- **Downstream artifacts:** this proposal's `product-catalog` capability is the contract the `specs/product-catalog/spec.md` delta satisfies; `design.md` and `tasks.md` are deferred to the implementation session per CritterMart's one-artifact-class-per-session pipeline.
