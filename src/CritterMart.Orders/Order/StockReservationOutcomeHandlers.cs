using Contracts = CritterMart.Contracts;
using Marten;

namespace CritterMart.Orders.Order;

// Inbound cross-BC outcome handlers (Workshop 001 slice 4.2). Inventory's reply lands here and
// is recorded as a Klefter local commit on the Order's own stream. Both are idempotent via a
// stream-state guard (design.md decision 8): they act only while the order still awaits
// confirmation, so a duplicate or late reply on an already-progressed, terminal, or unknown
// order is a silent no-op — exactly the at-least-once safety the Workshop § 6.1 scenarios require.

public static class StockReservedHandler
{
    public static async Task Handle(Contracts.StockReserved message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<OrderStatusView>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.AwaitingConfirmation)
        {
            return; // terminal, already reserved, or unknown order — ignore
        }

        stream.AppendOne(new StockReserved(message.OrderId));
    }
}

public static class StockReservationFailedHandler
{
    public static async Task Handle(Contracts.StockReservationFailed message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<OrderStatusView>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.AwaitingConfirmation)
        {
            return; // terminal or already past the stock gate — ignore
        }

        // Slice 4.5: record the refusal, then cancel. No cross-BC release is published — the
        // all-or-nothing reservation means nothing was reserved to give back.
        stream.AppendOne(new StockReservationFailed(message.OrderId, message.Reason));
        stream.AppendOne(new OrderCancelled(message.OrderId, CancelReason.StockUnavailable));
    }
}
