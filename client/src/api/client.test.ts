import { describe, it, expect, vi, afterEach } from "vitest";
import { z } from "zod";

import {
  ApiError,
  CUSTOMER_ID_HEADER,
  NotFoundError,
  deleteCommand,
  fetchParsed,
  postCommand,
} from "@/api/client";

const schema = z.object({ id: z.string() });

const ctx = { customerId: "customer-7" };

afterEach(() => {
  vi.restoreAllMocks();
});

describe("fetchParsed", () => {
  it("sets the X-Customer-Id header from the request context and parses the body", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ id: "crit-001" }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await fetchParsed("http://orders/carts/mine", schema, {
      customerId: "customer-7",
    });

    expect(result).toEqual({ id: "crit-001" });
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect((init.headers as Record<string, string>)[CUSTOMER_ID_HEADER]).toBe("customer-7");
  });

  it("throws NotFoundError on 404 so callers can treat it as a domain-empty state", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    await expect(
      fetchParsed("http://orders/carts/mine", schema, { customerId: "customer-7" }),
    ).rejects.toBeInstanceOf(NotFoundError);
  });

  it("rejects when the body fails the Zod boundary parse", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 42 }), { status: 200 })),
    );

    await expect(
      fetchParsed("http://orders/carts/mine", schema, { customerId: "customer-7" }),
    ).rejects.toThrow();
  });
});

describe("postCommand", () => {
  it("POSTs the body with the identity header and parses the response through the schema", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ id: "cart-1" }), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await postCommand("http://orders/carts/c-7/items", { sku: "crit-001" }, ctx, schema);

    expect(result).toEqual({ id: "cart-1" });
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[CUSTOMER_ID_HEADER]).toBe("customer-7");
    expect(JSON.parse(init.body as string)).toEqual({ sku: "crit-001" });
  });

  it("returns void and never reads the body when no schema is given (a 204 has none)", async () => {
    // A real 204 Response has an empty body; calling `.json()` on it would throw. Asserting the resolve
    // proves the no-schema path skips the parse entirely.
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    const result = await postCommand("http://orders/carts/c-7/items/crit-001/quantity", { newQuantity: 3 }, ctx);

    expect(result).toBeUndefined();
  });

  it("throws ApiError on a non-2xx response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    await expect(
      postCommand("http://orders/carts/c-7/items/crit-001/quantity", { newQuantity: 3 }, ctx),
    ).rejects.toBeInstanceOf(ApiError);
  });
});

describe("deleteCommand", () => {
  it("issues a DELETE with the identity header and resolves on a 204 (no body to parse)", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(deleteCommand("http://orders/carts/c-7/items/crit-001", ctx)).resolves.toBeUndefined();

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("http://orders/carts/c-7/items/crit-001");
    expect(init.method).toBe("DELETE");
    expect((init.headers as Record<string, string>)[CUSTOMER_ID_HEADER]).toBe("customer-7");
  });

  it("throws ApiError on a non-2xx response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    await expect(deleteCommand("http://orders/carts/c-7/items/crit-001", ctx)).rejects.toBeInstanceOf(
      ApiError,
    );
  });
});
