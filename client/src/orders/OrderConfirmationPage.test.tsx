import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { OrderConfirmationPage } from "@/orders/OrderConfirmationPage";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

const placedOrder = {
  id: "ord-7f3a",
  customerId: "customer-demo",
  status: "awaiting_confirmation",
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
  total: 103.98,
};

// OrderConfirmationPage is a PURE component (orderId is a prop — the route reads the param), so it needs only
// the query + identity providers, no RouterProvider. retry:false so the error path doesn't stall the suite.
function renderConfirmation(orderId = "ord-7f3a") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider customerId="customer-demo">
        <OrderConfirmationPage orderId={orderId} />
      </CurrentCustomerProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("OrderConfirmationPage", () => {
  it("renders the placed order's honest status + total from the OrderStatusView read", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(placedOrder), { status: 200 })),
    );

    renderConfirmation();

    expect(await screen.findByText("Order placed")).toBeInTheDocument();
    // Status is conveyed as TEXT (a11y: not color-only), humanized from the wire `awaiting_confirmation`.
    expect(screen.getByText("Awaiting confirmation")).toBeInTheDocument();
    // Total is read straight off the view (server-computed — NOT recomputed from lines), formatted to USD.
    expect(screen.getByText("$103.98")).toBeInTheDocument();
    expect(screen.getByText("ord-7f3a")).toBeInTheDocument();
  });

  it("renders a genuine error state when the order read fails (a 404 is NOT an empty domain state here)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    renderConfirmation("ord-missing");

    expect(await screen.findByRole("alert")).toHaveTextContent(/couldn.t load your order/i);
  });

  it("renders [ Track this order ] disabled — W4 tracking is the next slice (a deferred control, not a broken link)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(placedOrder), { status: 200 })),
    );

    renderConfirmation();
    await screen.findByText("Order placed");

    expect(screen.getByRole("button", { name: "Track this order" })).toBeDisabled();
  });
});
