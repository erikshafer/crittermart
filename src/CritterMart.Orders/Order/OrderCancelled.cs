namespace CritterMart.Orders.Order;

// Terminal event on the Order stream (Workshop 001 § 4, ADR 007): the order was cancelled.
// Slice 4.2/4.5 emits it with reason "stock_unavailable" when stock could not be reserved;
// later slices add reasons "payment_declined" (4.6) and "payment_timeout" (4.7). When the
// cancellation followed a stock failure no reservation existed, so no cross-BC release is sent.
public record OrderCancelled(string OrderId, string Reason);

// Cancellation reasons carried by OrderCancelled. Only stock_unavailable is reachable in
// slice 4.5; payment_declined / payment_timeout arrive with slices 4.6 / 4.7.
public static class CancelReason
{
    public const string StockUnavailable = "stock_unavailable";
}
