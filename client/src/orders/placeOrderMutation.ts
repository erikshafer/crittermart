import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { z } from "zod";

import { postCommand, useApiContext } from "@/api/client";
import { serviceUrls } from "@/config";
import { cartKeys } from "@/cart/cartQueries";

// Place-order — the storefront's checkout command, and the one mutation that DELIBERATELY BREAKS the cart's
// optimistic-UI template (frontend SKILL Convention 3, its named exception). The three cart commands fake
// their outcome locally (`onMutate`) because a cart edit is knowable; placing an order cannot be, because it
// kicks off a cross-bounded-context process — reserve stock in Inventory over RabbitMQ, then authorize
// (stubbed) payment — whose outcome the SPA does not yet know. So this hook has NO `onMutate` guess: it fires
// the command, and on success invalidates the cart (the badge resets to `Cart (0)`) and navigates to W3,
// which reads the server's honest `awaiting_confirmation` status. Optimism stops here.
//
// It lives in `orders/` (locked decision 2): it *produces* an Order — mirroring backend
// Orders/Features/PlaceOrder — so it belongs with the order read it kicks off, not with the cart commands.
// The cart-badge reset is just the `invalidate(cartKeys.mine)` it does on success.

// The 201 response body, parsed at the boundary like every wire surface (Convention 2). `POST /orders` returns
// **only `{ orderId }`** — NOT a status (the placed order's status is read separately via GET /orders/{orderId}
// on the W3 screen). This is the load-bearing contract detail: the place response cannot carry status, so the
// confirmation screen must follow up with the OrderStatusView read.
export const PlaceOrderResponseSchema = z.object({ orderId: z.string() });

// The place-order mutation hook. Identity transport (frontend SKILL Convention 4): `POST /orders` is
// **bearer-keyed** — identity rides the `Authorization: Bearer` token the shared client sets from the auth
// seam (ADR 023 hard cutover; the `sub` claim is the trust boundary), matching every other customer-keyed
// endpoint (GET /orders/mine, GET /carts/mine). The body is empty: the order's contents are NOT sent (the
// server resolves the customer's open cart, Narrative 005 Moment 4).
//
// Slice 6.2 adds ONE optional variable: `mutate(couponCode)`. When the customer applied a coupon on W2, its
// code rides checkout as the `?couponCode=` QUERY parameter the DCB redemption path reads (slice 6.3 — a
// query param, not a body field, so the bodyless checkout contract is preserved; retro 039). `mutate()` with
// no argument is the unchanged no-coupon checkout. The customer still comes from the seam either way.
export function usePlaceOrder() {
  const ctx = useApiContext();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: (couponCode?: string) => {
      const url = couponCode
        ? `${serviceUrls.ordersUrl}/orders?couponCode=${encodeURIComponent(couponCode)}`
        : `${serviceUrls.ordersUrl}/orders`;
      return postCommand(url, {}, ctx, PlaceOrderResponseSchema);
    },

    // onSuccess — NOT onMutate. There is nothing optimistic to apply; the order is placed only once the server
    // confirms it. Reset the cart (the checked-out cart is no longer the customer's open cart → `GET /carts/mine`
    // 404 → `Cart (0)`), then navigate to the confirmation screen keyed by the returned orderId. A failure
    // (409 NoOpenCart / CartEmpty) surfaces in `isError` with no navigate — the cart stays put.
    onSuccess: ({ orderId }) => {
      void queryClient.invalidateQueries({ queryKey: cartKeys.mine(ctx.customerId) });
      void navigate({ to: "/orders/$orderId/confirmation", params: { orderId } });
    },
  });
}
