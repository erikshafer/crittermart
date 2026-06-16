import { describe, it, expect, vi, afterEach } from "vitest";
import type { ReactNode } from "react";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { addLineToCart, useAddToCart } from "@/cart/cartMutations";
import { cartKeys } from "@/cart/cartQueries";
import type { CartView } from "@/cart/cartSchema";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

const CUSTOMER = "customer-demo";

// A cart with one line — the snapshot for the rollback / merge tests.
const oneLineCart: CartView = {
  id: "cart-7f3a",
  customerId: CUSTOMER,
  isOpen: true,
  lines: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }],
  lastActivityAt: "2026-06-14T14:02:00+00:00",
};

const newtAdd = {
  sku: "crit-002",
  quantity: 1,
  productSnapshot: { name: "Nebula Newt", price: 18.0 },
};

afterEach(() => {
  vi.restoreAllMocks();
});

// ── The pure optimistic merge — the heart of the optimistic update, tested without React-Query timing. ──
describe("addLineToCart", () => {
  const newtLine = { sku: "crit-002", quantity: 1, name: "Nebula Newt", price: 18.0 };

  it("seeds a fresh open cart on a cold first add (no open cart yet)", () => {
    const cart = addLineToCart(null, newtLine, CUSTOMER);

    expect(cart.customerId).toBe(CUSTOMER);
    expect(cart.isOpen).toBe(true);
    expect(cart.lines).toEqual([newtLine]);
  });

  it("appends a new SKU to an existing cart (the badge count grows)", () => {
    const cart = addLineToCart(oneLineCart, newtLine, CUSTOMER);

    expect(cart.lines).toHaveLength(2);
    expect(cart.lines.map((l) => l.sku)).toEqual(["crit-001", "crit-002"]);
  });

  it("sums quantity for a SKU already in the cart — and never re-prices", () => {
    // Re-adding crit-001 with a bogus price must NOT change the authoritative snapshot price.
    const cart = addLineToCart(
      oneLineCart,
      { sku: "crit-001", quantity: 1, name: "Cosmic Critter Plush", price: 999 },
      CUSTOMER,
    );

    expect(cart.lines).toHaveLength(1);
    expect(cart.lines[0].quantity).toBe(3); // 2 + 1, merged
    expect(cart.lines[0].price).toBe(24.99); // snapshot price unchanged
  });
});

// ── The mutation hook — optimistic bump, rollback, contract wiring. ──
function makeWrapper(queryClient: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider customerId={CUSTOMER}>{children}</CurrentCustomerProvider>
    </QueryClientProvider>
  );
}

function freshClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

describe("useAddToCart", () => {
  it("optimistically adds the line to the cached cart before the server answers (the badge bumps)", async () => {
    const queryClient = freshClient();
    const cartKey = cartKeys.mine(CUSTOMER);
    queryClient.setQueryData(cartKey, oneLineCart);
    // A fetch that never settles, so the cache stays in its optimistic state for the assertion.
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>(() => {})));

    const { result } = renderHook(() => useAddToCart(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate(newtAdd);
    });

    await waitFor(() => {
      const cart = queryClient.getQueryData<CartView>(cartKey);
      expect(cart?.lines).toHaveLength(2); // crit-001 + the optimistic crit-002
    });
  });

  it("rolls the optimistic add back to the snapshot when the command fails", async () => {
    const queryClient = freshClient();
    const cartKey = cartKeys.mine(CUSTOMER);
    queryClient.setQueryData(cartKey, oneLineCart);
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 500 })));

    const { result } = renderHook(() => useAddToCart(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate(newtAdd);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    // onError restored the pre-mutation snapshot: back to the single line.
    expect(queryClient.getQueryData<CartView>(cartKey)?.lines).toHaveLength(1);
  });

  it("POSTs to the route-keyed URL with a `productSnapshot` body (not `snapshot`)", async () => {
    const queryClient = freshClient();
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ cartId: "cart-1" }), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useAddToCart(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate(newtAdd);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`/carts/${CUSTOMER}/items`); // route-keyed (locked decision 1), NOT /carts/mine
    expect(init.method).toBe("POST");
    const body = JSON.parse(init.body as string);
    expect(body.productSnapshot).toEqual({ name: "Nebula Newt", price: 18.0 }); // the exact field name the backend binds
  });
});
