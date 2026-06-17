import { queryOptions } from "@tanstack/react-query";

import { fetchParsed, type RequestContext } from "@/api/client";
import { serviceUrls } from "@/config";

import {
  OrderListSchema,
  OrderStatusViewSchema,
  type OrderList,
  type OrderStatusView,
} from "./orderSchema";

// The orders' TanStack Query layer — the W3 confirmation read (and, when W4 lands, the tracking read), against
// the THIRD service surface (Orders' `OrderStatusView`). Mirrors the cart's `queryOptions` + key-factory idiom
// (client/src/cart/cartQueries.ts), the W2 precedent.

// Query-key factory (tanstack `qk-factory-pattern`). An order is resolved BY its id — not by the customer
// (the order-status read carries no identity in the route or a header that varies the result), so the cache
// keys on the `orderId`, the result's one true dependency (`qk-include-dependencies`). This is the contrast
// with `cartKeys.mine(customerId)`: the cart is "the customer's open one" (customer-keyed); an order is a
// specific, immutable-identity thing (id-keyed). When W4 polls the same order, it shares this key.
export const orderKeys = {
  all: ["order"] as const,
  detail: (orderId: string) => [...orderKeys.all, "detail", orderId] as const,
  // The "My Orders" list is resolved BY the customer (identity in the X-Customer-Id header, not the URL), so
  // the customer id is the result's true dependency and belongs in the key — mirroring `cartKeys.mine`, and the
  // contrast with `detail` above (an order is id-keyed; the list is customer-keyed).
  mine: (customerId: string) => [...orderKeys.all, "mine", customerId] as const,
};

// Fetch a single order's status view. Standalone (not inlined in `queryFn`) so a test can drive it directly
// with a literal context + mocked fetch.
//
// `GET /orders/{orderId}`: 200 → the parsed `OrderStatusView`; a `404` here is a GENUINE error — unlike
// `GET /carts/mine` (where 404 = "no open cart", a domain-empty state mapped to null), an order the SPA just
// placed and navigated to MUST exist, so a 404 means something is wrong (a stale/bad id, or the inline
// projection didn't land). `fetchParsed` throws `NotFoundError` on 404; we let it propagate so it surfaces in
// the query's `isError`, rather than masking a real failure as empty data.
export async function fetchOrder(orderId: string, ctx: RequestContext): Promise<OrderStatusView> {
  return await fetchParsed(`${serviceUrls.ordersUrl}/orders/${orderId}`, OrderStatusViewSchema, ctx);
}

// The order-status query the W3 confirmation screen binds (and the W4 tracking screen will reuse, adding a
// `refetchInterval` poll — ADR 015 R5, status converges by refetch, not a socket). `ctx` is threaded so the
// boundary fetch sets the identity header (Convention 4), even though the order read resolves by id.
export function orderQueryOptions(orderId: string, ctx: RequestContext) {
  return queryOptions({
    queryKey: orderKeys.detail(orderId),
    queryFn: () => fetchOrder(orderId, ctx),
  });
}

// Fetch the customer's orders, newest-first — the "My Orders" list (`GET /orders/mine`; workshop Gap #3). Like
// `fetchMyCart`, it is customer-keyed: identity rides the X-Customer-Id header (Convention 4), so the result
// depends on the customer. Unlike the cart read, an empty order history is a `200 []`, NOT a `404` — so there
// is no NotFoundError mapping; `fetchParsed` returns the parsed (possibly empty) array, and any non-2xx is a
// genuine error. Standalone (not inlined in `queryFn`) so a test can drive it with a literal context + mocked
// fetch.
export async function fetchMyOrders(ctx: RequestContext): Promise<OrderList> {
  return await fetchParsed(`${serviceUrls.ordersUrl}/orders/mine`, OrderListSchema, ctx);
}

// The My Orders list query the `MyOrdersPage` binds. Keyed by the customer (the list's one true dependency), so
// a dev-identity switch keys a different list rather than serving the wrong one (`qk-include-dependencies`).
export function myOrdersQueryOptions(ctx: RequestContext) {
  return queryOptions({
    queryKey: orderKeys.mine(ctx.customerId),
    queryFn: () => fetchMyOrders(ctx),
  });
}
