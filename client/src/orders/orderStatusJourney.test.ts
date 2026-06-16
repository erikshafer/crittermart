import { describe, it, expect } from "vitest";

import type { OrderStatus, OrderStatusView } from "@/orders/orderSchema";
import {
  POLL_INTERVAL_MS,
  isTerminalStatus,
  pollIntervalFor,
  deriveJourney,
  humanizeStatus,
} from "@/orders/orderStatusJourney";

// A minimal view at a given status — the only field the derivation reads.
function viewAt(status: OrderStatus): OrderStatusView {
  return { id: "ord-7f3a", customerId: "customer-demo", status, lines: [], total: 0 };
}

describe("isTerminalStatus", () => {
  it("is true only for the two settled states", () => {
    expect(isTerminalStatus("confirmed")).toBe(true);
    expect(isTerminalStatus("cancelled")).toBe(true);
  });

  it("is false for every mid-flight state and for undefined (the cold first load)", () => {
    expect(isTerminalStatus("awaiting_confirmation")).toBe(false);
    expect(isTerminalStatus("stock_reserved")).toBe(false);
    expect(isTerminalStatus("payment_authorized")).toBe(false);
    expect(isTerminalStatus(undefined)).toBe(false);
  });
});

describe("pollIntervalFor — the refetchInterval brain (locked decision 2)", () => {
  it("polls (~2.5s) on the cold first load, before any data", () => {
    expect(pollIntervalFor(undefined)).toBe(POLL_INTERVAL_MS);
  });

  it("keeps polling while the order is mid-flight", () => {
    expect(pollIntervalFor(viewAt("awaiting_confirmation"))).toBe(POLL_INTERVAL_MS);
    expect(pollIntervalFor(viewAt("stock_reserved"))).toBe(POLL_INTERVAL_MS);
    expect(pollIntervalFor(viewAt("payment_authorized"))).toBe(POLL_INTERVAL_MS);
  });

  it("STOPS (false) once the order settles — confirmed or cancelled won't change further", () => {
    expect(pollIntervalFor(viewAt("confirmed"))).toBe(false);
    expect(pollIntervalFor(viewAt("cancelled"))).toBe(false);
  });
});

describe("deriveJourney — the lifecycle stepper (locked decision 4)", () => {
  it("at awaiting_confirmation, step 1 is current and the rest are upcoming", () => {
    const journey = deriveJourney("awaiting_confirmation");
    expect(journey).toEqual({
      kind: "progress",
      steps: [
        { key: "awaiting_confirmation", label: "Awaiting confirmation", state: "current" },
        { key: "stock_reserved", label: "Stock reserved", state: "upcoming" },
        { key: "payment_authorized", label: "Payment authorized", state: "upcoming" },
        { key: "confirmed", label: "Confirmed", state: "upcoming" },
      ],
    });
  });

  it("at stock_reserved, the first step is done and the second is current", () => {
    const journey = deriveJourney("stock_reserved");
    expect(journey.kind).toBe("progress");
    if (journey.kind !== "progress") return;
    expect(journey.steps.map((s) => s.state)).toEqual(["done", "current", "upcoming", "upcoming"]);
  });

  it("at payment_authorized, the first two are done and the third is current", () => {
    const journey = deriveJourney("payment_authorized");
    if (journey.kind !== "progress") throw new Error("expected progress");
    expect(journey.steps.map((s) => s.state)).toEqual(["done", "done", "current", "upcoming"]);
  });

  it("at confirmed (terminal success), every step is done", () => {
    const journey = deriveJourney("confirmed");
    if (journey.kind !== "progress") throw new Error("expected progress");
    expect(journey.steps.map((s) => s.state)).toEqual(["done", "done", "done", "current"]);
  });

  it("at cancelled, the journey is its own terminal branch — not a position on the path", () => {
    // The view carries no cancellation reason and no failure step, so we render a terminal treatment
    // rather than guessing where on the lifecycle it stopped.
    expect(deriveJourney("cancelled")).toEqual({ kind: "cancelled" });
  });
});

describe("humanizeStatus", () => {
  it("turns the wire enum into sentence-case display text", () => {
    expect(humanizeStatus("awaiting_confirmation")).toBe("Awaiting confirmation");
    expect(humanizeStatus("stock_reserved")).toBe("Stock reserved");
    expect(humanizeStatus("payment_authorized")).toBe("Payment authorized");
    expect(humanizeStatus("confirmed")).toBe("Confirmed");
    expect(humanizeStatus("cancelled")).toBe("Cancelled");
  });
});
