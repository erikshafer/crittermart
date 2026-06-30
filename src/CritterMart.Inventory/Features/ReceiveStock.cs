using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine;
using Wolverine.Http;

namespace CritterMart.Inventory.Features;

// The Operator records a stock receipt (Workshop 001 slice 2.1). SKU from the route.
public record ReceiveStock(int Quantity);

public static class ReceiveStockEndpoint
{
    [WolverinePost("/stock/{sku}/receipts")]
    public static async Task Post(string sku, ReceiveStock command, IDocumentSession session, IMessageBus bus)
    {
        // Create-or-append: FetchForWriting handles both the first receipt (starts the
        // stream) and subsequent receipts (appends). [Aggregate] can't create a missing
        // stream and StartStream throws on an existing one — FetchForWriting does both.
        var stream = await session.Events.FetchForWriting<StockLevel>(sku);
        stream.AppendOne(new StockReceived(sku, command.Quantity));
        // AutoApplyTransactions commits; the inline StockLevel snapshot and the StockLevelView read
        // projection both fold the StockReceived and update available.

        // Slice 2.6: announce the receipt to any open Replenishment saga for this SKU as a dedicated
        // RestockArrived message (resolution #15). PUBLISHED, not returned, so the endpoint's 204 No Content
        // is preserved; the publish is outbox-enlisted (IntegrateWithWolverine + AutoApplyTransactions), so
        // it dispatches only after the receipt commits. No open saga for the SKU → silent no-op (saga NotFound).
        await bus.PublishAsync(new RestockArrived(sku, command.Quantity));
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
