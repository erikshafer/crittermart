namespace CritterMart.Orders.Ordering;

// Read-time enrichment wrapper (slice 5.3): the OrderStatusView shape plus a CustomerName
// resolved from the consumer-local LocalCustomerView (Workshop 002 slice 5.3). Returned by
// GET /orders/{orderId} and GET /orders/mine instead of the raw OrderStatusView so that the
// customer's display name is included in the response without changing the OrderStatusView
// projection (which is a pure event-sourced fold and carries no cross-BC dependency).
//
// Wire shape: { id, customerId, status, lines, total, placedAt, cancelReason, customerName }
// — the existing OrderStatusView fields preserved in order, with customerName appended and
// nullable. This is ADDITIVE: existing deserializers that do not know customerName will simply
// ignore it. CustomerName is null when the LocalCustomerView is absent (eventually-consistent
// gap — the PL event may not have arrived yet when the first order read is made).
public record EnrichedOrderView(
    string Id,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    DateTimeOffset PlacedAt,
    string? CancelReason,
    string? CustomerName)
{
    public static EnrichedOrderView From(OrderStatusView view, string? customerName) =>
        new(view.Id, view.CustomerId, view.Status, view.Lines, view.Total,
            view.PlacedAt, view.CancelReason, customerName);
}
