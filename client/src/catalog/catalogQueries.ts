import { queryOptions } from "@tanstack/react-query";

import { fetchParsed, type RequestContext } from "@/api/client";
import { serviceUrls } from "@/config";

import { ProductCatalogListSchema, type ProductCatalogView } from "./catalogSchema";

// The catalog's TanStack Query layer — the W1 listing's read, against the SECOND service (Catalog). Mirrors
// the cart's `queryOptions` + key-factory idiom (client/src/cart/cartQueries.ts), the W2 precedent.

// Query-key factory (tanstack `qk-factory-pattern`). The catalog list is **public** — Catalog gates nothing
// on identity — so unlike `cartKeys.mine(customerId)`, the catalog key carries **no customer id**: the result
// does not depend on who is asking (`qk-include-dependencies` keys by the result's *true* dependencies, and
// the product list has none beyond "the catalog"). The `Authorization: Bearer` token still rides along when the
// shopper is signed in (the shared client attaches it), but it is not part of the cache key because it does not
// vary the result — and Catalog's public reads work signed-out, with no auth header at all.
export const catalogKeys = {
  all: ["catalog"] as const,
  products: () => [...catalogKeys.all, "products"] as const,
};

// The product-listing query the W1 BrowsePage binds. `GET /products` → the parsed array; an empty catalog is
// `[]` (200), which the page renders as its empty state. A failure (5xx / network) surfaces in `isError`.
// `ctx` is threaded through purely so the boundary fetch sets the identity header (Convention 4), even though
// the catalog read ignores it.
export function productsQueryOptions(ctx: RequestContext) {
  return queryOptions({
    queryKey: catalogKeys.products(),
    queryFn: () => fetchProducts(ctx),
  });
}

// Standalone (not inlined in `queryFn`) so a test can drive it directly with a literal context + mocked fetch.
export async function fetchProducts(ctx: RequestContext): Promise<ProductCatalogView[]> {
  return await fetchParsed(`${serviceUrls.catalogUrl}/products`, ProductCatalogListSchema, ctx);
}
