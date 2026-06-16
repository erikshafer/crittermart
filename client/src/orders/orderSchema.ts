import { z } from "zod";

// OrderStatusViewSchema — the THIRD per-read-model Zod schema (frontend SKILL Convention 2), and the third
// service read surface (Orders' `OrderStatusView`, shared by W3 confirmation and the deferred W4 tracking). It
// is the SPA's hand-written copy of the `OrderStatusView` contract that Orders' `GET /orders/{orderId}`
// returns (slice 4.1, src/CritterMart.Orders/Order/OrderStatusView.cs); NOT generated — when the service's
// response shape changes, this schema changes in the same PR that consumes the change.
//
// **Wire casing is camelCase.** Wolverine.Http serializes with System.Text.Json *web defaults* and Orders
// applies no `PropertyNamingPolicy` override (same as CartView, confirmed in #58/#61), so the C# PascalCase
// properties land camelCased on the wire: `id`, `customerId`, `status`, `lines`, `total`. `decimal Total`
// and per-line `decimal Price` serialize as JSON **numbers** (not strings).
//
// **Default `.strip()`, deliberately — not `.strict()`.** A field the SPA reads going missing or changing
// type throws here, loud and located; a benign *additive* Orders field the SPA doesn't read is dropped, not
// rejected — keeping the contract forward-compatible on a wire the SPA doesn't own both ends of (zod
// `object-strict-vs-strip`).

// The closed set of order statuses — the 5 `OrderStatus` constants the backend folds onto the view
// (src/CritterMart.Orders/Order/OrderStatusView.cs:OrderStatus). Modeled as a `z.enum` (locked decision 3,
// zod `schema-use-enums`): an unexpected/typo'd status FAILS LOUD at the boundary rather than rendering
// through to the screen — the Convention 2 spirit, with the teaching value that status is a known domain
// enum. The cost is the schema couples to today's set: a new backend status is a schema bump here, which is
// the hand-written-schema convention anyway. W3 only ever observes `awaiting_confirmation` right after a
// placement; the other four exist for the W4 tracking screen that reuses this schema.
export const OrderStatusSchema = z.enum([
  "awaiting_confirmation",
  "stock_reserved",
  "payment_authorized",
  "confirmed",
  "cancelled",
]);

// One order line — a SKU at the quantity and the name/price frozen onto the order when it was placed.
// Structurally identical to `CartLineSchema`, but deliberately a SEPARATE schema: the backend mirrors this
// (a distinct `OrderLine` record on the Order aggregate, src/CritterMart.Orders/Order/OrderPlaced.cs, not the
// Cart's `CartLine`), and the two are independent contract surfaces that can drift apart — sharing one schema
// across the cart↔orders feature seam would couple them against Convention 2's premise. Counts and money are
// inherently non-negative, so the schema says so.
export const OrderLineSchema = z.object({
  sku: z.string(),
  quantity: z.number().int().nonnegative(),
  name: z.string(),
  price: z.number().nonnegative(),
});

// The order-status read model. W3 (and the deferred W4) consume `status`, `total`, and `lines`; `id` and
// `customerId` are present-but-unused by the screen, yet still modeled so the boundary parse validates the
// whole payload and catches drift in the unread fields too. Unlike `CartView`, **`total` is on the view** (a
// server-computed JSON number), so the screen renders it directly — it does NOT recompute it from the lines.
export const OrderStatusViewSchema = z.object({
  id: z.string(),
  customerId: z.string(),
  status: OrderStatusSchema,
  lines: z.array(OrderLineSchema),
  total: z.number().nonnegative(),
});

// `z.infer` over hand-typed interfaces (zod `type-use-z-infer`): the type and the runtime schema can never
// drift apart, because the type IS the schema.
export type OrderStatus = z.infer<typeof OrderStatusSchema>;
export type OrderLine = z.infer<typeof OrderLineSchema>;
export type OrderStatusView = z.infer<typeof OrderStatusViewSchema>;
