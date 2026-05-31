namespace CritterMart.Contracts;

// Cross-BC reply: Inventory could not reserve the order's stock (Workshop 001 slice 4.2).
// Because reservation is all-or-nothing, a refusal reserved nothing — so the order's
// cancellation (slice 4.5) has no stock to release and crosses no boundary back. Orders
// records this as a Klefter local commit (Orders.Order.StockReservationFailed).
public record StockReservationFailed(string OrderId, string Reason);
