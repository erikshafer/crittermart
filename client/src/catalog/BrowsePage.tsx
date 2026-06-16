import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";
import { useAddToCart } from "@/cart/cartMutations";

import { productsQueryOptions } from "./catalogQueries";
import type { ProductCatalogView } from "./catalogSchema";

// W1 — Browse / Listing (workshop § 5.1; Narrative 005 Moments 1–2). The storefront landing: the product grid
// rendered from `GET /products` (slice 1.2), each card carrying the `[ Add to cart ]` that issues `AddToCart`
// (slice 3.1) — the project's first optimistic mutation. Product *detail* folds from the list payload; there is
// no `GET /products/{sku}` (Gap #2, deferred). The catalog is public — the X-Customer-Id header rides along but
// gates nothing.

// One $-formatter built once at module load (stable reference, cheaper than per-render). A single price is a
// display value, so Intl's 2-decimal rounding is exact enough here; the cart sums in integer cents (CartPage).
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

// A product card with its own add-to-cart mutation. Each card owns its hook so `isPending` is **per-card** —
// the tapped button shows "Adding…" and disables without freezing the whole grid (the alternative, one shared
// hook, would disable every button on any add). All cards' hooks manipulate the same shared cart cache key, so
// the header badge bumps regardless of which card fired.
function ProductCard({ product }: { product: ProductCatalogView }) {
  const addToCart = useAddToCart();

  return (
    <li className="flex flex-col rounded-lg border border-border p-5">
      <h2 className="text-lg font-semibold tracking-tight">{product.name}</h2>
      <p className="mt-1 font-mono text-sm text-muted-foreground">{product.sku}</p>
      <p className="mt-2 text-lg tabular-nums">{usd.format(product.price)}</p>
      <p className="mt-2 flex-1 text-sm text-muted-foreground">{product.description}</p>

      <button
        type="button"
        // Snapshot name + price from the loaded listing into the command — product data reaches the cart only
        // via this SPA snapshot, never a Catalog↔Orders call (Narrative 005 Moment 2). `productSnapshot` is the
        // exact field name the backend binds (retro 018).
        onClick={() =>
          addToCart.mutate({
            sku: product.sku,
            quantity: 1,
            productSnapshot: { name: product.name, price: product.price },
          })
        }
        disabled={addToCart.isPending}
        aria-label={`Add ${product.name} to cart`}
        className="mt-4 rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
      >
        {addToCart.isPending ? "Adding…" : "Add to cart"}
      </button>

      {addToCart.isError && (
        <p role="alert" className="mt-2 text-sm text-muted-foreground">
          Couldn&apos;t add that — please try again.
        </p>
      )}
    </li>
  );
}

export function BrowsePage() {
  const ctx = useApiContext();
  const { data: products, isPending, isError, refetch } = useQuery(productsQueryOptions(ctx));

  // Loading — the catalog read is in flight (no cached list yet on a cold load).
  if (isPending) {
    return (
      <section className="space-y-2" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
        <p role="status" className="text-muted-foreground">
          Loading products…
        </p>
      </section>
    );
  }

  // Error — a genuine failure (Catalog unreachable / 5xx). Distinct from the empty catalog, which is `[]` (200).
  if (isError) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
        <p role="alert" className="text-muted-foreground">
          We couldn&apos;t load the catalog. Please try again.
        </p>
        <button
          type="button"
          onClick={() => refetch()}
          className="rounded-md border border-border px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          Retry
        </button>
      </section>
    );
  }

  // Empty — the catalog holds no products (the Seller has published none yet). A legitimate 200 `[]`, not an error.
  if (products.length === 0) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
        <p className="text-muted-foreground">No products are available yet.</p>
      </section>
    );
  }

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
      <ul role="list" className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {products.map((product) => (
          <ProductCard key={product.sku} product={product} />
        ))}
      </ul>
    </section>
  );
}
