import { describe, it, expect } from "vitest";

import { ProductCatalogListSchema, ProductCatalogViewSchema } from "@/catalog/catalogSchema";

// A representative live-wire payload: camelCase keys, `price` a JSON number — the shape Wolverine.Http's
// System.Text.Json web defaults produce for Catalog's ProductCatalogView (confirmed against the running
// service in the W1 browser check). `GET /products` returns these as a JSON array.
const wireProducts = [
  { sku: "crit-001", name: "Cosmic Critter Plush", description: "a plush gremlin", price: 24.99 },
  { sku: "crit-002", name: "Nebula Newt", description: "a vinyl newt", price: 18.0 },
];

describe("ProductCatalogListSchema", () => {
  it("parses a well-formed camelCase product array", () => {
    const products = ProductCatalogListSchema.parse(wireProducts);

    expect(products).toHaveLength(2);
    expect(products[0]).toEqual({
      sku: "crit-001",
      name: "Cosmic Critter Plush",
      description: "a plush gremlin",
      price: 24.99,
    });
  });

  it("parses an empty catalog ([] is a 200, not a 404)", () => {
    expect(ProductCatalogListSchema.parse([])).toEqual([]);
  });

  // Default `.strip()` keeps the contract forward-compatible: a field Catalog adds that the SPA doesn't read is
  // dropped, not rejected. (`.strict()` would reject it — the wrong call for a wire we don't own both ends of.)
  it("drops a benign additive field rather than rejecting it", () => {
    const product = ProductCatalogViewSchema.parse({ ...wireProducts[0], imageUrl: "/crit-001.png" });

    expect(product).not.toHaveProperty("imageUrl");
    expect(product.sku).toBe("crit-001");
  });

  // The drift that MUST be caught: a field the SPA reads changing type. `decimal` regressing to a quoted string
  // would otherwise surface as `NaN` in the formatted price.
  it("rejects drift in a read field — price as a string", () => {
    const drifted = [{ ...wireProducts[0], price: "24.99" }];

    expect(() => ProductCatalogListSchema.parse(drifted)).toThrow();
  });

  // The other drift class: a required field disappearing entirely.
  it("rejects drift — a missing name", () => {
    const withoutName = [{ sku: "crit-001", description: "a plush gremlin", price: 24.99 }];

    expect(() => ProductCatalogListSchema.parse(withoutName)).toThrow();
  });
});
