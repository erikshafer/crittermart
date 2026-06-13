namespace CritterMart.Inventory.Stock;

// A reservation permanently consumed — the Stock stream's fourth event kind. The terminal
// success counterpart of StockReleased: decrements reserved, increments committed, and drops
// the order from the SKU's live reservations.
// (Workshop 001 § 5, slice 2.4; reached when an order is confirmed.)
public record StockCommitted(string Sku, string OrderId, int Quantity);
