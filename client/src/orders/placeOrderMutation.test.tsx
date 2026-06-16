import { describe, it, expect, vi, afterEach } from "vitest";
import type { ReactNode } from "react";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { usePlaceOrder } from "@/orders/placeOrderMutation";
import { cartKeys } from "@/cart/cartQueries";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

const CUSTOMER = "customer-demo";

// usePlaceOrder navigates on success via the router's useNavigate. Mock just that export so the navigate is
// observable here without a RouterProvider — the typed route wiring is exercised by the build's tsc pass and
// the OrderConfirmationPage / live-browser checks, not this unit.
const { navigateMock } = vi.hoisted(() => ({ navigateMock: vi.fn() }));
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return { ...actual, useNavigate: () => navigateMock };
});

function freshClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function makeWrapper(queryClient: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider customerId={CUSTOMER}>{children}</CurrentCustomerProvider>
    </QueryClientProvider>
  );
}

afterEach(() => {
  vi.restoreAllMocks();
  navigateMock.mockReset(); // a hoisted vi.fn() — restoreAllMocks doesn't clear its call history
});

describe("usePlaceOrder", () => {
  it("POSTs { customerId } to /orders, invalidates the cart, and navigates to the confirmation on success", async () => {
    const queryClient = freshClient();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ orderId: "ord-7f3a" }), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => usePlaceOrder(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toMatch(/\/orders$/); // POST /orders — NOT route-keyed by customer
    expect(init.method).toBe("POST");
    // Body-keyed identity (the 3rd transport shape): { customerId } rides the body, not the route. The order's
    // contents are NOT sent — the server resolves the customer's open cart.
    expect(JSON.parse(init.body as string)).toEqual({ customerId: CUSTOMER });
    // The cart badge resets: invalidating /carts/mine refetches → 404 → Cart (0).
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: cartKeys.mine(CUSTOMER) });
    // Navigate to W3, keyed by the returned orderId (the ONLY field the place response carries).
    expect(navigateMock).toHaveBeenCalledWith({
      to: "/orders/$orderId/confirmation",
      params: { orderId: "ord-7f3a" },
    });
  });

  it("surfaces a 409 (NoOpenCart / CartEmpty) as an error and does NOT navigate — the cart stays put", async () => {
    const queryClient = freshClient();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    const { result } = renderHook(() => usePlaceOrder(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    // No optimistic guess to roll back, and no navigate on failure — the customer stays on the cart.
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
