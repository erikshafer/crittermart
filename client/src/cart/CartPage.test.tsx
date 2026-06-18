import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

// A cart whose one line sits at quantity 1 — the boundary the [-] stepper guards (locked decision 2).
const singleQtyCart = {
  ...openCart,
  lines: [{ sku: "crit-001", quantity: 1, name: "Cosmic Critter Plush", price: 24.99 }],
};

// A fetch that serves the cart on the GET and a 204 on any command (POST/DELETE). The interaction tests
// assert which command fired by inspecting `fetchMock.mock.calls`, not the post-reconcile DOM (onSettled
// refetches the same fixture, so the optimistic change reverts — the command call is the durable evidence).
function stubCartFetch(cart: unknown) {
  const fetchMock = vi.fn((_url: string, init?: RequestInit) => {
    if (init?.method && init.method !== "GET") {
      return Promise.resolve(new Response(null, { status: 204 }));
    }
    return Promise.resolve(new Response(JSON.stringify(cart), { status: 200 }));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

// The single command (non-GET) request the interaction issued — its [url, init].
function commandCall(fetchMock: ReturnType<typeof stubCartFetch>) {
  return fetchMock.mock.calls.find(([, init]) => init?.method && init.method !== "GET") as
    | [string, RequestInit]
    | undefined;
}

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

  it("[+] issues ChangeCartItemQuantity with newQuantity = N+1 to the header-keyed URL", async () => {
    const fetchMock = stubCartFetch(openCart); // crit-001 @ qty 2
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.click(
      screen.getByRole("button", { name: "Increase quantity of Cosmic Critter Plush" }),
    );

    await waitFor(() => expect(commandCall(fetchMock)).toBeDefined());
    const [url, init] = commandCall(fetchMock)!;
    expect(url).toContain("/carts/mine/items/crit-001/quantity");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ newQuantity: 3 }); // 2 + 1, absolute
  });

  it("[-] issues ChangeCartItemQuantity with newQuantity = N-1", async () => {
    const fetchMock = stubCartFetch(openCart); // crit-001 @ qty 2
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.click(
      screen.getByRole("button", { name: "Decrease quantity of Cosmic Critter Plush" }),
    );

    await waitFor(() => expect(commandCall(fetchMock)).toBeDefined());
    const [, init] = commandCall(fetchMock)!;
    expect(JSON.parse(init.body as string)).toEqual({ newQuantity: 1 }); // 2 - 1
  });

  it("disables [-] at quantity 1 — removal is only ever the explicit [x] (locked decision 2)", async () => {
    stubCartFetch(singleQtyCart); // crit-001 @ qty 1
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    expect(
      screen.getByRole("button", { name: "Decrease quantity of Cosmic Critter Plush" }),
    ).toBeDisabled();
    // The plus and remove controls stay live at qty 1.
    expect(
      screen.getByRole("button", { name: "Increase quantity of Cosmic Critter Plush" }),
    ).toBeEnabled();
    expect(
      screen.getByRole("button", { name: "Remove Cosmic Critter Plush from cart" }),
    ).toBeEnabled();
  });

  it("[x] issues a header-keyed DELETE to remove the line", async () => {
    const fetchMock = stubCartFetch(openCart);
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.click(
      screen.getByRole("button", { name: "Remove Cosmic Critter Plush from cart" }),
    );

    await waitFor(() => expect(commandCall(fetchMock)).toBeDefined());
    const [url, init] = commandCall(fetchMock)!;
    expect(url).toContain("/carts/mine/items/crit-001");
    expect(init.method).toBe("DELETE");
  });

  it("[ Place Order ] POSTs { customerId } to /orders and disables while placing (checkout fires PlaceOrder)", async () => {
    // A never-settling POST keeps the mutation pending: usePlaceOrder's onSuccess navigate (to a route absent
    // from this throwaway router) never fires, so the durable evidence is the command call + the pending state.
    const fetchMock = vi.fn((_url: string, init?: RequestInit) => {
      if (init?.method === "POST") return new Promise<Response>(() => {});
      return Promise.resolve(new Response(JSON.stringify(openCart), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.click(screen.getByRole("button", { name: "Place Order" }));

    await waitFor(() => expect(commandCall(fetchMock)).toBeDefined());
    const [url, init] = commandCall(fetchMock)!;
    expect(url).toMatch(/\/orders$/); // POST /orders — body-keyed, not the route-keyed /carts/{id}/...
    expect(JSON.parse(init.body as string)).toEqual({ customerId: "customer-demo" });
    // The button enters its pending state (no optimistic guess; it waits for the server).
    expect(screen.getByRole("button", { name: "Placing…" })).toBeDisabled();
  });
});
