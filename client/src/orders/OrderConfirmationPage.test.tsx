import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  RouterProvider,
} from "@tanstack/react-router";

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

// OrderConfirmationPage now renders a router <Link> ("Track this order" → W4), so it needs router context. A
// throwaway memory-history router hosts the page at "/" and registers the W4 target (/orders/$orderId) so the
// link resolves and a click navigates — the same pattern CartPage's empty-state test uses. retry:false so the
// error path doesn't stall the suite.
function renderConfirmation(orderId = "ord-7f3a") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const confirmationRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/",
    component: () => <OrderConfirmationPage orderId={orderId} />,
  });
  const trackingRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/orders/$orderId",
    component: () => <div>Tracking screen</div>,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([confirmationRoute, trackingRoute]),
    history: createMemoryHistory({ initialEntries: ["/"] }),
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider customerId="customer-demo">
        <RouterProvider router={router} />
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

  it("[ Track this order ] is now a live link to the W4 tracking route and navigates there (W4 landed)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(placedOrder), { status: 200 })),
    );

    renderConfirmation();
    await screen.findByText("Order placed");

    // The deferred control of #62 is now a real <Link> resolving to the order-keyed W4 route.
    const trackLink = screen.getByRole("link", { name: "Track this order" });
    expect(trackLink).toHaveAttribute("href", "/orders/ord-7f3a");

    await userEvent.click(trackLink);
    expect(await screen.findByText("Tracking screen")).toBeInTheDocument();
  });
});
