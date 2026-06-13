using CritterMart.Inventory.Stock;
using Marten;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Inventory.Features;

// Release a cancelled order's reserved stock in response to a cross-BC ReleaseStock message from
// Orders (Workshop 001 slice 2.3; first reached on payment decline, slice 4.6). The compensating
// counterpart of ReserveStockHandler — and the first cross-BC hop that flows cancellation back
// into Inventory. No reply is published: the cancellation already happened on the Order stream;
// this is a one-way release. Slice 4.7 (payment timeout) will publish the SAME message unchanged.
//
// Per-SKU idempotent (decision 3), NOT all-or-nothing like reserve: each line is released only if
// this order actually holds a reservation on that SKU (Reservations.Contains(orderId)). A line
// with no live reservation — duplicate delivery, already released, or (under 4.7) a grant that
// never landed — is a silent no-op for that SKU. Release is keyed on the reservation existing,
// not on event order, so it stays correct under at-least-once delivery and broker reordering.
public static class ReleaseStockHandler
{
    public static async Task Handle(Contracts.ReleaseStock message, IDocumentSession session)
    {
        foreach (var line in message.Lines)
        {
            var stream = await session.Events.FetchForWriting<StockLevelView>(line.Sku);

            // Release only what this order holds. No reservation for the order on this SKU means
            // nothing to give back (already released, or never reserved) — skip the line.
            if (stream.Aggregate?.Reservations.Contains(message.OrderId) == true)
            {
                stream.AppendOne(new StockReleased(line.Sku, message.OrderId, line.Quantity));
            }
        }
    }
}
