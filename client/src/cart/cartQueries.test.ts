import { describe, it, expect, vi, afterEach } from "vitest";

import { fetchMyCart, selectCartLineCount, cartKeys } from "@/cart/cartQueries";
import type { CartView } from "@/cart/cartSchema";

const ctx = { customerId: "customer-demo" };

const openCart = {
  id: "cart-7f3a",
  customerId: "customer-demo",
  isOpen: true,
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
  lastActivityAt: "2026-06-14T14:02:00+00:00",
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("fetchMyCart", () => {
  it("returns the parsed cart on 200", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(openCart), { status: 200 })),
    );

    const cart = await fetchMyCart(ctx);

    expect(cart?.lines).toHaveLength(2);
  });

  // The Convention 4 mapping: 404 ("no open cart") is a DOMAIN-empty state, resolved to `null` data so the
  // query never trips its error path for it.
  it("maps a 404 to null — no open cart is empty data, not an error", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    await expect(fetchMyCart(ctx)).resolves.toBeNull();
  });

  // A 400 means the identity header was missing (the seam misfired) — a real bug, not an empty cart, so it
  // rethrows into the query's error state rather than masquerading as `null`.
  it("rethrows a non-404 failure (e.g. a 400 seam misfire)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 400 })));

    await expect(fetchMyCart(ctx)).rejects.toThrow();
  });
});

describe("selectCartLineCount", () => {
  it("counts distinct cart lines (the badge's Cart (N))", () => {
    expect(selectCartLineCount(openCart as CartView)).toBe(2);
  });

  it("is 0 when there is no open cart", () => {
    expect(selectCartLineCount(null)).toBe(0);
  });
});

describe("cartKeys", () => {
  // The key carries the customer id — the dependency the cart is resolved by — so a dev identity switch keys a
  // different cart instead of serving the wrong one.
  it("keys the cart hierarchically by customer id", () => {
    expect(cartKeys.mine("customer-demo")).toEqual(["cart", "mine", "customer-demo"]);
  });
});
