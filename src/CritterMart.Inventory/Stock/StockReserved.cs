namespace CritterMart.Inventory.Stock;

// Stock committed to an order — the Stock stream's second event kind (after
// StockReceived). Decrements available and increments reserved in the projection.
// (Workshop 001 § 4, slice 2.2.)
public record StockReserved(string Sku, string OrderId, int Quantity);
