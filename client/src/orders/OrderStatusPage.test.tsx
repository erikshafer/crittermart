import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { OrderStatusPage } from "@/orders/OrderStatusPage";
import type { OrderStatus } from "@/orders/orderSchema";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

function orderAt(status: OrderStatus, total = 103.98) {
  return {
    id: "ord-7f3a",
    customerId: "customer-demo",
    status,
    lines: [
      { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
      { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
    ],
    total,
  };
}

// OrderStatusPage is a PURE component (orderId is a prop — the route reads the param) and holds no router
// <Link>, so it needs only the query + identity providers, no RouterProvider. retry:false so the error path
// doesn't stall the suite.
function renderStatusPage(orderId = "ord-7f3a") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider customerId="customer-demo">
        <OrderStatusPage orderId={orderId} />
      </CurrentCustomerProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("OrderStatusPage", () => {
  it("renders the per-line receipt and the Total READ OFF THE VIEW (not re-summed from lines)", async () => {
    // Total deliberately differs from the line arithmetic (2×24.99 + 3×18 = 103.98) to prove the screen
    // renders the server-computed view total, not a client re-sum.
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(orderAt("stock_reserved", 999.99)), { status: 200 }),
      ),
    );

    renderStatusPage();

    expect(await screen.findByText("Cosmic Critter Plush")).toBeInTheDocument();
    expect(screen.getByText("Nebula Newt")).toBeInTheDocument();
    // Per-line totals (unit × quantity, integer-cents): 2 × $24.99 = $49.98, 3 × $18.00 = $54.00.
    expect(screen.getByText("$49.98")).toBeInTheDocument();
    expect(screen.getByText("$54.00")).toBeInTheDocument();
    // The grand Total is the view's number, not 103.98.
    expect(screen.getByText("$999.99")).toBeInTheDocument();
  });

  it.each([
    ["awaiting_confirmation", "Awaiting confirmation"],
    ["stock_reserved", "Stock reserved"],
    ["payment_authorized", "Payment authorized"],
    ["confirmed", "Confirmed"],
  ] as const)(
    "at %s, the stepper marks the right current step (text, not color-only)",
    async (status, currentLabel) => {
      vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue(new Response(JSON.stringify(orderAt(status)), { status: 200 })),
      );

      renderStatusPage();
      await screen.findByText("Cosmic Critter Plush");

      // The current step carries aria-current="step"; the receipt's <li> rows do not, so this resolves to
      // exactly the stepper's active waypoint.
      const current = screen.getByRole("listitem", { current: "step" });
      expect(current).toHaveTextContent(currentLabel);
    },
  );

  it("at cancelled, shows an honest terminal treatment — no stepper position, no fabricated reason", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(orderAt("cancelled")), { status: 200 })),
    );

    renderStatusPage();

    expect(await screen.findByText(/cancelled and will not be fulfilled/i)).toBeInTheDocument();
    // No lifecycle step is marked current — we don't claim to know where it failed (the view carries no
    // cancellation reason or failure step).
    expect(screen.queryByRole("listitem", { current: "step" })).not.toBeInTheDocument();
  });

  it("the manual [ Refresh ] button re-reads the order", async () => {
    // A confirmed (terminal) order: the auto-poll is OFF (pollIntervalFor → false), so the ONLY extra fetch
    // is the manual one — making the assertion deterministic with no fake timers.
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(orderAt("confirmed")), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    renderStatusPage();
    await screen.findByText("Cosmic Critter Plush");
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await userEvent.click(screen.getByRole("button", { name: "Refresh" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });

  it("renders a genuine error state when the order read fails (a 404 is NOT an empty domain state here)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    renderStatusPage("ord-missing");

    expect(await screen.findByRole("alert")).toHaveTextContent(/couldn.t load your order/i);
  });
});
