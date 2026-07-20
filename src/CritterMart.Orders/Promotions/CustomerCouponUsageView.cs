using JasperFx.Events;
using Marten.Events.Projections;

namespace CritterMart.Orders.Promotions;

// CustomerCouponUsageView — the ADVISORY per-(coupon × customer) usage read model (Workshop 003 slice 6.6,
// § 7). Net redemption count for ONE pair, so the cart-review validate query can warn a signed-in customer
// that they have already used a per-customer coupon — BEFORE checkout refuses them (slice 6.5's 409).
//
// ADVISORY BY DESIGN, and doubly so. Like CouponUsageView it is a projection, and the authoritative
// per-customer answer is only ever the composite DCB boundary read (CustomerCouponUsage) at write time —
// this view is NEVER consulted at checkout and NEVER gates a redemption. Same arithmetic as that boundary,
// different existence: the boundary materializes on demand from tags and is thrown away; this is persisted
// and read by the UI. Note the one-character naming pair (CustomerCouponUsage / CustomerCouponUsageView),
// deliberate and mirroring CouponUsage / CouponUsageView.
//
// FORWARD-ONLY, and honestly so. Redemptions appended before CouponRedeemed carried CustomerId cannot be
// attributed, so they fold into an unattributed "{couponId}|" bucket that no query ever constructs (an
// authenticated caller always has a non-empty `sub`). The preview therefore may UNDER-warn — a customer whose
// only redemption predates the field sees `valid` and is refused at checkout exactly as today — and can never
// OVER-warn. That one-sided error is tolerable precisely because the read is advisory (Workshop 003 §6.6 has
// the scenario; it is tested, not worked around — a backfill would be its own slice with its own ADR).
public class CustomerCouponUsageView
{
    // The composite identity "{couponId}|{customerId}". Mirrors CouponCustomerTag's value shape — the boundary
    // and this view describe the same pair, so they spell it the same way — but this view takes NO dependency
    // on the tag type: a document identity keyed off a DCB tag record would imply a coupling that does not
    // exist. KeyFor is this view's own single construction site, used by both Identity routes AND by
    // ValidateCoupon's load, so the encoding cannot drift between writer and reader.
    public string Id { get; set; } = string.Empty;
    public int NetCount { get; set; }   // this customer's redemptions − releases of this coupon

    public static string KeyFor(string couponId, string customerId) => $"{couponId}|{customerId}";
}

// The codebase's third MULTI-stream projection (after CartAbandonmentReport and CouponUsageView): it folds
// events from MANY order streams into one document per (coupon, customer) pair, keyed by the events'
// CouponId + CustomerId MEMBERS. The member routing is the whole reason slice 6.6 amended the two events —
// a MultiStreamProjection has no seam to route by tag, and through slice 6.5 the pair existed only as a tag.
//
// `partial` is load-bearing (Marten 9 convention): conventional Apply methods are dispatched by the
// compile-time JasperFx source generator, which needs a partial class to extend — without it the host
// refuses to boot with InvalidProjectionException (docs/skills/marten-projection-conventions, DEBT row 1).
//
// INLINE (registered ProjectionLifecycle.Inline in Program.cs), for the reason CouponUsageView is inline: no
// async daemon runs in this project, so an async advisory view would sit perpetually empty and could not
// serve the preview it exists for (Workshop 003 §8 item 2).
public partial class CustomerCouponUsageViewProjection
    : MultiStreamProjection<CustomerCouponUsageView, string>
{
    public CustomerCouponUsageViewProjection()
    {
        // Route both redemption events to the usage document for their (coupon, customer) pair.
        Identity<CouponRedeemed>(e => CustomerCouponUsageView.KeyFor(e.CouponId, e.CustomerId));
        Identity<CouponRedemptionReleased>(e => CustomerCouponUsageView.KeyFor(e.CouponId, e.CustomerId));
    }

    public void Apply(CouponRedeemed e, CustomerCouponUsageView view) => view.NetCount++;

    public void Apply(CouponRedemptionReleased e, CustomerCouponUsageView view) => view.NetCount--;
}
