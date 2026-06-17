import { z } from "zod";

// OrderStatusViewSchema — the THIRD per-read-model Zod schema (frontend SKILL Convention 2), and the third
// service read surface (Orders' `OrderStatusView`, shared by W3 confirmation and the W4 tracking screen). It
// is the SPA's hand-written copy of the `OrderStatusView` contract that Orders' `GET /orders/{orderId}`
// returns (slice 4.1, src/CritterMart.Orders/Ordering/OrderStatusView.cs); NOT generated — when the service's
// response shape changes, this schema changes in the same PR that consumes the change.
//
// **Wire casing is camelCase.** Wolverine.Http serializes with System.Text.Json *web defaults* and Orders
// applies no `PropertyNamingPolicy` override (same as CartView, confirmed in #58/#61), so the C# PascalCase
// properties land camelCased on the wire: `id`, `customerId`, `status`, `lines`, `total`, and (slice 025)
// `placedAt`, `cancelReason`. `decimal Total` and per-line `decimal Price` serialize as JSON **numbers** (not
// strings); `DateTimeOffset PlacedAt` as an ISO-8601 string; `string? CancelReason` as a string-or-null.
//
// **Default `.strip()`, deliberately — not `.strict()`.** A field the SPA reads going missing or changing
// type throws here, loud and located; a benign *additive* Orders field the SPA doesn't read is dropped, not
// rejected — keeping the contract forward-compatible on a wire the SPA doesn't own both ends of (zod
// `object-strict-vs-strip`).

// The closed set of order statuses — the 5 `OrderStatus` constants the backend folds onto the view
// (src/CritterMart.Orders/Ordering/OrderStatusView.cs:OrderStatus). Modeled as a `z.enum` (locked decision 3,
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

// The closed set of cancellation reasons `OrderCancelled` carries (src/CritterMart.Orders/Ordering/
// OrderCancelled.cs:CancelReason). Like `OrderStatusSchema`, a `z.enum` so a reason the backend never sends
// FAILS LOUD at the boundary rather than rendering through as mystery copy. On the wire it is null until the
// order is cancelled, then one of these three — folded onto the view from `OrderCancelled.Reason` (slice 025),
// the reason the write aggregate ignores but the read view surfaces so W4 can name the failure.
export const CancelReasonSchema = z.enum([
  "stock_unavailable",
  "payment_declined",
  "payment_timeout",
]);

// One order line — a SKU at the quantity and the name/price frozen onto the order when it was placed.
// Structurally identical to `CartLineSchema`, but deliberately a SEPARATE schema: the backend mirrors this
// (a distinct `OrderLine` record on the Order aggregate, src/CritterMart.Orders/Ordering/OrderPlaced.cs, not the
// Cart's `CartLine`), and the two are independent contract surfaces that can drift apart — sharing one schema
// across the cart↔orders feature seam would couple them against Convention 2's premise. Counts and money are
// inherently non-negative, so the schema says so.
export const OrderLineSchema = z.object({
  sku: z.string(),
  quantity: z.number().int().nonnegative(),
  name: z.string(),
  price: z.number().nonnegative(),
});

// The order-status read model. W3 consumes `status`, `total`, and `lines`; W4 also binds `placedAt` (the
// order's placement time) and `cancelReason` (the per-reason cancel copy). `id` and `customerId` are
// present-but-unused by the screens, yet still modeled so the boundary parse validates the whole payload and
// catches drift in the unread fields too. Unlike `CartView`, **`total` is on the view** (a server-computed
// JSON number), so the screen renders it directly — it does NOT recompute it from the lines.
export const OrderStatusViewSchema = z.object({
  id: z.string(),
  customerId: z.string(),
  status: OrderStatusSchema,
  lines: z.array(OrderLineSchema),
  total: z.number().nonnegative(),
  // The order's placement time — the genesis `OrderPlaced` event's append timestamp, surfaced from Marten
  // event metadata (slice 025). An ISO-8601 instant from System.Text.Json's `DateTimeOffset`, rendered
  // client-side by `formatPlacedAt`. `z.string()` over a strict `.datetime()` so a valid-but-unusual offset
  // format is never falsely rejected at the boundary; type drift (a null or a number) still fails loud.
  placedAt: z.string(),
  // The cancellation reason — null until the order is cancelled, then one of the three `OrderCancelled`
  // reasons (slice 025). A closed nullable enum mirroring `status`: a reason the backend never sends fails
  // loud here rather than rendering through as mystery copy.
  cancelReason: CancelReasonSchema.nullable(),
});

// The "My Orders" list payload (`GET /orders/mine`) — a customer's orders, each a full `OrderStatusView`,
// ordered newest-first by the server. The schema is the array of the SAME per-order contract W3/W4 bind
// (Convention 2, reused wholesale): the list row and the detail screen share one shape, so a row links
// straight into the W4 track screen with no second contract. An empty array is the no-orders domain state
// (`200 []`), parsed like any other payload — not an error (the contrast with `GET /carts/mine`'s `404`).
export const OrderListSchema = z.array(OrderStatusViewSchema);

// `z.infer` over hand-typed interfaces (zod `type-use-z-infer`): the type and the runtime schema can never
// drift apart, because the type IS the schema.
export type OrderStatus = z.infer<typeof OrderStatusSchema>;
export type CancelReason = z.infer<typeof CancelReasonSchema>;
export type OrderLine = z.infer<typeof OrderLineSchema>;
export type OrderStatusView = z.infer<typeof OrderStatusViewSchema>;
export type OrderList = z.infer<typeof OrderListSchema>;
