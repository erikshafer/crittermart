namespace CritterMart.Orders.Promotions;

// CouponView — the coupon DEFINITION read model (ADR 020): the code→definition lookup checkout uses
// to resolve a carried `couponCode` into its discount and cap. A self-aggregating inline snapshot over
// the coupon stream (registered Projections.Snapshot<CouponView>(SnapshotLifecycle.Inline)), keyed by
// the couponId (the stream key). The realized shape of ADR 024's "seed/local read model in the Orders store."
//
// Code uniqueness is a partial-unique index on `Code` (Program.cs) — the open-cart precedent. A duplicate
// DefineCoupon is caught by a pre-check query here and, under a race, by that index. A code-uniqueness DCB
// would be over-engineering for seed-issued definitions (Workshop 003 §8 item 3).
//
// Distinct from CouponUsageView (advisory net usage, slice 6.3) and the never-persisted CouponUsage DCB
// boundary state: CouponView is the definition; those two are the running count.
public sealed record CouponView(string Id, string Code, int DiscountPercent, int Cap)
{
    // Genesis (slice 6.1): the coupon was defined. Id is the couponId (the stream key); Code is the
    // human-facing lookup key; DiscountPercent and Cap are the terms.
    public static CouponView Create(CouponDefined e) =>
        new(e.CouponId, e.Code, e.DiscountPercent, e.Cap);
}
