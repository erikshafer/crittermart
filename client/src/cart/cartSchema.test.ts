import { describe, it, expect } from "vitest";

import { CartViewSchema } from "@/cart/cartSchema";

// A representative live-wire payload: camelCase keys, `price` a JSON number, `lastActivityAt` an ISO string —
// the shape Wolverine.Http's System.Text.Json web defaults produce for Orders' CartView (confirmed against the
// running service in the W2 browser check).
const wireCart = {
  id: "cart-7f3a",
  customerId: "customer-demo",
  isOpen: true,
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
  lastActivityAt: "2026-06-14T14:02:00+00:00",
};

describe("CartViewSchema", () => {
  it("parses a well-formed camelCase CartView payload", () => {
    const cart = CartViewSchema.parse(wireCart);

    expect(cart.lines).toHaveLength(2);
    expect(cart.lines[0]).toEqual({
      sku: "crit-001",
      quantity: 2,
      name: "Cosmic Critter Plush",
      price: 24.99,
    });
    expect(cart.isOpen).toBe(true);
  });

  // Default `.strip()` keeps the contract forward-compatible: a field Orders adds that the SPA doesn't read is
  // dropped, not rejected. (`.strict()` would have failed here — the wrong call for a wire we don't own both ends of.)
  it("drops a benign additive field rather than rejecting it", () => {
    const cart = CartViewSchema.parse({ ...wireCart, settledAt: "2026-06-15T00:00:00+00:00" });

    expect(cart).not.toHaveProperty("settledAt");
    expect(cart.lines).toHaveLength(2);
  });

  // The drift that MUST be caught: a field the SPA reads changing type. `decimal` regressing to a quoted string
  // would otherwise surface as `NaN` in the total three components deep.
  it("rejects drift in a read field — price as a string", () => {
    const drifted = {
      ...wireCart,
      lines: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: "24.99" }],
    };

    expect(() => CartViewSchema.parse(drifted)).toThrow();
  });

  // The other drift class: a required field disappearing entirely.
  it("rejects drift — a missing lines array", () => {
    const withoutLines = {
      id: "cart-7f3a",
      customerId: "customer-demo",
      isOpen: true,
      lastActivityAt: "2026-06-14T14:02:00+00:00",
    };

    expect(() => CartViewSchema.parse(withoutLines)).toThrow();
  });
});
