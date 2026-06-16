namespace CritterMart.Orders.Ordering;

// Klefter local commit on the Order stream (Workshop 001 § 4, slice 4.2): Orders records
// Inventory's grant of stock as a first-class fact on the order's own stream. Order-level
// (no SKU) — the order reserved as a unit. Same conceptual fact as Inventory's per-SKU
// Stock-stream StockReserved and the Contracts.StockReserved wire message, persisted here
// for the Order's own audit trail (design.md decision 5).
public record StockReserved(string OrderId);
