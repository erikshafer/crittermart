namespace CritterMart.Orders.Order;

// Terminal event on the Order stream (Workshop 001 § 4, ADR 007): the order was cancelled.
// Slice 4.5 emits it with reason "stock_unavailable" when stock could not be reserved; slice 4.6
// emits "payment_declined" when payment is refused; "payment_timeout" (4.7) is still to come.
// A stock-failure cancellation reserved nothing, so it sends no cross-BC release; a payment-decline
// cancellation reserved stock first, so it publishes ReleaseStock to Inventory (slice 2.3).
public record OrderCancelled(string OrderId, string Reason);

// Cancellation reasons carried by OrderCancelled. stock_unavailable is reachable from slice 4.5
// and payment_declined from slice 4.6; payment_timeout arrives with slice 4.7.
public static class CancelReason
{
    public const string StockUnavailable = "stock_unavailable";
    public const string PaymentDeclined = "payment_declined";
}
