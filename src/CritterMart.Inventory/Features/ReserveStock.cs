using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Inventory.Features;

// Reserve stock against an order (Workshop 001 slice 2.2). Interim HTTP trigger;
// slice 4.2 routes ReserveStock from Orders over RabbitMQ to the same logic.
public record ReserveStock(string OrderId, int Quantity);

public static class ReserveStockEndpoint
{
    [WolverinePost("/stock/{sku}/reservations")]
    public static async Task<IResult> Post(string sku, ReserveStock command, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<StockLevelView>(sku);

        // Refuse if there is no stock for this SKU or not enough available — leave the
        // Stock stream unmodified (Workshop § 6.1: insufficient stock does not modify the stream).
        if (stream.Aggregate is null || stream.Aggregate.Available < command.Quantity)
        {
            return Results.Problem(
                title: "InsufficientStock",
                detail: $"Cannot reserve {command.Quantity} of '{sku}'; insufficient available stock.",
                statusCode: StatusCodes.Status409Conflict);
        }

        stream.AppendOne(new StockReserved(sku, command.OrderId, command.Quantity));
        // AutoApplyTransactions commits; the inline StockLevelView projection updates available/reserved.
        return Results.NoContent();
    }
}
