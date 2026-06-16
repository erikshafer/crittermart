import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";

import { postCommand, useApiContext } from "@/api/client";
import { serviceUrls } from "@/config";

import { cartKeys } from "./cartQueries";
import type { CartLine, CartView } from "./cartSchema";

// The cart's command layer — the storefront's FIRST mutation and FIRST optimistic-UI (frontend SKILL
// Convention 3). Add-to-cart is *triggered from* the W1 catalog screen but *targets* the cart (its cache key,
// its optimistic merge, its rollback are all CartView), so it lives with the cart feature — mirroring the
// backend, where `AddToCart` is an Orders/Shopping concern, not Catalog. The W2 edit commands (3.2 remove,
// 3.3 change-qty) and 4.1 place-order add their hooks here next, each copying this three-callback template.

// The add-to-cart command body. Matches `AddToCart(string Sku, int Quantity, ProductSnapshot ProductSnapshot)`
// exactly — System.Text.Json binds these camelCase keys case-insensitively. The field name **must** be
// `productSnapshot`: retro 018 found sending `snapshot` binds the parameter to null and 500s the inline
// projection. Product name + price are SNAPSHOTTED from the loaded listing (Narrative 005 Moment 2 — product
// data reaches the cart only via the SPA snapshot, never a Catalog↔Orders call).
export interface AddToCartCommand {
  sku: string;
  quantity: number;
  productSnapshot: { name: string; price: number };
}

// The 201 response body, parsed at the boundary like every wire surface (Convention 2). `AddToCartResponse`
// hands back the `cartId`; the optimistic flow doesn't need it (onSettled refetches `/carts/mine` by identity),
// but parsing validates the command succeeded with a well-formed response and catches response-shape drift.
export const AddToCartResponseSchema = z.object({ cartId: z.string() });

// Pure optimistic merge — the heart of the optimistic update, extracted so the hard part (merge rules) is
// unit-testable without React-Query timing. Mirrors the cart aggregate's own rule (AddToCart.cs): a SKU is
// exactly one line, so re-adding a SKU **sums quantity** (keeping the existing snapshot name/price — quantity
// changes never re-price); a new SKU **appends**; no open cart yet seeds a fresh one. Because the guess
// follows the server's rule, it converges cleanly on `onSettled` instead of flickering.
export function addLineToCart(cart: CartView | null, line: CartLine, customerId: string): CartView {
  const base: CartView = cart ?? {
    // A synthetic shell for the cold first-add (no open cart yet). `id` is a placeholder the SPA never reads
    // optimistically; `onSettled`'s refetch replaces this whole guess with the server's real CartView.
    id: "optimistic",
    customerId,
    isOpen: true,
    lines: [],
    lastActivityAt: new Date().toISOString(),
  };

  const alreadyInCart = base.lines.some((l) => l.sku === line.sku);
  const lines = alreadyInCart
    ? base.lines.map((l) =>
        l.sku === line.sku ? { ...l, quantity: l.quantity + line.quantity } : l,
      )
    : [...base.lines, line];

  return { ...base, lines };
}

// The add-to-cart mutation hook. Identity transport (locked decision 1, prompt 019): the command is
// **route-keyed** — customerId from the useCurrentCustomer seam is interpolated into the path
// (`POST /carts/{customerId}/items`); the X-Customer-Id header rides along (the shared client always sets it)
// but the route is authoritative server-side. This diverges from the header-keyed cart READ (`/carts/mine`) —
// a divergence logged as a future harmonization tidy, not fixed in this screen-only slice.
export function useAddToCart() {
  const ctx = useApiContext();
  const queryClient = useQueryClient();
  const cartKey = cartKeys.mine(ctx.customerId);

  return useMutation({
    mutationFn: (command: AddToCartCommand) =>
      postCommand(
        `${serviceUrls.ordersUrl}/carts/${ctx.customerId}/items`,
        command,
        ctx,
        AddToCartResponseSchema,
      ),

    // onMutate — the optimistic beat. Cancel in-flight cart refetches (so a late GET can't overwrite the
    // guess), snapshot the current cache for rollback, then apply the guess: the badge (a `select` off this
    // same key) bumps the instant the line lands. Returns the snapshot as context for onError.
    onMutate: async (command) => {
      await queryClient.cancelQueries({ queryKey: cartKey });
      const previous = queryClient.getQueryData<CartView | null>(cartKey);

      const optimisticLine: CartLine = {
        sku: command.sku,
        quantity: command.quantity,
        name: command.productSnapshot.name,
        price: command.productSnapshot.price,
      };
      queryClient.setQueryData<CartView | null>(cartKey, (current) =>
        addLineToCart(current ?? null, optimisticLine, ctx.customerId),
      );

      return { previous };
    },

    // onError — roll the guess back to the snapshot. (onSettled's invalidate then refetches the truth.)
    onError: (_error, _command, context) => {
      queryClient.setQueryData(cartKey, context?.previous);
    },

    // onSettled — reconcile. Invalidate the cart key so the optimistic guess converges on the re-fetched
    // CartView: the read model, never the guess, is the source of truth (Convention 3).
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: cartKey });
    },
  });
}
