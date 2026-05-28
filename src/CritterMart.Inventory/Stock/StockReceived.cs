namespace CritterMart.Inventory.Stock;

// Inventory is event-sourced: stock levels are derived from this and later
// Stock-stream events, not stored as a mutable number. (Workshop 001 § 2, § 4.)
public record StockReceived(string Sku, int Quantity);
