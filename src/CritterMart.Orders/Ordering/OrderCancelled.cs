namespace CritterMart.Orders.Ordering;

// Terminal event on the Order stream (Workshop 001 § 4, ADR 007): the order was cancelled.
// Slice 4.5 emits it with reason "stock_unavailable" when stock could not be reserved; slice 4.6
// emits "payment_declined" when payment is refused; slice 4.7 emits "payment_timeout" when the
// order's deadline passes without it settling. A stock-failure cancellation reserved nothing, so
// it sends no cross-BC release; the payment-decline and payment-timeout cancellations publish
// ReleaseStock to Inventory (slice 2.3) — decline because stock was provably reserved, timeout
// unconditionally (Inventory's guard decides; see PaymentTimeoutHandler).
public record OrderCancelled(string OrderId, string Reason);

// Cancellation reasons carried by OrderCancelled: stock_unavailable from slice 4.5,
// payment_declined from slice 4.6, payment_timeout from slice 4.7.
public static class CancelReason
{
    public const string StockUnavailable = "stock_unavailable";
    public const string PaymentDeclined = "payment_declined";
    public const string PaymentTimeout = "payment_timeout";
}
