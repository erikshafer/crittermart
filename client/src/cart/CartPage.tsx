import { Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";

import { cartQueryOptions } from "./cartQueries";
import type { CartLine } from "./cartSchema";

// W2 — Cart Review (workshop § 5.1). The storefront's first real screen: it renders the customer's open cart
// on a cold load by binding the slice-3.5 read `GET /carts/mine` (identity carried ambiently in the
// X-Customer-Id header via the useCurrentCustomer seam — frontend SKILL Convention 4). This slice is
// READ-ONLY: the wireframe's [-]/[+]/[x] edits (slices 3.2/3.3) and [ Place Order ] (slice 4.1) are their own
// modeled command slices and land in the follow-on W2 PRs; quantity here is read-only text, the edit/checkout
// controls deliberately absent rather than stubbed.

// One $-formatter, built once at module load (cheaper than per-render, stable reference).
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

// Money is summed in INTEGER CENTS, never floating-point dollars: `0.1 + 0.2 !== 0.3` in binary float, so a
// naive `Σ price × qty` can drift a penny on a long cart. Rounding each price to cents up front keeps the
// displayed Total exact (2 plush @ $24.99 + 3 newt @ $18.00 = $103.98, to the cent). Prices arrive as JSON
// numbers in dollars, so `toCents` is the one conversion.
function toCents(dollars: number): number {
  return Math.round(dollars * 100);
}

function formatCents(cents: number): string {
  return usd.format(cents / 100);
}

function lineSubtotalCents(line: CartLine): number {
  return toCents(line.price) * line.quantity;
}

export function CartPage() {
  const ctx = useApiContext();
  const { data: cart, isPending, isError, refetch } = useQuery(cartQueryOptions(ctx));

  // Loading — the read is in flight. (No cached cart yet on a cold load.)
  if (isPending) {
    return (
      <section className="space-y-2" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
        <p role="status" className="text-muted-foreground">
          Loading your cart…
        </p>
      </section>
    );
  }

  // Error — a genuine failure, NOT the empty-cart case (that resolves to `null` data, below). A 400 means the
  // identity header was missing (the seam misfired — a bug, Convention 4); a 5xx means Orders is unreachable.
  if (isError) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
        <p role="alert" className="text-muted-foreground">
          We couldn&apos;t load your cart. Please try again.
        </p>
        <button
          type="button"
          onClick={() => refetch()}
          className="rounded-md border border-border px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground"
        >
          Retry
        </button>
      </section>
    );
  }

  // Resolved. `cart` is `CartView | null` — `null` (no open cart) renders identically to a cart with no lines.
  const lines = cart?.lines ?? [];

  // Empty — a legitimate domain state: the customer has no open cart, or emptied every line. The cart stays
  // ready for the next add (Narrative 005 Moment 3); the one thing it can't do is check out.
  if (lines.length === 0) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
        <p className="text-muted-foreground">Your cart is empty.</p>
        <Link to="/" className="text-sm underline underline-offset-4">
          Browse the storefront
        </Link>
      </section>
    );
  }

  const totalCents = lines.reduce((sum, line) => sum + lineSubtotalCents(line), 0);

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>

      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">Items in your cart, with unit price, quantity, and subtotal.</caption>
        <thead>
          <tr className="border-b border-border text-left text-muted-foreground">
            <th scope="col" className="py-2 pr-4 font-medium">
              Item
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              SKU
            </th>
            <th scope="col" className="py-2 pr-4 text-right font-medium">
              Price
            </th>
            <th scope="col" className="py-2 pr-4 text-right font-medium">
              Qty
            </th>
            <th scope="col" className="py-2 text-right font-medium">
              Subtotal
            </th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => (
            <tr key={line.sku} className="border-b border-border">
              <td className="py-3 pr-4 font-medium">{line.name}</td>
              <td className="py-3 pr-4 font-mono text-muted-foreground">{line.sku}</td>
              <td className="py-3 pr-4 text-right tabular-nums">{formatCents(toCents(line.price))}</td>
              <td className="py-3 pr-4 text-right tabular-nums">{line.quantity}</td>
              <td className="py-3 text-right tabular-nums">{formatCents(lineSubtotalCents(line))}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row" colSpan={4} className="py-3 pr-4 text-right font-semibold">
              Total
            </th>
            <td className="py-3 text-right font-semibold tabular-nums">{formatCents(totalCents)}</td>
          </tr>
        </tfoot>
      </table>
    </section>
  );
}
