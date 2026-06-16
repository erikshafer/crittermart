import { z } from "zod";

// ProductCatalogViewSchema — the SECOND per-read-model Zod schema (frontend SKILL Convention 2), and the
// first one against a SECOND service surface (Catalog, not Orders), proving the multi-service SPA: three
// independently-deployed services are three contract surfaces that drift independently, and the boundary
// parse is the only place a drift is caught. It is the SPA's hand-written copy of the `ProductCatalogView`
// contract that Catalog's `GET /products` returns (slice 1.2, src/CritterMart.Catalog/Products/Product.cs:
// `ProductCatalogView(string Sku, string Name, string Description, decimal Price)`); NOT generated — when the
// service's response shape changes, this schema changes in the same PR that consumes the change.
//
// **Wire casing is camelCase.** Wolverine.Http serializes with System.Text.Json *web defaults* and Catalog
// applies no `PropertyNamingPolicy` override (same as Orders' CartView, confirmed in #58), so the C#
// PascalCase properties land camelCased on the wire: `sku`, `name`, `description`, `price`. `decimal Price`
// serializes as a JSON **number** (not a string). (Confirmed against the live response in the W1 Aspire check.)
//
// **Default `.strip()`, deliberately — not `.strict()`.** A field the SPA reads going missing or changing
// type throws here, loud and located; a benign *additive* Catalog field the SPA doesn't read is dropped, not
// rejected — keeping the contract forward-compatible on a wire the SPA doesn't own both ends of.
export const ProductCatalogViewSchema = z.object({
  sku: z.string(),
  name: z.string(),
  description: z.string(),
  // Price is money and inherently non-negative; a sign-flip is drift worth catching. Arrives in dollars as a
  // JSON number — the BrowsePage formats it for display (the cart sums in integer cents; see CartPage).
  price: z.number().nonnegative(),
});

// `GET /products` returns a JSON **array** of ProductCatalogView (an empty catalog is `[]` with 200, never a
// 404 — see BrowseProductsEndpoint), so the boundary schema is `z.array(...)`. This is the contrast with the
// single-object CartViewSchema: a list read parses the whole array at the boundary, element by element.
export const ProductCatalogListSchema = z.array(ProductCatalogViewSchema);

// `z.infer` over hand-typed interfaces (zod `type-use-z-infer`): the type and the runtime schema can never
// drift apart, because the type IS the schema.
export type ProductCatalogView = z.infer<typeof ProductCatalogViewSchema>;
