using Contracts = CritterMart.Contracts;
using CritterMart.Inventory.Stock;
using JasperFx.Events;
using Marten;

namespace CritterMart.Inventory.Features;

// Reserve a whole order's stock in response to a cross-BC ReserveStock message from Orders
// (Workshop 001 slice 4.2). This replaces the interim slice-2.2 HTTP route (POST
// /stock/{sku}/reservations) — the message is now the sole trigger (design.md decision 7).
//
// All-or-nothing across the order's lines (decision 2): every line's SKU Stock stream gets a
// StockReserved in one transaction, or none does. Idempotent against duplicate delivery via a
// per-SKU reservation guard on StockLevelView (decision 8). The granted/refused outcome is
// cascaded back to Orders as a Contracts message (routed over RabbitMQ by conventional routing).
public static class ReserveStockHandler
{
    public static async Task<object> Handle(Contracts.ReserveStock message, IDocumentSession session)
    {
        // Load each line's stream once: the StockLevelView aggregate carries available stock and
        // the order ids already reserved, and the IEventStream is what we append the grant to.
        var streams = new List<(Contracts.ReserveStockLine Line, IEventStream<StockLevelView> Stream)>();
        foreach (var line in message.Lines)
        {
            var stream = await session.Events.FetchForWriting<StockLevelView>(line.Sku);
            streams.Add((line, stream));
        }

        // Idempotency (at-least-once): if this order already holds a reservation on these streams,
        // do not reserve again — re-publish the granted outcome (Orders is itself idempotent).
        if (streams.Any(s => s.Stream.Aggregate?.Reservations.Contains(message.OrderId) == true))
        {
            return new Contracts.StockReserved(message.OrderId);
        }

        // Refuse the entire order if ANY line has insufficient (or no) available stock. No stream
        // is modified, so nothing is reserved and the order's cancellation releases nothing.
        var anyShort = streams.Any(s =>
            s.Stream.Aggregate is null || s.Stream.Aggregate.Available < s.Line.Quantity);
        if (anyShort)
        {
            return new Contracts.StockReservationFailed(message.OrderId, "insufficient");
        }

        // Reserve every line on its SKU stream; AutoApplyTransactions commits them together.
        foreach (var (line, stream) in streams)
        {
            stream.AppendOne(new StockReserved(line.Sku, message.OrderId, line.Quantity));
        }

        return new Contracts.StockReserved(message.OrderId);
    }
}
