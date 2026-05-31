namespace CritterMart.Contracts;

// Cross-BC reply: Inventory granted the whole order's reservation (Workshop 001 slice 4.2).
// Order-level (no SKU) — the order asked as a unit and is answered as a unit. Orders records
// this as a Klefter local commit on the Order stream (Orders.Order.StockReserved).
public record StockReserved(string OrderId);
