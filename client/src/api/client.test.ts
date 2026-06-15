import { describe, it, expect, vi, afterEach } from "vitest";
import { z } from "zod";

import { CUSTOMER_ID_HEADER, NotFoundError, fetchParsed } from "@/api/client";

const schema = z.object({ id: z.string() });

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
