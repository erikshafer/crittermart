import { describe, it, expect, vi, afterEach } from "vitest";
import { z } from "zod";

import {
  ApiError,
  NotFoundError,
  deleteCommand,
  fetchParsed,
  postCommand,
  type RequestContext,
} from "@/api/client";

const schema = z.object({ id: z.string() });

// An authenticated context carries the JWT (for the Authorization header) + the id (for cache keys).
const ctx: RequestContext = { token: "jwt-abc", customerId: "customer-7" };
// A logged-out context (e.g. Catalog browsing) carries no token — no auth header is sent.
const anonCtx: RequestContext = { token: null, customerId: "" };

function authHeader(init: RequestInit): string | undefined {
  return (init.headers as Record<string, string>).Authorization;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("fetchParsed", () => {
  it("sets the Authorization: Bearer header from the token and parses the body", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ id: "crit-001" }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await fetchParsed("http://orders/carts/mine", schema, ctx);

    expect(result).toEqual({ id: "crit-001" });
    expect(authHeader(fetchMock.mock.calls[0][1] as RequestInit)).toBe("Bearer jwt-abc");
  });

  it("sends no Authorization header when the context has no token (public reads)", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ id: "crit-001" }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await fetchParsed("http://catalog/products", schema, anonCtx);

    expect(authHeader(fetchMock.mock.calls[0][1] as RequestInit)).toBeUndefined();
  });

  it("throws NotFoundError on 404 so callers can treat it as a domain-empty state", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    await expect(fetchParsed("http://orders/carts/mine", schema, ctx)).rejects.toBeInstanceOf(
      NotFoundError,
    );
  });

  it("rejects when the body fails the Zod boundary parse", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 42 }), { status: 200 })),
    );

    await expect(fetchParsed("http://orders/carts/mine", schema, ctx)).rejects.toThrow();
  });
});

describe("postCommand", () => {
  it("POSTs the body with the bearer token and parses the response through the schema", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ id: "cart-1" }), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await postCommand("http://orders/carts/mine/items", { sku: "crit-001" }, ctx, schema);

    expect(result).toEqual({ id: "cart-1" });
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.method).toBe("POST");
    expect(authHeader(init)).toBe("Bearer jwt-abc");
    expect(JSON.parse(init.body as string)).toEqual({ sku: "crit-001" });
  });

  it("returns void and never reads the body when no schema is given (a 204 has none)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    const result = await postCommand("http://orders/carts/mine/items/crit-001/quantity", { newQuantity: 3 }, ctx);

    expect(result).toBeUndefined();
  });

  it("throws ApiError on a non-2xx response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    await expect(
      postCommand("http://orders/carts/mine/items/crit-001/quantity", { newQuantity: 3 }, ctx),
    ).rejects.toBeInstanceOf(ApiError);
  });
});

describe("deleteCommand", () => {
  it("issues a DELETE with the bearer token and resolves on a 204 (no body to parse)", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(deleteCommand("http://orders/carts/mine/items/crit-001", ctx)).resolves.toBeUndefined();

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("http://orders/carts/mine/items/crit-001");
    expect(init.method).toBe("DELETE");
    expect(authHeader(init)).toBe("Bearer jwt-abc");
  });

  it("throws ApiError on a non-2xx response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 409 })));

    await expect(deleteCommand("http://orders/carts/mine/items/crit-001", ctx)).rejects.toBeInstanceOf(
      ApiError,
    );
  });
});
