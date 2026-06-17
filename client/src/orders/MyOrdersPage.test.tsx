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

import { MyOrdersPage } from "@/orders/MyOrdersPage";
import type { CancelReason, OrderStatus } from "@/orders/orderSchema";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

function orderRow(
  id: string,
  status: OrderStatus,
  total: number,
  cancelReason: CancelReason | null = null,
) {
  return {
    id,
    customerId: "customer-demo",
    status,
    lines: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }],
    total,
    placedAt: "2026-06-16T14:02:00+00:00",
    cancelReason,
  };
}

// MyOrdersPage renders router <Link>s (each row → W4, the empty state → browse), so it needs router context. A
// throwaway memory-history router hosts the page at /orders and registers the link targets so they resolve and
// a click navigates — the same pattern OrderConfirmationPage's test uses. retry:false so the error path doesn't
// stall the suite.
function renderMyOrders() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const ordersRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/orders",
    component: MyOrdersPage,
  });
  const browseRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/",
    component: () => <div>Browse screen</div>,
  });
  const trackingRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/orders/$orderId",
    component: () => <div>Tracking screen</div>,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([ordersRoute, browseRoute, trackingRoute]),
    history: createMemoryHistory({ initialEntries: ["/orders"] }),
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

describe("MyOrdersPage", () => {
  it("renders the customer's orders in the server's order, each a link into the W4 track screen", async () => {
    // The server sorts newest-first; the page renders the list in received order. The array's first element is
    // the newest, so it must be the first row.
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify([
            orderRow("ord-9b22", "confirmed", 48.0),
            orderRow("ord-7f3a", "awaiting_confirmation", 103.98),
          ]),
          { status: 200 },
        ),
      ),
    );

    renderMyOrders();

    expect(await screen.findByText("ord-9b22")).toBeInTheDocument();
    expect(screen.getByText("ord-7f3a")).toBeInTheDocument();
    // Status conveyed as TEXT (a11y: never color-only), humanized from the wire token.
    expect(screen.getByText("Confirmed")).toBeInTheDocument();
    expect(screen.getByText("Awaiting confirmation")).toBeInTheDocument();
    // Server-computed totals, rendered directly (the list never re-sums lines).
    expect(screen.getByText("$48.00")).toBeInTheDocument();
    expect(screen.getByText("$103.98")).toBeInTheDocument();

    // Received order preserved: the first row is the newest (ord-9b22).
    const rows = screen.getAllByRole("listitem");
    expect(rows[0]).toHaveTextContent("ord-9b22");
    expect(rows[1]).toHaveTextContent("ord-7f3a");

    // Each row links to the order-keyed W4 route.
    expect(screen.getByRole("link", { name: /ord-9b22/ })).toHaveAttribute("href", "/orders/ord-9b22");
  });

  // A cancelled order names its specific reason on the row (the per-reason copy W4 binds), not a bare
  // "Cancelled".
  it("names the cancellation reason on a cancelled order's row", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify([orderRow("ord-c", "cancelled", 36.0, "payment_declined")]), {
          status: 200,
        }),
      ),
    );

    renderMyOrders();

    expect(await screen.findByText("Cancelled")).toBeInTheDocument();
    expect(screen.getByText(/payment was declined/i)).toBeInTheDocument();
  });

  // The no-orders domain state is a 200 [] — an empty state with a way back to the storefront, NOT an error.
  it("renders an empty state (with a browse link) when the customer has no orders", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]", { status: 200 })));

    renderMyOrders();

    expect(await screen.findByText(/haven't placed any orders yet/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /browse the storefront/i })).toHaveAttribute("href", "/");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  // A failed list read is a genuine error (Orders unreachable / 5xx) — distinct from the empty 200 [] state.
  it("renders a genuine error state when the list read fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 500 })));

    renderMyOrders();

    expect(await screen.findByRole("alert")).toHaveTextContent(/couldn.t load your orders/i);
  });

  // Clicking a row navigates to the W4 track screen — the list → detail link is live.
  it("navigates to the W4 track screen when a row is clicked", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify([orderRow("ord-9b22", "confirmed", 48.0)]), { status: 200 }),
      ),
    );

    renderMyOrders();
    const row = await screen.findByRole("link", { name: /ord-9b22/ });

    await userEvent.click(row);

    expect(await screen.findByText("Tracking screen")).toBeInTheDocument();
  });
});
