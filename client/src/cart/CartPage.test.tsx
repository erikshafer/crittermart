import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from "@tanstack/react-router";

import { CartPage } from "@/cart/CartPage";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

const openCart = {
  id: "cart-7f3a",
  customerId: "customer-demo",
  isOpen: true,
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
  lastActivityAt: "2026-06-14T14:02:00+00:00",
};

// CartPage's empty state renders a router <Link> ("Browse the storefront"), so it needs router context. A
// throwaway memory-history router hosting CartPage at "/" supplies it — the pattern W1/W4 screen tests reuse.
// retry:false so the error path (not exercised here) wouldn't stall the suite.
function renderCartPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute({ component: CartPage });
  const router = createRouter({
    routeTree: rootRoute,
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

describe("CartPage", () => {
  it("renders the cart lines and a client-derived total", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(openCart), { status: 200 })),
    );

    renderCartPage();

    expect(await screen.findByText("Cosmic Critter Plush")).toBeInTheDocument();
    expect(screen.getByText("Nebula Newt")).toBeInTheDocument();
    // 2 × $24.99 + 3 × $18.00 = $103.98, summed in integer cents (no binary-float drift).
    expect(screen.getByText("$103.98")).toBeInTheDocument();
  });

  it("renders the empty state when there is no open cart (404)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    renderCartPage();

    expect(await screen.findByText(/your cart is empty/i)).toBeInTheDocument();
  });
});
