import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";

import { orderQueryOptions } from "./orderQueries";
import type { OrderStatus } from "./orderSchema";

// W3 — Order Confirmation (workshop § 5.1; Narrative 005 Moment 4). The payoff of `[ Place Order ]`: after the
// SPA POSTs `/orders` and navigates here, this screen reads `GET /orders/{orderId}` (`OrderStatusView`, the 3rd
// Zod-parsed wire surface) and shows the placed order's HONEST status — `awaiting confirmation`, never a faked
// "confirmed." This is the beat where optimism stops (Convention 3's named exception): placement kicks off a
// cross-BC reserve-stock + authorize-payment process the SPA can't guess, so it renders server truth.
//
// A PURE component taking `orderId` as a prop — the route (src/router.tsx) reads the param and passes it in,
// so this screen needs no router context and tests render it directly. Per the § 5.1 W3 wireframe it shows the
// minimal celebration (Order id · Status · Total · [ Track this order ]); the per-line receipt + the status
// path live on W4 (the deferred tracking screen reusing this same read).

// One $-formatter built once at module load (stable reference). `total` is a server-computed display value
// (the view carries it), so Intl's 2-decimal rounding is exact enough here — unlike CartPage, this screen does
// NOT recompute the total from lines in integer cents (the server already did the arithmetic).
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

// Render the wire status (e.g. `awaiting_confirmation`) as human text — underscores to spaces, first letter
// capitalized: "Awaiting confirmation". The text is the AUTHORITATIVE conveyance of status (a11y: never
// color-only — the dot beside it is decorative, aria-hidden).
function humanizeStatus(status: OrderStatus): string {
  const spaced = status.replace(/_/g, " ");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export function OrderConfirmationPage({ orderId }: { orderId: string }) {
  const ctx = useApiContext();
  const { data: order, isPending, isError, refetch } = useQuery(orderQueryOptions(orderId, ctx));

  // Loading — the order read is in flight (a fresh GET right after the placement navigate).
  if (isPending) {
    return (
      <section className="space-y-2" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">Your order</h1>
        <p role="status" className="text-muted-foreground">
          Loading your order…
        </p>
      </section>
    );
  }

  // Error — a GENUINE failure. Unlike the cart's 404 (which is the empty-cart domain state), an order the SPA
  // just placed and navigated to MUST exist; a 404/5xx here means a bad id or Orders is unreachable.
  if (isError) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Your order</h1>
        <p role="alert" className="text-muted-foreground">
          We couldn&apos;t load your order. Please try again.
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

  // Resolved — the placed order. `role="status"` so a screen reader announces the confirmation on arrival
  // without stealing focus (web-interface-guidelines: confirm an async outcome politely, no layout shift).
  return (
    <section className="mx-auto max-w-md space-y-6">
      <div role="status" className="space-y-1 text-center">
        <h1 className="text-3xl font-semibold tracking-tight">Order placed</h1>
        <p className="text-muted-foreground">Thanks — we&apos;ve received your order.</p>
      </div>

      <dl className="rounded-lg border border-border p-6">
        <div className="flex items-center justify-between gap-4 py-2">
          <dt className="text-muted-foreground">Order</dt>
          <dd className="break-all text-right font-mono text-sm">{order.id}</dd>
        </div>
        <div className="flex items-center justify-between gap-4 py-2">
          <dt className="text-muted-foreground">Status</dt>
          <dd className="flex items-center gap-2 font-medium">
            {/* Decorative pending indicator; the text is the authoritative status conveyance. */}
            <span aria-hidden="true" className="h-2 w-2 rounded-full bg-muted-foreground/60" />
            {humanizeStatus(order.status)}
          </dd>
        </div>
        <div className="flex items-center justify-between gap-4 py-2">
          <dt className="text-muted-foreground">Total</dt>
          <dd className="text-right font-semibold tabular-nums">{usd.format(order.total)}</dd>
        </div>
      </dl>

      <div className="space-y-2 text-center">
        {/* [ Track this order ] → W4 (/orders/$orderId). W4 is the next slice (locked decision 1 — W3 alone),
            so this is rendered DISABLED rather than as a link to a not-yet-registered route — a deferred,
            honest control, not a broken navigation. It becomes a live <Link> when W4 lands. */}
        <button
          type="button"
          disabled
          aria-disabled="true"
          className="w-full rounded-md border border-border px-4 py-2 text-sm font-medium opacity-50"
        >
          Track this order
        </button>
        <p className="text-xs text-muted-foreground">Order tracking arrives in the next slice (W4).</p>
      </div>
    </section>
  );
}
