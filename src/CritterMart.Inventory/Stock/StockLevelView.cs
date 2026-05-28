using Marten.Events.Aggregation;

namespace CritterMart.Inventory.Stock;

// Inline snapshot of a SKU's Stock stream — the readable stock level, projected
// from events (not a stored mutable number). Id is the SKU (the stream key).
// Reserved stays 0 until reservations land in slice 2.2.
public class StockLevelView
{
    public string Id { get; set; } = string.Empty;
    public int Available { get; set; }
    public int Reserved { get; set; }
}

// Single-stream projection (Marten 9 partial-class convention). Marten constructs
// the empty view for the genesis event and sets Id to the stream key (the SKU).
public partial class StockLevelViewProjection : SingleStreamProjection<StockLevelView, string>
{
    public void Apply(StockReceived e, StockLevelView view) => view.Available += e.Quantity;

    public void Apply(StockReserved e, StockLevelView view)
    {
        view.Available -= e.Quantity;
        view.Reserved += e.Quantity;
    }
}
