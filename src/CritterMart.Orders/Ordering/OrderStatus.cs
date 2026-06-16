namespace CritterMart.Orders.Ordering;

// The status values an Order moves through. Slice 4.1 reaches AwaitingConfirmation; slice 4.2
// adds StockReserved (stock gate cleared) and Cancelled (stock failure → 4.5); slice 4.3 adds
// PaymentAuthorized (payment gate cleared) and slice 4.4 adds Confirmed (both gates closed —
// the terminal success state). Shared by the Order write aggregate (which folds them as its
// PMvH decision state), the OrderStatusView read model (which surfaces them on the wire), and
// the cross-BC handlers (whose stream-state guards branch on them). Extracted to its own file in
// the ADR 020/021 rollout, when the single OrderStatusView type that used to host it split in two.
public static class OrderStatus
{
    public const string AwaitingConfirmation = "awaiting_confirmation";
    public const string StockReserved = "stock_reserved";
    public const string PaymentAuthorized = "payment_authorized";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
}
