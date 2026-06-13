using Contracts = CritterMart.Contracts;
using CritterMart.Inventory.Stock;
using Marten;

namespace CritterMart.Inventory.Features;

// Commit a confirmed order's reserved stock in response to a cross-BC CommitStock message from
// Orders (Workshop 001 slice 2.4). The mirror of ReleaseStockHandler — per-SKU idempotent via
// the Reservations guard, one-way (no reply to Orders). Converts reserved stock into committed
// stock; the reservation is no longer returnable.
public static class CommitStockHandler
{
    public static async Task Handle(Contracts.CommitStock message, IDocumentSession session)
    {
        foreach (var line in message.Lines)
        {
            var stream = await session.Events.FetchForWriting<StockLevelView>(line.Sku);

            if (stream.Aggregate?.Reservations.Contains(message.OrderId) == true)
            {
                stream.AppendOne(new StockCommitted(line.Sku, message.OrderId, line.Quantity));
            }
        }
    }
}
