namespace CritterMart.Orders.Order;

// Klefter local commit on the Order stream (Workshop 001 § 4, slice 4.3 failure branch): Orders
// records the stubbed provider's refusal as a first-class fact on the order's own stream. Like
// StockReservationFailed, it carries no OrderStatusView status change of its own — the
// OrderCancelled { reason: "payment_declined" } that turns the order terminal is slice 4.6
// (which also releases the already-reserved stock cross-BC) and is deliberately deferred. Until
// 4.6 lands, a declined order records this event but its visible status stays stock_reserved.
public record PaymentAuthFailed(string OrderId, string Reason);
