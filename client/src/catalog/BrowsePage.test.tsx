import { describe, it, expect, vi, afterEach } from "vitest";
import type { ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { BrowsePage } from "@/catalog/BrowsePage";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

const wireProducts = [
  { sku: "crit-001", name: "Cosmic Critter Plush", description: "a plush gremlin", price: 24.99 },
  { sku: "crit-002", name: "Nebula Newt", description: "a vinyl newt", price: 18.0 },
];

// BrowsePage renders no router <Link> (unlike CartPage), so it needs only query + identity context — no router.
function renderBrowsePage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider customerId="customer-demo">{children}</CurrentCustomerProvider>
    </QueryClientProvider>
  );
  return render(<BrowsePage />, { wrapper });
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("BrowsePage", () => {
  it("renders a card per product with name, sku, price, and an Add to cart button", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(wireProducts), { status: 200 })),
    );

    renderBrowsePage();

    expect(await screen.findByText("Cosmic Critter Plush")).toBeInTheDocument();
    expect(screen.getByText("Nebula Newt")).toBeInTheDocument();
    expect(screen.getByText("$24.99")).toBeInTheDocument();
    expect(screen.getByText("crit-002")).toBeInTheDocument();
    // One per-card Add button, each given an accessible name distinguishing the product.
    expect(screen.getByRole("button", { name: /add cosmic critter plush to cart/i })).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /add .* to cart/i })).toHaveLength(2);
  });

  it("renders the empty state when the catalog is empty ([])", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]", { status: 200 })));

    renderBrowsePage();

    expect(await screen.findByText(/no products are available yet/i)).toBeInTheDocument();
  });

  it("issues an AddToCart command with the product's snapshot when its button is clicked", async () => {
    const user = userEvent.setup();
    // The list GET answers products; the command POST answers 201. Branch the one mock on method.
    const fetchMock = vi.fn((_url: string, init?: RequestInit) =>
      init?.method === "POST"
        ? Promise.resolve(new Response(JSON.stringify({ cartId: "cart-1" }), { status: 201 }))
        : Promise.resolve(new Response(JSON.stringify(wireProducts), { status: 200 })),
    );
    vi.stubGlobal("fetch", fetchMock);

    renderBrowsePage();

    await user.click(await screen.findByRole("button", { name: /add cosmic critter plush to cart/i }));

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(([, init]) => (init as RequestInit)?.method === "POST");
      expect(post).toBeDefined();
      const body = JSON.parse((post![1] as RequestInit).body as string);
      expect(body).toMatchObject({
        sku: "crit-001",
        quantity: 1,
        productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 },
      });
    });
  });
});
