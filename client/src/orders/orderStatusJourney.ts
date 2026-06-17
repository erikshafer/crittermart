import type { CancelReason, OrderStatus, OrderStatusView } from "./orderSchema";

// The order-status presentation layer for W4 (Narrative 005 Moment 5) — the PURE derivation the tracking
// screen renders, extracted from React so the hard parts (the stop-on-terminal poll rule, the stepper
// placement, the per-reason cancel copy, the placed-at formatting) are unit-testable without React-Query
// timing. The contract (`orderSchema.ts`) and the query (`orderQueries.ts`) are reused wholesale; this module
// is W4's net-new domain code, and it mints no new wire shape — every output is derived from fields the view
// already carries (`status`, `cancelReason`, `placedAt`).

// Poll cadence (locked decision 2). The W4 screen converges by `refetchInterval`, not a socket (ADR 015 R5,
// explicitly unlike the CritterBids SignalR sibling) — so it re-reads the same `OrderStatusView` until it
// settles. ~2.5s reads as "live" without hammering the three-service span chain the talk watches.
export const POLL_INTERVAL_MS = 2500;

// The happy-path lifecycle, in order — the four states an order walks on its way to success. This is the
// REAL enum sequence (not the wireframe's collapsed three-step `awaiting → stock reserved → confirmed`):
// `payment_authorized` is the transient intermediate the backend appends together with `OrderConfirmed`
// (src/CritterMart.Orders/Ordering/OrderStatusView.cs), so the stepper honors all four waypoints. `cancelled`
// is deliberately NOT in this list — it is a terminal branch off the path, not a step along it.
export const LIFECYCLE = [
  { key: "awaiting_confirmation", label: "Awaiting confirmation" },
  { key: "stock_reserved", label: "Stock reserved" },
  { key: "payment_authorized", label: "Payment authorized" },
  { key: "confirmed", label: "Confirmed" },
] as const satisfies ReadonlyArray<{ key: OrderStatus; label: string }>;

// A settled order — it will not change further, so the poll stops (locked decision 2). `confirmed` is the
// terminal of a successful purchase (CritterMart models no picking/packing/shipping — Narrative 004);
// `cancelled` is the terminal failure. Everything else is mid-flight and keeps polling.
export function isTerminalStatus(status: OrderStatus | undefined): boolean {
  return status === "confirmed" || status === "cancelled";
}

// The `refetchInterval` brain: keep polling (~2.5s) while the order is mid-flight, stop (`false`) once it
// settles. `undefined` — the first load, before any data — polls too, so the screen converges from a cold
// open. Unit-tested directly so "stops on terminal" is a deterministic assertion, never a flaky timer test.
export function pollIntervalFor(data: OrderStatusView | undefined): number | false {
  return isTerminalStatus(data?.status) ? false : POLL_INTERVAL_MS;
}

// Render the wire status (e.g. `awaiting_confirmation`) as human text — underscores to spaces, first letter
// capitalized: "Awaiting confirmation". The text is the AUTHORITATIVE conveyance of status (a11y: never
// color-only). Shared by the W3 confirmation card and the W4 tracking headline.
export function humanizeStatus(status: OrderStatus): string {
  const spaced = status.replace(/_/g, " ");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

// The per-reason cancel copy (slice 025). The view now carries `cancelReason` (the `OrderCancelled` reason the
// write aggregate ignores), so W4 names WHICH failure befell the order rather than a bare "cancelled". A
// `Record<CancelReason, string>` makes the map EXHAUSTIVE — a new backend reason is a compile error here until
// its copy is written, the same loud-at-the-seam discipline the `z.enum` gives the boundary.
const CANCEL_REASON_COPY: Record<CancelReason, string> = {
  stock_unavailable: "Some items were no longer in stock.",
  payment_declined: "The payment was declined.",
  payment_timeout: "The payment wasn't completed in time.",
};

export function humanizeCancelReason(reason: CancelReason): string {
  return CANCEL_REASON_COPY[reason];
}

// Format the placement instant (the view's `placedAt`, an ISO-8601 string) as a readable UTC timestamp — the
// wireframe's `Placed … UTC` line (§ 5.1 W4). Built once at module load (stable reference). UTC + 24-hour so
// the displayed time matches the event's append metadata regardless of the viewer's locale, and so the demo
// reads the same on any machine. A defensive guard returns the raw string if it ever fails to parse, rather
// than rendering "Invalid Date".
const placedAtFormatter = new Intl.DateTimeFormat("en-US", {
  year: "numeric",
  month: "short",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
  timeZone: "UTC",
  timeZoneName: "short",
});

export function formatPlacedAt(iso: string): string {
  const parsed = new Date(iso);
  return Number.isNaN(parsed.getTime()) ? iso : placedAtFormatter.format(parsed);
}

export type StepState = "done" | "current" | "upcoming";

export interface JourneyStep {
  key: OrderStatus;
  label: string;
  state: StepState;
}

// The derived view of where the order sits on (or off) the lifecycle path. A successful-or-pending order is
// a `progress` journey — the four steps, each marked done/current/upcoming by the current status's position.
// A `cancelled` order is its own kind, now carrying the `reason` the view surfaces (slice 025) so W4 can name
// the failure; we still cannot know WHICH step it failed at, so the screen shows an honest terminal treatment
// (the reason, not a guessed position on the path). `reason` is `null` only for the defensive case of a
// cancelled order whose reason somehow did not reach the wire — every real cancellation carries one.
export type Journey =
  | { kind: "progress"; steps: JourneyStep[] }
  | { kind: "cancelled"; reason: CancelReason | null };

export function deriveJourney(status: OrderStatus, cancelReason: CancelReason | null = null): Journey {
  if (status === "cancelled") {
    return { kind: "cancelled", reason: cancelReason };
  }

  const currentIndex = LIFECYCLE.findIndex((step) => step.key === status);
  const steps = LIFECYCLE.map((step, index): JourneyStep => ({
    key: step.key,
    label: step.label,
    state: index < currentIndex ? "done" : index === currentIndex ? "current" : "upcoming",
  }));
  return { kind: "progress", steps };
}
