using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Inventory.Features;

// The Operator records a stock receipt (Workshop 001 slice 2.1). SKU from the route.
public record ReceiveStock(int Quantity);

public static class ReceiveStockEndpoint
{
    [WolverinePost("/stock/{sku}/receipts")]
    public static async Task Post(string sku, ReceiveStock command, IDocumentSession session)
    {
        // Create-or-append: FetchForWriting handles both the first receipt (starts the
        // stream) and subsequent receipts (appends). [Aggregate] can't create a missing
        // stream and StartStream throws on an existing one — FetchForWriting does both.
        var stream = await session.Events.FetchForWriting<StockLevelView>(sku);
        stream.AppendOne(new StockReceived(sku, command.Quantity));
        // AutoApplyTransactions commits; the inline StockLevelView projection updates available.
    }
}

public static class StockLevelEndpoint
{
    [WolverineGet("/stock/{sku}")]
    public static async Task<IResult> Get(string sku, IQuerySession session)
    {
        var view = await session.LoadAsync<StockLevelView>(sku);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
