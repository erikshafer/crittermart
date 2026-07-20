import { describe, it, expect, vi, afterEach } from "vitest";

import { fetchCouponValidation } from "@/cart/couponQueries";

const ctx = { token: "jwt-demo", customerId: "customer-demo" };

afterEach(() => {
  vi.restoreAllMocks();
});

describe("fetchCouponValidation", () => {
  it("parses a valid coupon (status + discountPercent) and hits the validate URL", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ code: "FLASH20", status: "valid", discountPercent: 20 }), {
          status: 200,
        }),
      );
    vi.stubGlobal("fetch", fetchMock);

    const result = await fetchCouponValidation("FLASH20", ctx);

    expect(result.status).toBe("valid");
    expect(result.discountPercent).toBe(20);
    // The advisory read hits GET /coupons/{code}/validate (code URL-encoded).
    const [url] = fetchMock.mock.calls[0] as [string];
    expect(url).toMatch(/\/coupons\/FLASH20\/validate$/);
  });

  it("parses an unknown code as invalid with a null discountPercent", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ code: "BOGUS", status: "invalid", discountPercent: null }), {
            status: 200,
          }),
        ),
    );

    const result = await fetchCouponValidation("BOGUS", ctx);

    expect(result.status).toBe("invalid");
    expect(result.discountPercent).toBeNull();
  });

  it("parses an advisorily-exhausted coupon", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ code: "FLASH20", status: "exhausted", discountPercent: null }), {
            status: 200,
          }),
        ),
    );

    const result = await fetchCouponValidation("FLASH20", ctx);

    expect(result.status).toBe("exhausted");
  });

  // Slice 6.6: the fourth status. It reaches us only when the bearer token rode the call (the endpoint is
  // optionally authenticated) AND the coupon carries the per-customer policy — but the parse must accept it,
  // and it must not be mistaken for a valid answer: no discountPercent to price.
  it("parses an already-redeemed per-customer coupon", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(
            JSON.stringify({ code: "FIRSTORDER", status: "already_redeemed", discountPercent: null }),
            { status: 200 },
          ),
        ),
    );

    const result = await fetchCouponValidation("FIRSTORDER", ctx);

    expect(result.status).toBe("already_redeemed");
    expect(result.discountPercent).toBeNull();
  });

  // The z.enum boundary guard: a status the backend never sends fails loud rather than rendering as mystery UI.
  it("rejects an unexpected status at the boundary", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ code: "FLASH20", status: "maybe", discountPercent: 20 }), {
            status: 200,
          }),
        ),
    );

    await expect(fetchCouponValidation("FLASH20", ctx)).rejects.toThrow();
  });

  // URL-encoding: a code with a reserved character must not break the path.
  it("URL-encodes the code", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ code: "A B", status: "invalid", discountPercent: null }), {
          status: 200,
        }),
      );
    vi.stubGlobal("fetch", fetchMock);

    await fetchCouponValidation("A B", ctx);

    const [url] = fetchMock.mock.calls[0] as [string];
    expect(url).toContain("/coupons/A%20B/validate");
  });
});
