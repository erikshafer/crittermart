import { describe, it, expect, vi, afterEach } from "vitest";
import type { ReactNode } from "react";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import {
  addLineToCart,
  removeLineFromCart,
  setLineQuantity,
  useAddToCart,
  useChangeCartItemQuantity,
  useRemoveCartItem,
} from "@/cart/cartMutations";
import { cartKeys } from "@/cart/cartQueries";
import type { CartView } from "@/cart/cartSchema";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

const CUSTOMER = "customer-demo";
const TOKEN = "jwt-demo";

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
      <CurrentCustomerProvider customerId={CUSTOMER} token={TOKEN}>
        {children}
      </CurrentCustomerProvider>
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

  it("POSTs to the header-keyed URL (/carts/mine) with the Authorization bearer token and a `productSnapshot` body", async () => {
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
    expect(url).toContain("/carts/mine/items"); // header-keyed (change 032), NOT /carts/{customerId}
    expect(url).not.toContain(CUSTOMER); // identity rides the header, never the path
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>).Authorization).toBe(`Bearer ${TOKEN}`);
    const body = JSON.parse(init.body as string);
    expect(body.productSnapshot).toEqual({ name: "Nebula Newt", price: 18.0 }); // the exact field name the backend binds
  });
});

// A two-line cart for the remove/change-qty merges (oneLineCart has only crit-001).
const twoLineCart: CartView = {
  ...oneLineCart,
  lines: [
    { sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 },
    { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.0 },
  ],
};

// ── The pure remove merge — drops the SKU's line, leaves an empty-but-open cart at zero. ──
describe("removeLineFromCart", () => {
  it("drops the targeted line and keeps the rest", () => {
    const cart = removeLineFromCart(twoLineCart, "crit-001");

    expect(cart?.lines.map((l) => l.sku)).toEqual(["crit-002"]);
  });

  it("leaves an empty-but-open cart when the last line is removed (NOT null)", () => {
    const cart = removeLineFromCart(oneLineCart, "crit-001");

    expect(cart).not.toBeNull();
    expect(cart?.isOpen).toBe(true);
    expect(cart?.lines).toEqual([]);
  });

  it("is a no-op on a null cart", () => {
    expect(removeLineFromCart(null, "crit-001")).toBeNull();
  });
});

// ── The pure change-quantity merge — absolute rewrite, never re-prices. ──
describe("setLineQuantity", () => {
  it("rewrites the line's quantity to the ABSOLUTE new value (not a delta)", () => {
    const cart = setLineQuantity(oneLineCart, "crit-001", 5); // from 2 → 5, not 2 + 5

    expect(cart?.lines[0].quantity).toBe(5);
  });

  it("never re-prices and leaves the snapshot name untouched", () => {
    const cart = setLineQuantity(oneLineCart, "crit-001", 5);

    expect(cart?.lines[0].price).toBe(24.99);
    expect(cart?.lines[0].name).toBe("Cosmic Critter Plush");
  });

  it("leaves other lines untouched", () => {
    const cart = setLineQuantity(twoLineCart, "crit-001", 5);

    expect(cart?.lines.find((l) => l.sku === "crit-002")?.quantity).toBe(3);
  });

  it("is a no-op on a null cart", () => {
    expect(setLineQuantity(null, "crit-001", 5)).toBeNull();
  });
});

// ── The remove hook — optimistic drop, rollback, route-keyed DELETE. ──
describe("useRemoveCartItem", () => {
  it("optimistically removes the line from the cached cart before the server answers", async () => {
    const queryClient = freshClient();
    const cartKey = cartKeys.mine(CUSTOMER);
    queryClient.setQueryData(cartKey, twoLineCart);
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>(() => {})));

    const { result } = renderHook(() => useRemoveCartItem(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate({ sku: "crit-001" });
    });

    await waitFor(() => {
      const cart = queryClient.getQueryData<CartView>(cartKey);
      expect(cart?.lines.map((l) => l.sku)).toEqual(["crit-002"]);
    });
  });

  it("rolls the optimistic removal back to the snapshot when the command fails", async () => {
    const queryClient = freshClient();
    const cartKey = cartKeys.mine(CUSTOMER);
    queryClient.setQueryData(cartKey, twoLineCart);
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    const { result } = renderHook(() => useRemoveCartItem(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate({ sku: "crit-001" });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(queryClient.getQueryData<CartView>(cartKey)?.lines).toHaveLength(2); // both lines restored
  });

  it("DELETEs the header-keyed URL (/carts/mine, sku in the path, identity in the header)", async () => {
    const queryClient = freshClient();
    queryClient.setQueryData(cartKeys.mine(CUSTOMER), twoLineCart);
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useRemoveCartItem(), { wrapper: makeWrapper(queryClient) });
    act(() => {
      result.current.mutate({ sku: "crit-001" });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/carts/mine/items/crit-001"); // header-keyed (change 032); only {sku} on the path
    expect(url).not.toContain(CUSTOMER);
    expect(init.method).toBe("DELETE");
    expect((init.headers as Record<string, string>).Authorization).toBe(`Bearer ${TOKEN}`);
  });
});

// ── The change-quantity hook — optimistic absolute set, rollback, route-keyed POST with { newQuantity }. ──
describe("useChangeCartItemQuantity", () => {
  it("optimistically sets the line's quantity before the server answers", async () => {
    const queryClient = freshClient();
    const cartKey = cartKeys.mine(CUSTOMER);
    queryClient.setQueryData(cartKey, oneLineCart); // crit-001 @ qty 2
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>(() => {})));

    const { result } = renderHook(() => useChangeCartItemQuantity(), {
      wrapper: makeWrapper(queryClient),
    });
    act(() => {
      result.current.mutate({ sku: "crit-001", newQuantity: 5 });
    });

    await waitFor(() => {
      const cart = queryClient.getQueryData<CartView>(cartKey);
      expect(cart?.lines[0].quantity).toBe(5); // absolute, not 2 + 5
    });
  });

  it("rolls the optimistic quantity back to the snapshot when the command fails", async () => {
    const queryClient = freshClient();
    const cartKey = cartKeys.mine(CUSTOMER);
    queryClient.setQueryData(cartKey, oneLineCart);
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    const { result } = renderHook(() => useChangeCartItemQuantity(), {
      wrapper: makeWrapper(queryClient),
    });
    act(() => {
      result.current.mutate({ sku: "crit-001", newQuantity: 5 });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(queryClient.getQueryData<CartView>(cartKey)?.lines[0].quantity).toBe(2); // restored
  });

  it("POSTs the header-keyed URL (/carts/mine, sku in the path) with a `{ newQuantity }` body (no response schema — 204)", async () => {
    const queryClient = freshClient();
    queryClient.setQueryData(cartKeys.mine(CUSTOMER), oneLineCart);
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useChangeCartItemQuantity(), {
      wrapper: makeWrapper(queryClient),
    });
    act(() => {
      result.current.mutate({ sku: "crit-001", newQuantity: 5 });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/carts/mine/items/crit-001/quantity"); // header-keyed (change 032); only {sku} on the path
    expect(url).not.toContain(CUSTOMER);
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>).Authorization).toBe(`Bearer ${TOKEN}`);
    expect(JSON.parse(init.body as string)).toEqual({ newQuantity: 5 });
  });
});
