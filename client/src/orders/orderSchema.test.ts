import { describe, it, expect } from "vitest";

import { OrderStatusViewSchema } from "@/orders/orderSchema";

// A representative live-wire payload: camelCase keys, `total` + line `price` JSON numbers, `status` one of the
// five OrderStatus constants — the shape Wolverine.Http's System.Text.Json web defaults produce for Orders'
// OrderStatusView (src/CritterMart.Orders/Order/OrderStatusView.cs).
const wireOrder = {
  id: "ord-7f3a",
  customerId: "customer-demo",
  status: "awaiting_confirmation",
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
  total: 103.98,
};

describe("OrderStatusViewSchema", () => {
  it("parses a well-formed camelCase OrderStatusView payload", () => {
    const order = OrderStatusViewSchema.parse(wireOrder);

    expect(order.status).toBe("awaiting_confirmation");
    expect(order.total).toBe(103.98);
    expect(order.lines).toHaveLength(2);
  });

  // The z.enum guard (locked decision 3): a status outside the closed OrderStatus set fails LOUD at the
  // boundary rather than rendering through to the screen — the teaching value of modeling a domain enum.
  it("rejects an unexpected status the backend never sends (the z.enum guard)", () => {
    expect(() => OrderStatusViewSchema.parse({ ...wireOrder, status: "shipped" })).toThrow();
  });

  // Default `.strip()` keeps the contract forward-compatible: an additive Orders field the SPA doesn't read is
  // dropped, not rejected (a wire the SPA doesn't own both ends of).
  it("drops a benign additive field rather than rejecting it", () => {
    const order = OrderStatusViewSchema.parse({ ...wireOrder, placedAt: "2026-06-16T00:00:00+00:00" });

    expect(order).not.toHaveProperty("placedAt");
    expect(order.lines).toHaveLength(2);
  });

  // The drift that MUST be caught: `total` (a read field) regressing from a number to a quoted string would
  // otherwise surface as a broken display or NaN downstream.
  it("rejects drift in a read field — total as a string", () => {
    expect(() => OrderStatusViewSchema.parse({ ...wireOrder, total: "103.98" })).toThrow();
  });
});
