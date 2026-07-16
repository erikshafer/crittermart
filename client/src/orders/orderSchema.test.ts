import { describe, it, expect } from "vitest";

import { OrderListSchema, OrderStatusViewSchema } from "@/orders/orderSchema";

// A representative live-wire payload: camelCase keys, `total` + line `price` JSON numbers, `status` one of the
// five OrderStatus constants, `placedAt` an ISO-8601 instant, `cancelReason` null (an active order) — the
// shape Wolverine.Http's System.Text.Json web defaults produce for Orders' OrderStatusView
// (src/CritterMart.Orders/Ordering/OrderStatusView.cs, enriched in slice 025).
const wireOrder = {
  id: "ord-7f3a",
  customerId: "customer-demo",
  status: "awaiting_confirmation",
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
  total: 103.98,
  placedAt: "2026-06-16T14:02:00+00:00",
  cancelReason: null,
  // No-coupon order (slice 6.3 pricing fields, now required by the schema): subtotal == total, no discount.
  subtotal: 103.98,
  discount: 0,
  couponCode: null,
};

describe("OrderStatusViewSchema", () => {
  it("parses a well-formed camelCase OrderStatusView payload", () => {
    const order = OrderStatusViewSchema.parse(wireOrder);

    expect(order.status).toBe("awaiting_confirmation");
    expect(order.total).toBe(103.98);
    expect(order.lines).toHaveLength(2);
    expect(order.placedAt).toBe("2026-06-16T14:02:00+00:00");
    expect(order.cancelReason).toBeNull();
  });

  // placedAt + cancelReason are MODELED fields now (slice 025): a cancelled order carries its reason, parsed
  // through the closed reason enum.
  it("parses placedAt and a cancellation reason on a cancelled order", () => {
    const order = OrderStatusViewSchema.parse({
      ...wireOrder,
      status: "cancelled",
      cancelReason: "payment_declined",
    });

    expect(order.status).toBe("cancelled");
    expect(order.cancelReason).toBe("payment_declined");
    expect(order.placedAt).toBe("2026-06-16T14:02:00+00:00");
  });

  // The z.enum guard (locked decision 3): a status outside the closed OrderStatus set fails LOUD at the
  // boundary rather than rendering through to the screen — the teaching value of modeling a domain enum.
  it("rejects an unexpected status the backend never sends (the z.enum guard)", () => {
    expect(() => OrderStatusViewSchema.parse({ ...wireOrder, status: "shipped" })).toThrow();
  });

  // The same z.enum guard on cancelReason: a reason outside the closed set fails loud rather than rendering as
  // mystery copy on W4.
  it("rejects an unexpected cancellation reason (the z.enum guard on cancelReason)", () => {
    expect(() =>
      OrderStatusViewSchema.parse({ ...wireOrder, status: "cancelled", cancelReason: "lost_in_post" }),
    ).toThrow();
  });

  // A read field W4 binds (placedAt) regressing to missing — the pre-slice-025 shape — now fails loud, so a
  // service that dropped the field can't silently render a blank timestamp.
  it("rejects a payload missing placedAt (a read field W4 binds)", () => {
    const withoutPlacedAt = {
      id: wireOrder.id,
      customerId: wireOrder.customerId,
      status: wireOrder.status,
      lines: wireOrder.lines,
      total: wireOrder.total,
      cancelReason: wireOrder.cancelReason,
    };
    expect(() => OrderStatusViewSchema.parse(withoutPlacedAt)).toThrow();
  });

  // Default `.strip()` keeps the contract forward-compatible: an additive Orders field the SPA doesn't read is
  // dropped, not rejected (a wire the SPA doesn't own both ends of). `placedAt` used to be this example; it is
  // a modeled field now, so this proves `.strip()` with a genuinely-unmodeled field.
  it("drops a benign additive field rather than rejecting it", () => {
    const order = OrderStatusViewSchema.parse({ ...wireOrder, serverNote: "ignored-by-the-spa" });

    expect(order).not.toHaveProperty("serverNote");
    expect(order.lines).toHaveLength(2);
  });

  // customerName is optional (slice 5.3): present when LocalCustomerView has arrived, absent during the
  // eventual-consistency gap. Both shapes must parse — the SPA degrades gracefully, not loudly.
  it("parses customerName when present (the enriched happy path)", () => {
    const order = OrderStatusViewSchema.parse({ ...wireOrder, customerName: "Demo Customer" });
    expect(order.customerName).toBe("Demo Customer");
  });

  it("parses without customerName (the eventual-consistency gap — field absent is not an error)", () => {
    const order = OrderStatusViewSchema.parse(wireOrder);
    expect(order.customerName).toBeUndefined();
  });

  // The drift that MUST be caught: `total` (a read field) regressing from a number to a quoted string would
  // otherwise surface as a broken display or NaN downstream.
  it("rejects drift in a read field — total as a string", () => {
    expect(() => OrderStatusViewSchema.parse({ ...wireOrder, total: "103.98" })).toThrow();
  });
});

// The "My Orders" list payload (GET /orders/mine) — an array of the SAME per-order contract, reused wholesale.
describe("OrderListSchema", () => {
  it("parses an array of order views", () => {
    const orders = OrderListSchema.parse([
      wireOrder,
      { ...wireOrder, id: "ord-9b22", status: "confirmed" },
    ]);

    expect(orders).toHaveLength(2);
    expect(orders[1].status).toBe("confirmed");
  });

  // The no-orders domain state is a `200 []` — parsed, not an error (the contrast with the cart read's 404).
  it("parses an empty list (the no-orders domain state)", () => {
    expect(OrderListSchema.parse([])).toEqual([]);
  });

  // Per-element drift still fails loud at the boundary — a bad status in any row rejects the whole payload.
  it("rejects the payload when any element drifts (a bad status in one row)", () => {
    expect(() => OrderListSchema.parse([wireOrder, { ...wireOrder, status: "shipped" }])).toThrow();
  });
});
