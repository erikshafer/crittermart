using CritterMart.Orders.Promotions;
using Marten;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Ordering;

// Inbound cross-BC outcome handlers (Workshop 001 slice 4.2). Inventory's reply lands here and
// is recorded as a Klefter local commit on the Order's own stream. Both are idempotent via a
// stream-state guard (design.md decision 8): they act only while the order still awaits
// confirmation, so a duplicate or late reply on an already-progressed, terminal, or unknown
// order is a silent no-op — exactly the at-least-once safety the Workshop § 6.1 scenarios require.

public static class StockReservedHandler
{
    public static async Task<AuthorizePayment?> Handle(Contracts.StockReserved message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<Order>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.AwaitingConfirmation)
        {
            return null; // terminal, already reserved, or unknown order — ignore (no cascade)
        }

        stream.AppendOne(new StockReserved(message.OrderId));

        // Stock gate cleared → open the payment gate (slice 4.3). Cascade AuthorizePayment for the
        // order total; it has a local handler, so Wolverine routes it in-process (local routing
        // wins over the RabbitMQ convention) rather than over the broker. The guard above makes
        // this idempotent: a duplicate StockReserved on an already-progressed order returns null.
        return new AuthorizePayment(message.OrderId, stream.Aggregate.Total);
    }
}

public static class StockReservationFailedHandler
{
    public static async Task Handle(Contracts.StockReservationFailed message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<Order>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.AwaitingConfirmation)
        {
            return; // terminal or already past the stock gate — ignore
        }

        // Slice 4.5: record the refusal, then cancel. No cross-BC release is published — the
        // all-or-nothing reservation means nothing was reserved to give back.
        stream.AppendOne(new StockReservationFailed(message.OrderId, message.Reason));
        stream.AppendOne(new OrderCancelled(message.OrderId, CancelReason.StockUnavailable));

        // Slice 6.4: if the order redeemed a coupon, return its slot to the pool (tagged
        // CouponRedemptionReleased on the same stream, same transaction). No-op when CouponId is null.
        // Slice 6.5: a per-customer coupon's release also carries the composite (coupon × customer) tag.
        session.AppendCouponRelease(
            message.OrderId, stream.Aggregate.CouponId, stream.Aggregate.CustomerId, stream.Aggregate.CouponPerCustomer);
    }
}
