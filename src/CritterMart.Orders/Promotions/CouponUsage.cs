using JasperFx.Events.Aggregation;

namespace CritterMart.Orders.Promotions;

// CouponUsage — the DCB BOUNDARY STATE (Workshop 003 § 4/§ 7; ADR 024). The write-side decision state
// FetchForWritingByTags<CouponUsage> materializes ON DEMAND from every event tagged with a CouponId
// (net count = redemptions − releases), checks it against the cap inside the write transaction, and throws
// it away. It is NEVER persisted and NEVER queried by the UI — distinguish it from the advisory persisted
// CouponUsageView, which carries the same arithmetic but a different existence (a queryable projection).
//
// It is an identity-LESS boundary aggregate ([BoundaryAggregate]) — the textbook DCB shape: the consistency
// boundary aligns with the COUPON (the tag), not with any single stream/aggregate. Marten DCB boundary
// aggregates use the mutable-class + void-Apply convention (JasperFx.Events.Aggregation), a deliberate
// divergence from CritterMart's immutable-record read models (ADR 020) — this is a transient write-side
// fold, not a read model, and the DCB API drives it by that convention.
[BoundaryAggregate]
public class CouponUsage
{
    // Net redemptions of this coupon across all order streams. Redemptions raise it, releases lower it;
    // the checkout cap check is `NetCount < cap`.
    public int NetCount { get; set; }

    public void Apply(CouponRedeemed _) => NetCount++;

    public void Apply(CouponRedemptionReleased _) => NetCount--;
}
