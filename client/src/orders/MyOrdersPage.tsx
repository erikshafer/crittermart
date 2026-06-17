import { Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";

import { myOrdersQueryOptions } from "./orderQueries";
import type { OrderStatusView } from "./orderSchema";
import { formatPlacedAt, humanizeCancelReason, humanizeStatus } from "./orderStatusJourney";

// "My Orders" — the order-history list (workshop § 5.1 Gap #3, closed; Narrative 005 Moment 6). The list
// counterpart to W4's single-order tracking: it reads `GET /orders/mine` (customer-keyed, the X-Customer-Id
// header behind the useCurrentCustomer seam — the same identity transport as `GET /carts/mine`) and renders
// the customer's orders newest-first, each row a `<Link>` into the W4 `/orders/$orderId` track screen. It
// reuses the order read contract wholesale (`OrderListSchema` = `z.array(OrderStatusViewSchema)`) and the pure
// presentation helpers W4 already owns (`formatPlacedAt`, `humanizeStatus`, `humanizeCancelReason`) — its only
// net-new code is the list layout. Unlike the cart (404 = empty), an empty order history is a `200 []`, so the
// empty state is a normal data branch, not an error.

// One $-formatter built once at module load (stable reference). Each order carries its server-computed Total,
// rendered directly (the list never re-sums lines — the same stance the W3/W4 screens take).
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

// A single order row, linking into the W4 track screen. Status is conveyed as TEXT (a11y: never color-only); a
// cancelled order also shows its specific reason as a muted sub-line, so the list names the failure rather than
// a bare "Cancelled" — the same per-reason copy W4 binds.
function OrderRow({ order }: { order: OrderStatusView }) {
  return (
    <li>
      <Link
        to="/orders/$orderId"
        params={{ orderId: order.id }}
        className="flex items-center justify-between gap-4 rounded-lg border border-border p-4 hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <div className="min-w-0">
          <p className="truncate font-mono text-sm">{order.id}</p>
          <p className="text-sm text-muted-foreground">Placed {formatPlacedAt(order.placedAt)}</p>
        </div>
        <div className="shrink-0 text-right">
          <p className="font-medium">{humanizeStatus(order.status)}</p>
          {order.status === "cancelled" && order.cancelReason && (
            <p className="text-xs text-muted-foreground">{humanizeCancelReason(order.cancelReason)}</p>
          )}
          <p className="tabular-nums text-muted-foreground">{usd.format(order.total)}</p>
        </div>
      </Link>
    </li>
  );
}

export function MyOrdersPage() {
  const ctx = useApiContext();
  const { data: orders, isPending, isError, refetch } = useQuery(myOrdersQueryOptions(ctx));

  // Loading — the list read is in flight (no cached list yet on a cold load).
  if (isPending) {
    return (
      <section className="space-y-2" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">My Orders</h1>
        <p role="status" className="text-muted-foreground">
          Loading your orders…
        </p>
      </section>
    );
  }

  // Error — a genuine failure (Orders unreachable / 5xx / a malformed payload). Distinct from the empty history,
  // which is a `200 []` handled below.
  if (isError) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">My Orders</h1>
        <p role="alert" className="text-muted-foreground">
          We couldn&apos;t load your orders. Please try again.
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

  // Empty — the customer has never placed an order. A legitimate `200 []`, not an error; point them back to the
  // storefront to start one.
  if (orders.length === 0) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">My Orders</h1>
        <p className="text-muted-foreground">You haven&apos;t placed any orders yet.</p>
        <Link
          to="/"
          className="inline-block rounded-md border border-border px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          Browse the storefront
        </Link>
      </section>
    );
  }

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">My Orders</h1>
      <ul role="list" className="space-y-3">
        {orders.map((order) => (
          <OrderRow key={order.id} order={order} />
        ))}
      </ul>
    </section>
  );
}
