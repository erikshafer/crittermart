using CritterMart.Orders.Promotions;
using Marten;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Ordering;

// The payment gate, slices 4.3 (authorize) + 4.4 (confirm) + 4.6 (cancel on decline). This mirrors
// slice 4.2's reserve flow, but the provider call is in-process: a request hop that calls the
// provider boundary, then a translation hop that records the decision as a Klefter local commit on
// the Order stream and makes the follow-on aggregate decision (confirm or cancel-and-release). The
// chain is kicked off by StockReservedHandler cascading AuthorizePayment once the stock gate clears
// (see StockReservationOutcomeHandlers). The cancel path is the one hop that leaves the process:
// it publishes ReleaseStock back to Inventory over the broker.

// Hop 1 of the payment gate: call the stubbed provider and cascade its decision. No stream
// access here — this handler is purely the "reach the provider" boundary (the in-process
// counterpart of slice 4.2's cross-BC Inventory hop). The returned PaymentDecision is a
// cascading message; it has a local handler below, so it is processed in-process.
public static class AuthorizePaymentHandler
{
    public static async Task<PaymentDecision> Handle(
        AuthorizePayment message, IPaymentProvider provider, PaymentAuthDelay delay)
    {
        // DEMO AFFORDANCE: configurable pause so the stock_reserved → payment_authorized transition
        // is visible at speaking pace. Zero in tests and default config; set via Payment:AuthDelay.
        if (delay.Duration > TimeSpan.Zero)
            await Task.Delay(delay.Duration);
        return await provider.AuthorizeAsync(message);
    }
}

// Hop 2 of the payment gate: translate the provider's transient decision into a durable Klefter
// local commit on the Order stream, and make the follow-on aggregate decision — confirm on
// approval (slice 4.4) or cancel-and-release on decline (slice 4.6). Idempotent and order-sensitive
// via a stream-state guard (mirrors the 4.2 outcome handlers): it acts only while the order sits at
// the payment gate (stock_reserved). A duplicate decision, or one for an order already confirmed /
// terminal / unknown, is a silent no-op (returns null, so nothing is appended and nothing cascades).
//
// Returns a nullable tuple: exactly one of CommitStock (approve) or ReleaseStock (decline) is
// non-null; guard/no-op paths return (null, null). Wolverine sees both types at code-gen time and
// provisions outbound routing for each. Neither has a local handler, so conventional routing
// carries both to Inventory over RabbitMQ. A null tuple member simply doesn't cascade.
public static class PaymentDecisionHandler
{
    public static async Task<(Contracts.CommitStock?, Contracts.ReleaseStock?)> Handle(
        PaymentDecision message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<Order>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.StockReserved)
        {
            return (null, null);
        }

        if (message.Approved)
        {
            stream.AppendOne(new PaymentAuthorized(message.OrderId, message.AuthCode!, stream.Aggregate.Total));
            stream.AppendOne(new OrderConfirmed(message.OrderId));

            var commitLines = stream.Aggregate.Lines
                .Select(l => new Contracts.CommitStockLine(l.Sku, l.Quantity))
                .ToList();
            return (new Contracts.CommitStock(message.OrderId, commitLines), null);
        }

        stream.AppendOne(new PaymentAuthFailed(message.OrderId, message.Reason ?? "declined"));
        stream.AppendOne(new OrderCancelled(message.OrderId, CancelReason.PaymentDeclined));

        // Slice 6.4: return the coupon slot if this order redeemed one (tagged CouponRedemptionReleased on
        // the same stream, same transaction). No-op when CouponId is null. Independent of the stock release.
        // Slice 6.5: a per-customer coupon's release also carries the composite (coupon × customer) tag.
        session.AppendCouponRelease(
            message.OrderId, stream.Aggregate.CouponId, stream.Aggregate.CustomerId, stream.Aggregate.CouponPerCustomer);

        var lines = stream.Aggregate.Lines
            .Select(l => new Contracts.ReleaseStockLine(l.Sku, l.Quantity))
            .ToList();
        return (null, new Contracts.ReleaseStock(message.OrderId, lines));
    }
}
