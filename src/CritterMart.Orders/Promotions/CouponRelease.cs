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
// Takes primitives (orderId, couponId?) to keep Promotions decoupled from the Ordering.Order type.
public static class CouponRelease
{
    public static void AppendCouponRelease(this IDocumentSession session, string orderId, string? couponId)
    {
        if (couponId is null)
        {
            return;
        }

        var released = session.Events.BuildEvent(new CouponRedemptionReleased(orderId, couponId));
        released.WithTag(new CouponId(couponId));
        session.Events.Append(orderId, released);
    }
}
