import { z } from "zod";

// CartViewSchema — the FIRST per-read-model Zod schema (frontend SKILL Convention 2). It is the SPA's
// copy of the `CartView` contract that Orders' `GET /carts/mine` returns (slice 3.5,
// src/CritterMart.Orders/Cart/CartView.cs). It is hand-written, NOT generated from the backend: when the
// service's response shape changes, this schema is updated in the same PR that consumes the change.
//
// **Wire casing is camelCase.** Wolverine.Http serializes with System.Text.Json *web defaults* and Orders
// applies no `PropertyNamingPolicy` override, so the C# `CartView`/`CartLine` PascalCase properties land on
// the wire camelCased: `customerId`, `isOpen`, `lastActivityAt`, and per line `sku`/`quantity`/`name`/`price`.
// `decimal Price` serializes as a JSON **number** (not a string) and `DateTimeOffset LastActivityAt` as an
// ISO-8601 **string**. (Confirmed against the live response during the W2 browser/OTel verification.)
//
// **Default `.strip()`, deliberately — not `.strict()`.** The boundary parse must catch the drift that breaks
// the SPA: a field it reads going missing or changing type (a `lines` that vanished, a `price` that became a
// string) throws here, loud and located, rather than surfacing as a silent `undefined` three components deep.
// It must NOT break on a *benign additive* change — Orders adding a new `CartView` property the SPA doesn't
// read. Default strip gives exactly that: required known fields are validated; unknown future fields are
// dropped, keeping the contract forward-compatible. `.strict()` would reject the additive case and is wrong here.

// One cart line — a SKU at the quantity and the name/price snapshotted when first added (the snapshot price
// stays authoritative; quantity changes never re-price). Counts and money are inherently non-negative, so the
// schema says so — a sign-flip is drift worth catching.
export const CartLineSchema = z.object({
  sku: z.string(),
  quantity: z.number().int().nonnegative(),
  name: z.string(),
  price: z.number().nonnegative(),
});

// The readable cart. The W2 screen consumes `lines` (rows + derived total) and, for the badge, the line count;
// `id`/`customerId`/`isOpen`/`lastActivityAt` are present-but-unused by the view, yet still modeled so the
// boundary parse validates the whole payload and catches drift in the unread fields too. `lastActivityAt` is
// kept a loose `z.string()` (an ISO timestamp the SPA never parses into a Date) — enough to catch a type drift
// without over-strict ISO validation that could reject a valid-but-unusual server format.
export const CartViewSchema = z.object({
  id: z.string(),
  customerId: z.string(),
  isOpen: z.boolean(),
  lines: z.array(CartLineSchema),
  lastActivityAt: z.string(),
});

// `z.infer` over a hand-typed interface (zod `type-use-z-infer`): the type and the runtime schema can never
// drift apart, because the type IS the schema.
export type CartLine = z.infer<typeof CartLineSchema>;
export type CartView = z.infer<typeof CartViewSchema>;
