using JasperFx.Events;
using Marten;

namespace CritterMart.Orders.Promotions;

// Slice 6.4 (Workshop 003): the compensating release, shared by the three order-cancellation sites
// (StockReservationFailedHandler 4.5, PaymentDecisionHandler decline 4.6, PaymentTimeoutHandler 4.7).
// Each already FetchForWriting<Order> and appends OrderCancelled; each calls this to ALSO append a tagged
// CouponRedemptionReleased to the same order stream, in the same transaction, IFF the order carried a
// redemption (Order.CouponId set). The DCB boundary and CouponUsageView count redemptions MINUS releases,
// so a cancelled order returns its flash-sale slot. A no-coupon order passes couponId == null → no-op.
//
// At-most-one-release is inherited free: OrderCancelled is terminal and appended once (Workshop 001's
// terminal-guard discipline, enforced by each handler's status guard), so this rides a single append.
// Takes primitives (orderId, couponId?, customerId?, perCustomer) to keep Promotions decoupled from the
// Ordering.Order type — the caller reads them off the loaded Order aggregate.
//
// Slice 6.5: when the redeemed coupon was per-customer, the release ALSO carries the composite (coupon ×
// customer) tag, so CustomerCouponUsage decrements and the customer's slot returns (the reserve/release
// symmetry, now on BOTH boundaries). The composite tag is rebuilt from the SAME (couponId, customerId) pair
// the redemption used — Order carries both, so no lookup is needed.
public static class CouponRelease
{
    public static void AppendCouponRelease(
        this IDocumentSession session, string orderId, string? couponId, string? customerId, bool perCustomer)
    {
        if (couponId is null)
        {
            return;
        }

        var released = session.Events.BuildEvent(new CouponRedemptionReleased(orderId, couponId));
        released.WithTag(new CouponId(couponId));

        // Per-customer coupon → also decrement the composite boundary for this (coupon, customer) pair.
        // customerId is always set for a redeemed order (it comes from OrderPlaced); guard defensively anyway.
        if (perCustomer && customerId is not null)
        {
            released.WithTag(CouponCustomerTag.For(couponId, customerId));
        }

        session.Events.Append(orderId, released);
    }
}
