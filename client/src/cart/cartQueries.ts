import { queryOptions } from "@tanstack/react-query";

import { fetchParsed, NotFoundError, type RequestContext } from "@/api/client";
import { serviceUrls } from "@/config";

import { CartViewSchema, type CartView } from "./cartSchema";

// The cart's TanStack Query layer — the FIRST `queryOptions` factory in the storefront, the precedent W1
// (catalog) and W4 (orders) reuse. Two named factories share one cache entry (the same `queryKey`), so the
// header badge and the cart page trigger a single `GET /carts/mine` and read the same `CartView`.

// Query-key factory (tanstack `qk-factory-pattern`): centralized, hierarchical, dependency-complete. The cart
// is resolved BY the customer (identity rides the X-Customer-Id header, not the URL), so the customer id is a
// real dependency of the result and belongs in the key — if the dev identity ever switches, the cache keys a
// different cart rather than serving the wrong one (`qk-include-dependencies`).
export const cartKeys = {
  all: ["cart"] as const,
  mine: (customerId: string) => [...cartKeys.all, "mine", customerId] as const,
};

// Fetch the customer's open cart, mapping the domain-empty case to `null` instead of an error. Standalone
// (not inlined in `queryFn`) so a test can drive it directly with a literal context + mocked fetch.
//
// `GET /carts/mine`: 200 → the parsed `CartView`; 404 → `null` ("this customer has no open cart" — a domain
// state, render empty, NOT a failure; frontend SKILL Convention 4). A 400 (the seam misfired, no identity
// header) or any other non-2xx is a genuine error and rethrows, surfacing in the query's `isError`.
export async function fetchMyCart(ctx: RequestContext): Promise<CartView | null> {
  try {
    return await fetchParsed(`${serviceUrls.ordersUrl}/carts/mine`, CartViewSchema, ctx);
  } catch (error) {
    if (error instanceof NotFoundError) {
      return null;
    }
    throw error;
  }
}

// The cart query the W2 page binds. `data` is `CartView | null` — `null` is the empty-cart domain state, which
// the page renders identically to a cart whose `lines` are `[]`.
export function cartQueryOptions(ctx: RequestContext) {
  return queryOptions({
    queryKey: cartKeys.mine(ctx.customerId),
    queryFn: () => fetchMyCart(ctx),
  });
}

// Stable, module-scope selector for the header badge: it needs ONLY the total item count, so deriving it
// via `select` re-renders the badge only when the count changes, not on every cart-field change (tanstack
// `perf-select-transform`). Sums `quantity` across all lines so "Cart (N)" reflects the true item count
// (e.g. 2× plush + 3× newt → 5), not the number of distinct SKUs; `null` (no open cart) → 0.
export function selectCartLineCount(cart: CartView | null): number {
  return cart?.lines.reduce((sum, line) => sum + line.quantity, 0) ?? 0;
}

// The same cart query projected to just the badge count — same `queryKey`, so it shares the cache entry and
// the single in-flight fetch with `cartQueryOptions`; only the `select` differs.
export function cartLineCountQueryOptions(ctx: RequestContext) {
  return queryOptions({
    queryKey: cartKeys.mine(ctx.customerId),
    queryFn: () => fetchMyCart(ctx),
    select: selectCartLineCount,
  });
}
