namespace CritterMart.Inventory.Stock;

// A reservation given back to the pool — the Stock stream's third event kind (after
// StockReceived and StockReserved). The inverse of StockReserved: increments available,
// decrements reserved, and drops the order from the SKU's live reservations.
// (Workshop 001 § 4, slice 2.3; first reached when an order is cancelled on payment decline.)
public record StockReleased(string Sku, string OrderId, int Quantity);
