import { useMutation } from "@tanstack/react-query";

import { fetchParsed, useApiContext, type RequestContext } from "@/api/client";
import { serviceUrls } from "@/config";

import { CouponValidationSchema, type CouponValidation } from "./couponSchema";

// The advisory coupon-validate layer (slice 6.2). Applying a code on W2 is a USER-INITIATED fetch — the
// customer types a code and taps Apply — so it is modeled as a `useMutation` rather than an auto-running
// `useQuery`: nothing should fire until the tap, and each tap is a fresh imperative check. (It is a GET under
// the hood, but "run on demand, expose isPending/data/reset" is the mutation ergonomics we want here; there is
// nothing to cache — the answer is advisory and short-lived.)

// Fetch the advisory validation for a code. Standalone + pure (no React) so a test can drive it with a literal
// context + mocked fetch, mirroring `fetchMyCart`. The endpoint always answers `200` with a discriminated body
// (valid/invalid/exhausted) — checking a code is not an error — so there is no 404/empty branch here.
export async function fetchCouponValidation(
  code: string,
  ctx: RequestContext,
): Promise<CouponValidation> {
  return fetchParsed(
    `${serviceUrls.ordersUrl}/coupons/${encodeURIComponent(code)}/validate`,
    CouponValidationSchema,
    ctx,
  );
}

// The Apply hook the W2 coupon field binds. `mutate(code)` fires the advisory query; `data` is the parsed
// `CouponValidation` the page branches on (valid → hold + preview; invalid/exhausted → inline error). `reset`
// clears it when the customer edits the code again. The mutation deliberately holds no optimistic state — the
// discount preview is derived from the returned `discountPercent`, not guessed.
export function useValidateCoupon() {
  const ctx = useApiContext();
  return useMutation({
    mutationFn: (code: string) => fetchCouponValidation(code, ctx),
  });
}
