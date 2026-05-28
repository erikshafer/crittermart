## Why

Slice 1.1 lets the Seller publish products onto the storefront, but customers cannot yet *see* them. Workshop 001 slice 1.2 (`Browse and view products`) is the Customer's first interaction with CritterMart and the **read side** of the "when CRUD is fine" Catalog example. It is also the project's first **query slice** — read-only, no command, no events, no failure path. Narrative 002 (the Customer's catalog-browsing journey) is this proposal's human-readable sibling; the two must agree.

## What Changes

- Expose the published products through the `ProductCatalogView` as a browsable listing the storefront reads (e.g., `GET /products`).
- Each listed product carries its SKU, name, description, and current price — enough for the customer to browse the shelf and read what each item is.
- Read-only: browsing records no event and modifies no product. There is no failure path — a query slice.
- No cross-bounded-context integration: the listing reads only Catalog's own document store. No stock level is fetched from Inventory (per the context map, Catalog has no round-one BC integration).

## Capabilities

### New Capabilities

<!-- None. Slice 1.2 extends the existing product-catalog capability. -->

### Modified Capabilities

- `product-catalog`: adds a **browse** requirement — exposing the published products through the catalog read model so customers can view them. Slice 1.1 introduced this capability's publish and SKU-uniqueness requirements; slice 1.2 adds browse. This is the first accumulation onto the one-capability-per-bounded-context model the slice 1.1 proposal established.

## Impact

- **HTTP surface:** a read-only Wolverine.Http endpoint (e.g., `GET /products`) returning the `ProductCatalogView` listing; no synchronous service-to-service calls.
- **Persistence:** read-only over the existing `Product` document store. `ProductCatalogView` is a **query over `Product` documents, not a new projection** (per slice 1.1 `design.md` Decision 1 — ADR 008's inline-projection rule is scoped to event-sourced aggregates, which Catalog is not). No schema change.
- **Identity:** browsing the public catalog is not gated; the stubbed customer ID (ADR 009) flows through requests but does not affect the listing.
- **Out of scope (mirrors Narrative 002's non-events):** no live stock availability on the listing, no recommendations or cross-sell, no real-time price push. A price the customer sees is as-of-request; the Cart snapshots price later at add-to-cart time.
- **Downstream artifacts:** `design.md` and `tasks.md` are deferred to the slice 1.2 implementation session per CritterMart's one-artifact-class-per-session pipeline (ADR 011).
