namespace CritterMart.Contracts;

// Cross-BC command: Orders asks Inventory to reserve the whole order's stock (Workshop 001
// slice 4.2). One message carries every line; Inventory reserves all lines atomically or
// none (design.md decision 2). This is the wire shape — distinct from Inventory's per-SKU
// Stock-stream StockReserved event and the order-level Klefter events on the Order stream.
public record ReserveStock(string OrderId, IReadOnlyList<ReserveStockLine> Lines);

// A single line to reserve: how much of a SKU the order needs.
public record ReserveStockLine(string Sku, int Quantity);
