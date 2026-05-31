using Marten.Events.Aggregation;

namespace CritterMart.Orders.Order;

// The status values an Order moves through. Slice 4.1 reaches AwaitingConfirmation; slice 4.2
// adds StockReserved (stock gate cleared) and Cancelled (stock failure → 4.5); slice 4.3 adds
// PaymentAuthorized (payment gate cleared) and slice 4.4 adds Confirmed (both gates closed —
// the terminal success state). All fold onto this same view as their events land.
public static class OrderStatus
{
    public const string AwaitingConfirmation = "awaiting_confirmation";
    public const string StockReserved = "stock_reserved";
    public const string PaymentAuthorized = "payment_authorized";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
}

// Inline snapshot of an Order stream — the Workshop's OrderStatusView read model. Id is the
// stream key (the generated orderId). Slice 4.1 projects the placed order (lines, total, and
// the awaiting_confirmation status); later slices fold StockReserved / PaymentAuthorized /
// OrderConfirmed / OrderCancelled into Status. (ADR 008: inline, no async daemon.)
public class OrderStatusView
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = [];
    public decimal Total { get; set; }
}

// Single-stream projection (Marten 9 partial-class convention). Apply(OrderPlaced) is a pure
// fold over the stream — unit-tested without a database, like CartViewProjection.
public partial class OrderStatusViewProjection : SingleStreamProjection<OrderStatusView, string>
{
    public void Apply(OrderPlaced e, OrderStatusView view)
    {
        view.CustomerId = e.CustomerId;
        view.Status = OrderStatus.AwaitingConfirmation;
        view.Lines = [.. e.Items];
        view.Total = e.Total;
    }

    // Klefter grant (slice 4.2): the stock gate is cleared.
    public void Apply(StockReserved e, OrderStatusView view) => view.Status = OrderStatus.StockReserved;

    // Klefter grant (slice 4.3): the payment gate is cleared. PaymentAuthFailed has no Apply —
    // like StockReservationFailed it is recorded for audit but drives no status change; the
    // OrderCancelled that follows it (slice 4.6) is what the Customer would see.
    public void Apply(PaymentAuthorized e, OrderStatusView view) => view.Status = OrderStatus.PaymentAuthorized;

    // Terminal success (slice 4.4): both gates closed. Appended together with PaymentAuthorized,
    // so the view settles on confirmed — payment_authorized is the transient intermediate.
    public void Apply(OrderConfirmed e, OrderStatusView view) => view.Status = OrderStatus.Confirmed;

    // Terminal cancellation (slice 4.5 reaches this via OrderCancelled). StockReservationFailed
    // itself is recorded on the stream for audit but carries no view status change — the
    // cancellation that follows it is what the Customer sees.
    public void Apply(OrderCancelled e, OrderStatusView view) => view.Status = OrderStatus.Cancelled;
}
