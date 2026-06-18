import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";

import { orderQueryOptions } from "./orderQueries";
import type { CancelReason, OrderLine, OrderStatus } from "./orderSchema";
import {
  deriveJourney,
  formatPlacedAt,
  humanizeCancelReason,
  humanizeStatus,
  pollIntervalFor,
} from "./orderStatusJourney";

// W4 — Order Status / Tracking (workshop § 5.1; Narrative 005 Moment 5). The payoff of W3's [ Track this
// order ]: the same `OrderStatusView` the confirmation screen read, but entered BY id and **converging by
// polling** toward the terminal state. The customer takes no further action — they watch the status settle
// `awaiting_confirmation → stock_reserved → payment_authorized → confirmed`, or land on `cancelled`.
//
// This is Convention 3's "status converges by refetch, not a socket" sentence (ADR 015 R5) made code — the
// storefront's first `refetchInterval` consumer. There is no live push round one (explicitly unlike the
// CritterBids SignalR sibling); the screen re-reads the read model until it settles, then stops.
//
// A PURE component taking `orderId` as a prop — the route (src/router.tsx) reads the param and passes it in,
// so this screen needs no router context and tests render it directly. It reuses `orderQueryOptions` and
// `OrderStatusViewSchema` wholesale (PR #62); its only net-new domain code is the pure `orderStatusJourney`
// derivation (the stop-on-terminal poll rule + the lifecycle stepper).

// One $-formatter built once at module load (stable reference). The order Total is a server-computed display
// value (the view carries it), so it is rendered directly — NOT re-summed from the lines.
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

// A line's money, in integer cents to avoid binary-float drift (mirrors CartPage's money handling). `price`
// is the unit price frozen onto the order; the line total is unit × quantity. The order's grand Total still
// comes off the view — this per-line figure is presentation only.
function lineTotalCents(line: OrderLine): number {
  return Math.round(line.price * 100) * line.quantity;
}

// A screen-reader phrasing of a step's progress state, so "done / current / upcoming" is not conveyed by the
// dot + weight alone (web-interface-guidelines: never color/shape-only). The current step also carries
// `aria-current="step"` on its <li>.
const STEP_STATE_LABEL = {
  done: "completed",
  current: "current step",
  upcoming: "not yet reached",
} as const;

// The lifecycle stepper, derived purely from the current status. A progress journey renders the four
// waypoints with the current one emphasized; a cancelled order renders an honest terminal treatment (the
// view carries no reason and no failure step, so we do not guess a position on the path).
function StatusJourney({
  status,
  cancelReason,
}: {
  status: OrderStatus;
  cancelReason: CancelReason | null;
}) {
  const journey = deriveJourney(status, cancelReason);

  if (journey.kind === "cancelled") {
    return (
      <div className="space-y-1 text-sm text-muted-foreground">
        <p>This order was cancelled and will not be fulfilled.</p>
        {/* The specific reason, now bound (slice 025) — W4 names the failure instead of a bare "cancelled".
            Guarded for the defensive null case (every real cancellation carries a reason). */}
        {journey.reason && <p>Reason: {humanizeCancelReason(journey.reason)}</p>}
      </div>
    );
  }

  return (
    <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
      {journey.steps.map((step, index) => (
        <li
          key={step.key}
          aria-current={step.state === "current" ? "step" : undefined}
          className="flex items-center gap-2"
        >
          {index > 0 && (
            <span aria-hidden="true" className="text-muted-foreground/50">
              →
            </span>
          )}
          {/* Decorative progress dot — filled once reached, hollow while upcoming. The text + sr-only label
              carry the meaning. */}
          <span
            aria-hidden="true"
            className={
              step.state === "upcoming"
                ? "h-2 w-2 rounded-full border border-muted-foreground/40"
                : "h-2 w-2 rounded-full bg-foreground"
            }
          />
          <span
            className={
              step.state === "current"
                ? "font-semibold"
                : step.state === "done"
                  ? "text-foreground"
                  : "text-muted-foreground"
            }
          >
            {step.label}
          </span>
          <span className="sr-only"> ({STEP_STATE_LABEL[step.state]})</span>
        </li>
      ))}
    </ol>
  );
}

export function OrderStatusPage({ orderId }: { orderId: string }) {
  const ctx = useApiContext();
  // Reuse the W3 query wholesale, adding the W4 poll: ~2.5s while mid-flight, STOP once terminal (the brain
  // is the pure `pollIntervalFor`). `refetchIntervalInBackground: false` — don't poll a hidden tab.
  const {
    data: order,
    isPending,
    isError,
    isFetching,
    refetch,
  } = useQuery({
    ...orderQueryOptions(orderId, ctx),
    refetchInterval: (query) => pollIntervalFor(query.state.data),
    refetchIntervalInBackground: false,
  });

  // Loading — the first read is in flight (a cold open of the tracking screen by id).
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

  // Error — a GENUINE failure. Unlike the cart's 404 (the empty-cart domain state), an order opened by id
  // MUST exist; a 404/5xx here means a bad id or Orders is unreachable. The poll keeps retrying in the
  // background, so this recovers on its own once the service answers.
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

  // Resolved — the order, polling toward its terminal state.
  return (
    <section className="mx-auto max-w-xl space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Your order</h1>
        <p className="break-all font-mono text-sm text-muted-foreground">{order.id}</p>
        {order.customerName && (
          <p className="text-sm text-muted-foreground">{order.customerName}</p>
        )}
      </div>

      <div className="space-y-3 rounded-lg border border-border p-6">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-sm font-medium text-muted-foreground">Status</h2>
          {/* Manual convergence (locked decision 2) — alongside the auto-poll, a "show me now". Disabled
              while a fetch is already in flight so the label stays honest. */}
          <button
            type="button"
            onClick={() => refetch()}
            disabled={isFetching}
            className="rounded-md border border-border px-3 py-1 text-xs font-medium hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
          >
            {isFetching ? "Refreshing…" : "Refresh"}
          </button>
        </div>
        {/* aria-live="polite" — as the status converges across polls, a screen reader announces the new
            state without stealing focus. The text is the authoritative conveyance (not color). */}
        <p aria-live="polite" className="text-lg font-semibold">
          {humanizeStatus(order.status)}
        </p>
        <StatusJourney status={order.status} cancelReason={order.cancelReason} />
        {/* The § 5.1 W4 "Placed … UTC" line — the order's placement time, now bound (slice 025). */}
        <p className="text-sm text-muted-foreground">Placed {formatPlacedAt(order.placedAt)}</p>
      </div>

      {/* The receipt the W3 celebration omitted: per-line rows + the server-computed Total. */}
      <div className="rounded-lg border border-border">
        <ul className="divide-y divide-border">
          {order.lines.map((line) => (
            <li key={line.sku} className="flex items-center justify-between gap-4 p-4">
              <div className="min-w-0">
                <p className="truncate font-medium">{line.name}</p>
                <p className="font-mono text-xs text-muted-foreground">
                  {line.sku} · ×{line.quantity}
                </p>
              </div>
              <span className="tabular-nums">{usd.format(lineTotalCents(line) / 100)}</span>
            </li>
          ))}
        </ul>
        <div className="flex items-center justify-between gap-4 border-t border-border p-4">
          <span className="font-medium">Total</span>
          <span className="text-lg font-semibold tabular-nums">{usd.format(order.total)}</span>
        </div>
      </div>
    </section>
  );
}
