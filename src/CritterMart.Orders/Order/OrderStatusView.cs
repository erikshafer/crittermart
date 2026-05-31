using Marten.Events.Aggregation;

namespace CritterMart.Orders.Order;

// The status values an Order moves through. Slice 4.1 only ever reaches AwaitingConfirmation;
// stock-reserved, payment-authorized, confirmed, and cancelled arrive with slices 4.2–4.7,
// folded onto this same view as those events land on the Order stream.
public static class OrderStatus
{
    public const string AwaitingConfirmation = "awaiting_confirmation";
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
}
