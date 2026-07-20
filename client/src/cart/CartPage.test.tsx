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

  it("[ Place Order ] POSTs an empty body to /orders with the bearer token and disables while placing (checkout fires PlaceOrder)", async () => {
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
    expect(url).toMatch(/\/orders$/); // POST /orders — header-keyed, not route-keyed like /carts/{id}/...
    expect(JSON.parse(init.body as string)).toEqual({});
    // The button enters its pending state (no optimistic guess; it waits for the server).
    expect(screen.getByRole("button", { name: "Placing…" })).toBeDisabled();
  });

  // ── Slice 6.2: the coupon field ─────────────────────────────────────────────────────────────────

  // A fetch that serves the cart on GET /carts, the given coupon validation on GET /coupons/.../validate, and
  // a 204 on any command. Routing by URL keeps the coupon read from being answered with the cart payload.
  function stubCartAndCoupon(cart: unknown, coupon: unknown) {
    const fetchMock = vi.fn((url: string, init?: RequestInit) => {
      if (init?.method && init.method !== "GET") {
        return Promise.resolve(new Response(null, { status: 204 }));
      }
      if (String(url).includes("/coupons/")) {
        return Promise.resolve(new Response(JSON.stringify(coupon), { status: 200 }));
      }
      return Promise.resolve(new Response(JSON.stringify(cart), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  it("applies a valid coupon and previews the discounted Subtotal / Discount / Total (advisory)", async () => {
    stubCartAndCoupon(openCart, { code: "FLASH20", status: "valid", discountPercent: 20 });
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.type(screen.getByLabelText("Coupon code"), "FLASH20");
    await userEvent.click(screen.getByRole("button", { name: "Apply" }));

    // The applied chip + the priced breakdown: $103.98 − 20% ($20.80) = $83.18, all in integer cents.
    expect(await screen.findByText("Discount (FLASH20)")).toBeInTheDocument();
    // Scope to the summary <dt> — the table also has a per-line "Subtotal" column header.
    expect(screen.getByText("Subtotal", { selector: "dt" })).toBeInTheDocument();
    expect(screen.getByText("−$20.80")).toBeInTheDocument();
    expect(screen.getByText("$83.18")).toBeInTheDocument();
  });

  it("shows an inline error for an invalid code and applies no discount", async () => {
    stubCartAndCoupon(openCart, { code: "BOGUS", status: "invalid", discountPercent: null });
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.type(screen.getByLabelText("Coupon code"), "BOGUS");
    await userEvent.click(screen.getByRole("button", { name: "Apply" }));

    expect(await screen.findByText("This code isn't valid.")).toBeInTheDocument();
    // Nothing held: no discount line, the full total stands.
    expect(screen.queryByText(/^Discount \(/)).not.toBeInTheDocument();
    expect(screen.getByText("$103.98")).toBeInTheDocument();
  });

  it("shows the 'no longer available' error for an advisorily-exhausted coupon", async () => {
    stubCartAndCoupon(openCart, { code: "FLASH20", status: "exhausted", discountPercent: null });
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.type(screen.getByLabelText("Coupon code"), "FLASH20");
    await userEvent.click(screen.getByRole("button", { name: "Apply" }));

    expect(await screen.findByText("This coupon is no longer available.")).toBeInTheDocument();
  });

  // Slice 6.6: the personal reason, previewed at cart review instead of ambushing the shopper at checkout.
  // The server orders it ahead of `exhausted` so the copy points at the right remedy — "try another code",
  // not "try again later". Like the other refusals it holds nothing: no discount line, the full total stands.
  it("shows the 'already used' error for a per-customer coupon this customer redeemed", async () => {
    stubCartAndCoupon(openCart, {
      code: "FIRSTORDER",
      status: "already_redeemed",
      discountPercent: null,
    });
    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.type(screen.getByLabelText("Coupon code"), "FIRSTORDER");
    await userEvent.click(screen.getByRole("button", { name: "Apply" }));

    expect(await screen.findByText("You've already used this coupon.")).toBeInTheDocument();
    expect(screen.queryByText(/^Discount \(/)).not.toBeInTheDocument();
    expect(screen.getByText("$103.98")).toBeInTheDocument();
  });

  it("[ Place Order ] carries the applied coupon as ?couponCode= (advisory preview is not a guard)", async () => {
    // Cart + coupon on GET; a never-settling POST /orders keeps the placement pending (its onSuccess navigate
    // targets a route absent from this throwaway router), so the durable evidence is the command URL.
    const fetchMock = vi.fn((url: string, init?: RequestInit) => {
      if (init?.method === "POST" && String(url).includes("/orders")) {
        return new Promise<Response>(() => {});
      }
      if (String(url).includes("/coupons/")) {
        return Promise.resolve(
          new Response(JSON.stringify({ code: "FLASH20", status: "valid", discountPercent: 20 }), {
            status: 200,
          }),
        );
      }
      return Promise.resolve(new Response(JSON.stringify(openCart), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    renderCartPage();
    await screen.findByText("Cosmic Critter Plush");

    await userEvent.type(screen.getByLabelText("Coupon code"), "FLASH20");
    await userEvent.click(screen.getByRole("button", { name: "Apply" }));
    await screen.findByText("Discount (FLASH20)");

    await userEvent.click(screen.getByRole("button", { name: "Place Order" }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.find(([, init]) => (init as RequestInit)?.method === "POST"),
      ).toBeDefined(),
    );
    const orderCall = fetchMock.mock.calls.find(
      ([, init]) => (init as RequestInit)?.method === "POST",
    ) as [string, RequestInit];
    expect(orderCall[0]).toMatch(/\/orders\?couponCode=FLASH20$/);
  });
});
