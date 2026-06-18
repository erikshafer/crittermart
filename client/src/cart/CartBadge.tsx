import { Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";

import { cartLineCountQueryOptions } from "./cartQueries";

// The header cart badge — "Cart (N)", where N is the total item count (sum of quantities) across all cart lines. It lives
// in the app chrome (AppShell renders it on every route) but owns its own query subscription so only the badge
// re-renders when the count changes, not the whole shell — the count is `select`-derived (cartQueries.ts), so
// the subscription wakes only on a count change. Sharing the cart query key, it rides the same single
// `GET /carts/mine` as the cart page. A no-open-cart 404 resolves to `null` → 0, so the badge reads "Cart (0)"
// rather than erroring; while the first read is in flight `data` is `undefined` → also 0. Links to W2.
export function CartBadge() {
  const ctx = useApiContext();
  const { data: count } = useQuery(cartLineCountQueryOptions(ctx));

  return (
    <Link to="/cart" className="text-sm font-medium text-muted-foreground hover:text-foreground">
      Cart ({count ?? 0})
    </Link>
  );
}
