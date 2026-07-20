import { z } from "zod";

// CouponValidationSchema — the SPA's copy of the advisory validate contract Orders' `GET /coupons/{code}/validate`
// returns (slice 6.2, src/CritterMart.Orders/Features/ValidateCoupon.cs). Hand-written per Convention 2, NOT
// generated: when the endpoint's response shape changes, this schema changes in the same PR.
//
// **Discriminated by `status`.** The query always answers `200` — checking a code is not an error — with one of
// four statuses that map one-to-one onto the W2 UI states:
//   valid            → the code applies; `discountPercent` is present and the storefront prices the dollar
//                      amount against the cart total it already holds (the % → $ math is client-side; the
//                      server never sees cart money on this read).
//   already_redeemed → (slice 6.6) this SIGNED-IN customer has already used this per-customer coupon →
//                      "you've already used this coupon". The endpoint is OPTIONALLY authenticated: our bearer
//                      token rides every call via `authHeaders` (Convention 4), and the server answers this
//                      status only when it holds an identity AND the coupon carries the per-customer policy.
//                      A signed-out shopper can never see it — the anonymous answer is unchanged from 6.2.
//   exhausted        → a definition resolves but its advisory net count has reached the cap → "no longer
//                      available".
//   invalid          → no definition resolves for the code → "this code isn't valid".
//
// **Advisory only.** A `valid` answer here does NOT guarantee checkout succeeds (the DCB boundary re-decides,
// and the coupon may have been claimed since); an `exhausted` answer does not stop the customer carrying the
// code to a checkout that may still admit it if a slot freed. The checkout append is the sole authority
// (Workshop 003 §3) — this query is a convenience that previews the discount, never a guard.

// The closed status set — a `z.enum` (zod `schema-use-enums`, mirroring OrderStatusSchema): a status the
// backend never sends fails loud at the boundary rather than rendering through as mystery UI copy.
export const CouponStatusSchema = z.enum([
  "valid",
  "invalid",
  "exhausted",
  "already_redeemed",
]);

// `discountPercent` is present only for `valid` (a whole-number percent in (0, 100]); it is null for
// `invalid`/`exhausted`/`already_redeemed`. Modeled `.nullable()` (the wire sends `null`, not an absent key) — the UI reads it
// only on the `valid` branch, where the backend guarantees it non-null.
export const CouponValidationSchema = z.object({
  code: z.string(),
  status: CouponStatusSchema,
  discountPercent: z.number().int().positive().nullable(),
});

// `z.infer` over the schema (zod `type-use-z-infer`): the type IS the schema, so they cannot drift.
export type CouponStatus = z.infer<typeof CouponStatusSchema>;
export type CouponValidation = z.infer<typeof CouponValidationSchema>;
