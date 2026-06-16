using Marten;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Ordering;

// The fired payment deadline (Workshop 001 slice 4.7, Bruun temporal automation). Scheduled by
// PlaceOrder when the order was placed; delivered here when the deadline passes. This handler is
// the purest expression of the stream-state guard idiom every Order handler uses: an order that
// reached a terminal state before its timer fired makes the timer a silent no-op — losing that
// race is the timer's normal, expected fate for every successfully confirmed order.
//
// The cancel path appends OrderCancelled { payment_timeout } and ALWAYS cascades ReleaseStock to
// Inventory (design.md Decision 2) — even when this stream never recorded a StockReserved grant.
// A timeout firing at awaiting_confirmation cannot know whether Inventory granted (the reply may
// be lost or still in flight), so instead of proving the reservation exists — the way 4.6's
// payment-gate guard can — it delegates: Inventory's per-SKU reservation guard (slice 2.3) no-ops
// wherever nothing is actually held. This is what survives the Workshop § 4.7 delayed-grant race
// without leaking a reservation. The contract and the Inventory handler are slice 4.6's, unchanged.
public static class PaymentTimeoutHandler
{
    public static async Task<Contracts.ReleaseStock?> Handle(OrderPaymentTimeout message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<Order>(message.OrderId);

        // Terminal-state guard: confirmed, already cancelled (including by a duplicate of this
        // very timeout), or unknown — append nothing, cascade nothing.
        if (stream.Aggregate is null
            || stream.Aggregate.Status is OrderStatus.Confirmed or OrderStatus.Cancelled)
        {
            return null;
        }

        // Non-terminal at the deadline (awaiting_confirmation or stock_reserved): the order never
        // settled, so the deadline ends it.
        stream.AppendOne(new OrderCancelled(message.OrderId, CancelReason.PaymentTimeout));

        // Always release. ReleaseStock has no Orders-local handler, so conventional routing
        // carries it to Inventory over the broker — the same path 4.6's decline-cancel uses.
        var lines = stream.Aggregate.Lines
            .Select(l => new Contracts.ReleaseStockLine(l.Sku, l.Quantity))
            .ToList();
        return new Contracts.ReleaseStock(message.OrderId, lines);
    }
}
