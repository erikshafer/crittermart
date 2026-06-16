import type { OrderStatus, OrderStatusView } from "./orderSchema";

// The order-status presentation layer for W4 (Narrative 005 Moment 5) — the PURE derivation the tracking
// screen renders, extracted from React so the hard parts (the stop-on-terminal poll rule, the stepper
// placement) are unit-testable without React-Query timing. The contract (`orderSchema.ts`) and the query
// (`orderQueries.ts`) are reused wholesale; this module is W4's only net-new domain code, and it mints no
// new wire shape — every output is derived from the current `status` the view already carries.

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

export type StepState = "done" | "current" | "upcoming";

export interface JourneyStep {
  key: OrderStatus;
  label: string;
  state: StepState;
}

// The derived view of where the order sits on (or off) the lifecycle path. A successful-or-pending order is
// a `progress` journey — the four steps, each marked done/current/upcoming by the current status's position.
// A `cancelled` order is its own kind: the view carries no cancellation *reason* (`OrderCancelled` folds to a
// bare `Status = "cancelled"`, so `stock_unavailable`/`payment_declined`/`payment_timeout` are not on the
// wire — a logged backend gap), and we cannot know WHICH step it failed at, so the screen shows an honest
// terminal "cancelled" treatment rather than guessing a position on the path.
export type Journey = { kind: "progress"; steps: JourneyStep[] } | { kind: "cancelled" };

export function deriveJourney(status: OrderStatus): Journey {
  if (status === "cancelled") {
    return { kind: "cancelled" };
  }

  const currentIndex = LIFECYCLE.findIndex((step) => step.key === status);
  const steps = LIFECYCLE.map((step, index): JourneyStep => ({
    key: step.key,
    label: step.label,
    state: index < currentIndex ? "done" : index === currentIndex ? "current" : "upcoming",
  }));
  return { kind: "progress", steps };
}
