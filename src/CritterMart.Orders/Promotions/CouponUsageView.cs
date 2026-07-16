using JasperFx.Events;
using Marten.Events.Projections;

namespace CritterMart.Orders.Promotions;

// CouponUsageView — the ADVISORY per-coupon usage read model (Workshop 003 § 7). Net redemption count
// per coupon, for display ("N left" / "no longer available", slice 6.2's affordance and any seller readout).
//
// ADVISORY BY DESIGN: it is a projection and MAY lag; the authoritative cap count is only ever computed
// inside the DCB boundary (CouponUsage) at write time. Distinguish it from that never-persisted boundary
// state — same arithmetic, different existence. INLINE (SnapshotLifecycle equivalent for multi-stream:
// ProjectionLifecycle.Inline) because no async daemon runs in this project (an async advisory view would sit
// perpetually empty and could not serve the display it exists for) — settled with Erik, Workshop 003 §8 item 2.
public class CouponUsageView
{
    public string Id { get; set; } = string.Empty;   // the couponId
    public int NetCount { get; set; }                 // redemptions − releases
}

// The codebase's second MULTI-stream projection (CartAbandonmentReport was the first): it folds events from
// MANY order streams into one document per coupon, keyed by the events' couponId member (ordinary
// multi-stream identity routing — no tags needed here; only the write-side DCB boundary uses tags).
//
// `partial` is load-bearing (Marten 9 convention): conventional Apply methods are dispatched by the
// compile-time JasperFx source generator, which needs a partial class to extend — without it the host
// refuses to boot with InvalidProjectionException (docs/skills/marten-projection-conventions, DEBT row 1).
public partial class CouponUsageViewProjection : MultiStreamProjection<CouponUsageView, string>
{
    public CouponUsageViewProjection()
    {
        // Route both redemption events to the usage document for their coupon.
        Identity<CouponRedeemed>(e => e.CouponId);
        Identity<CouponRedemptionReleased>(e => e.CouponId);
    }

    public void Apply(CouponRedeemed e, CouponUsageView view) => view.NetCount++;

    public void Apply(CouponRedemptionReleased e, CouponUsageView view) => view.NetCount--;
}
