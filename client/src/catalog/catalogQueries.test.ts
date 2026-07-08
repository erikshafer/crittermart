import { describe, it, expect, vi, afterEach } from "vitest";

import { fetchProducts, catalogKeys } from "@/catalog/catalogQueries";

const ctx = { token: "jwt-demo", customerId: "customer-demo" };

const wireProducts = [
  { sku: "crit-001", name: "Cosmic Critter Plush", description: "a plush gremlin", price: 24.99 },
  { sku: "crit-002", name: "Nebula Newt", description: "a vinyl newt", price: 18.0 },
];

afterEach(() => {
  vi.restoreAllMocks();
});

describe("fetchProducts", () => {
  it("returns the parsed product list on 200", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(wireProducts), { status: 200 })),
    );

    const products = await fetchProducts(ctx);

    expect(products).toHaveLength(2);
    expect(products[1].sku).toBe("crit-002");
  });

  it("returns [] for an empty catalog", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]", { status: 200 })));

    await expect(fetchProducts(ctx)).resolves.toEqual([]);
  });
});

describe("catalogKeys", () => {
  // The contrast with `cartKeys.mine(customerId)`: the catalog is PUBLIC, so its key carries NO customer id —
  // the product list doesn't vary by who's asking, so identity is not a cache dependency (qk-include-dependencies).
  it("keys the products query without a customer id (public read)", () => {
    expect(catalogKeys.products()).toEqual(["catalog", "products"]);
  });
});
