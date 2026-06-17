import { describe, it, expect, vi, afterEach } from "vitest";

import { CUSTOMER_ID_HEADER, NotFoundError } from "@/api/client";
import { fetchMyOrders, fetchOrder, orderKeys } from "@/orders/orderQueries";

const ctx = { customerId: "customer-demo" };

const wireOrder = {
  id: "ord-7f3a",
  customerId: "customer-demo",
  status: "awaiting_confirmation",
  lines: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }],
  total: 49.98,
  placedAt: "2026-06-16T14:02:00+00:00",
  cancelReason: null,
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("fetchOrder", () => {
  it("returns the parsed order and sets the identity header on 200", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(wireOrder), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const order = await fetchOrder("ord-7f3a", ctx);

    expect(order.status).toBe("awaiting_confirmation");
    expect(order.total).toBe(49.98);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/orders/ord-7f3a");
    expect((init.headers as Record<string, string>)[CUSTOMER_ID_HEADER]).toBe("customer-demo");
  });

  // The contrast with the cart read: a 404 here is a GENUINE error (an order the SPA just placed must exist),
  // NOT a domain-empty state mapped to null. fetchOrder lets NotFoundError propagate to the query's isError.
  it("propagates a 404 as a NotFoundError (an order should exist — not empty data)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    await expect(fetchOrder("ord-missing", ctx)).rejects.toBeInstanceOf(NotFoundError);
  });
});

describe("fetchMyOrders", () => {
  it("returns the parsed order list and sets the identity header on 200", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify([wireOrder, { ...wireOrder, id: "ord-9b22", status: "confirmed" }]), {
          status: 200,
        }),
      );
    vi.stubGlobal("fetch", fetchMock);

    const orders = await fetchMyOrders(ctx);

    expect(orders).toHaveLength(2);
    expect(orders[1].status).toBe("confirmed");
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/orders/mine");
    expect((init.headers as Record<string, string>)[CUSTOMER_ID_HEADER]).toBe("customer-demo");
  });

  // The no-orders domain state is a 200 [] — fetchMyOrders returns the parsed empty array (NOT a NotFoundError
  // mapping like the cart read), so the page renders an empty state rather than an error.
  it("returns an empty list for a customer with no orders (200 [])", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]", { status: 200 })));

    await expect(fetchMyOrders(ctx)).resolves.toEqual([]);
  });
});

describe("orderKeys", () => {
  // The order is resolved BY its id (not the customer), so the id is the key's one true dependency — W4 polling
  // the same order shares this entry.
  it("keys an order hierarchically by orderId", () => {
    expect(orderKeys.detail("ord-7f3a")).toEqual(["order", "detail", "ord-7f3a"]);
  });

  // The list is resolved BY the customer (not an order id), so the customer id is the key's dependency —
  // mirroring cartKeys.mine.
  it("keys the My Orders list hierarchically by customerId", () => {
    expect(orderKeys.mine("customer-demo")).toEqual(["order", "mine", "customer-demo"]);
  });
});
