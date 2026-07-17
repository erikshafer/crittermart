import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";

import { deleteCommand, postCommand, useApiContext } from "@/api/client";
import { serviceUrls } from "@/config";

import { cartKeys } from "./cartQueries";
import type { CartLine, CartView } from "./cartSchema";

// The cart's command layer — the storefront's optimistic-UI mutations (frontend SKILL Convention 3). Each is
// a *thin three-callback hook* paired with a *pure merge sibling* — the merge (the hard part: the optimistic
// guess must follow the server's own rule so it reconciles cleanly instead of flickering) is extracted as a
// pure, unit-tested function mirroring the Cart aggregate; the hook wires it into TanStack Query's
// onMutate/onError/onSettled. Add-to-cart established the template (slice 3.1); the two W2 edits below
// (3.2 remove, 3.3 change-qty) are its 2nd + 3rd realizations; 4.1 place-order adds its hook here next —
// where optimism deliberately *stops* (a cross-BC outcome the SPA can't guess). All three cart commands here
// target CartView (its cache key, its optimistic merge, its rollback), so they live with the cart feature —
// mirroring the backend, where they are Orders/Shopping concerns, not Catalog.

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

// The add-to-cart mutation hook. Identity transport (route harmonized in change 032; ADR 023 auth cutover):
// the command is **header-keyed** — `POST /carts/mine/items`, with the customer resolved server-side from the
// `sub` claim of the `Authorization: Bearer` token the shared client always sets (from the useCurrentCustomer
// seam). The route no longer carries identity, matching the cart READ (`/carts/mine`). (`ctx.customerId` is
// still used for the client-side cache key, not the URL.)
export function useAddToCart() {
  const ctx = useApiContext();
  const queryClient = useQueryClient();
  const cartKey = cartKeys.mine(ctx.customerId);

  return useMutation({
    mutationFn: (command: AddToCartCommand) =>
      postCommand(
        `${serviceUrls.ordersUrl}/carts/mine/items`,
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

// ── Remove from cart (slice 3.2) — the SPA's first DELETE ───────────────────────────────────────────────

// The remove-item command. Identity rides the `Authorization: Bearer` token and the SKU rides the route
// (DELETE /carts/mine/items/{sku}), so the command carries only the SKU — there is no request body, and the
// response is 204 (handled by deleteCommand).
export interface RemoveCartItemCommand {
  sku: string;
}

// Pure optimistic merge for remove — drops the SKU's line. Mirrors the Cart aggregate (RemoveCartItem.cs):
// lines are SKU-keyed, so removal is an exact filter, and removing the *last* line leaves an empty-but-open
// cart (the backend keeps it open — design.md decision 5), NOT null, so the screen renders empty rather than
// vanishing. A null cart (nothing to remove from — never reached from a rendered row) is a no-op.
export function removeLineFromCart(cart: CartView | null, sku: string): CartView | null {
  if (!cart) return cart;
  return { ...cart, lines: cart.lines.filter((l) => l.sku !== sku) };
}

// The remove-item mutation hook. Same header-keyed identity transport as useAddToCart (the `Authorization:
// Bearer` token set by the shared client; only the {sku} rides the path) and the same three-callback optimistic
// shape: the line disappears the instant [x] is tapped, then reconciles against the refetched CartView.
export function useRemoveCartItem() {
  const ctx = useApiContext();
  const queryClient = useQueryClient();
  const cartKey = cartKeys.mine(ctx.customerId);

  return useMutation({
    mutationFn: (command: RemoveCartItemCommand) =>
      deleteCommand(`${serviceUrls.ordersUrl}/carts/mine/items/${command.sku}`, ctx),

    onMutate: async (command) => {
      await queryClient.cancelQueries({ queryKey: cartKey });
      const previous = queryClient.getQueryData<CartView | null>(cartKey);
      queryClient.setQueryData<CartView | null>(cartKey, (current) =>
        removeLineFromCart(current ?? null, command.sku),
      );
      return { previous };
    },

    onError: (_error, _command, context) => {
      queryClient.setQueryData(cartKey, context?.previous);
    },

    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: cartKey });
    },
  });
}

// ── Change quantity (slice 3.3) ─────────────────────────────────────────────────────────────────────────

// The change-quantity command. The {sku} rides the route and identity the `Authorization: Bearer` token; the new
// ABSOLUTE quantity rides the body as `{ newQuantity }` — matching `ChangeCartItemQuantity(int NewQuantity)`,
// which System.Text.Json binds case-insensitively. Not a delta: the UI computes N±1 and sends the result.
// The backend rejects <= 0, but the [-] stepper is disabled at quantity 1 (locked decision 2), so the SPA
// never sends a non-positive value. Returns 204 (no body).
export interface ChangeCartItemQuantityCommand {
  sku: string;
  newQuantity: number;
}

// Pure optimistic merge for change-quantity — rewrites the line's quantity to the ABSOLUTE new value, leaving
// the snapshotted name/price untouched. Mirrors the Cart aggregate (ChangeCartItemQuantity.cs): only "how
// many" changes; a quantity change never re-prices. A SKU not in the cart, or a null cart, is a no-op (the
// control is only rendered for an existing line).
export function setLineQuantity(
  cart: CartView | null,
  sku: string,
  newQuantity: number,
): CartView | null {
  if (!cart) return cart;
  return {
    ...cart,
    lines: cart.lines.map((l) => (l.sku === sku ? { ...l, quantity: newQuantity } : l)),
  };
}

// The change-quantity mutation hook. Same header-keyed transport (`Authorization: Bearer`; only {sku} on the path) +
// three-callback optimistic shape; the body is `{ newQuantity }` and the 204 response means `postCommand` is
// called WITHOUT a schema (nothing to parse).
export function useChangeCartItemQuantity() {
  const ctx = useApiContext();
  const queryClient = useQueryClient();
  const cartKey = cartKeys.mine(ctx.customerId);

  return useMutation({
    mutationFn: (command: ChangeCartItemQuantityCommand) =>
      postCommand(
        `${serviceUrls.ordersUrl}/carts/mine/items/${command.sku}/quantity`,
        { newQuantity: command.newQuantity },
        ctx,
      ),

    onMutate: async (command) => {
      await queryClient.cancelQueries({ queryKey: cartKey });
      const previous = queryClient.getQueryData<CartView | null>(cartKey);
      queryClient.setQueryData<CartView | null>(cartKey, (current) =>
        setLineQuantity(current ?? null, command.sku, command.newQuantity),
      );
      return { previous };
    },

    onError: (_error, _command, context) => {
      queryClient.setQueryData(cartKey, context?.previous);
    },

    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: cartKey });
    },
  });
}
