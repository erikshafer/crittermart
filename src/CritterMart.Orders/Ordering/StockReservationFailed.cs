namespace CritterMart.Orders.Ordering;

// Klefter local commit on the Order stream (Workshop 001 § 4, slice 4.2): Orders records
// Inventory's refusal as a first-class fact on the order's own stream. Not on any Stock
// stream — a refusal is not a state change there. Precedes OrderCancelled(stock_unavailable)
// in the same handler (slice 4.5).
public record StockReservationFailed(string OrderId, string Reason);
