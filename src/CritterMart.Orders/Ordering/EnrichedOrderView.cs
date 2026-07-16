namespace CritterMart.Orders.Ordering;

// Read-time enrichment wrapper (slice 5.3): the OrderStatusView shape plus a CustomerName
// resolved from the consumer-local LocalCustomerView (Workshop 002 slice 5.3). Returned by
// GET /orders/{orderId} and GET /orders/mine instead of the raw OrderStatusView so that the
// customer's display name is included in the response without changing the OrderStatusView
// projection (which is a pure event-sourced fold and carries no cross-BC dependency).
//
// Wire shape: { id, customerId, status, lines, total, placedAt, cancelReason, subtotal, discount,
// couponCode, customerName } — the existing OrderStatusView fields preserved in order, then the slice-6.3
// pricing/coupon fields, then customerName last (as it was originally appended). This is ADDITIVE: existing
// deserializers that do not know the new fields simply ignore them. CustomerName is null when the
// LocalCustomerView is absent; CouponCode is null when no coupon was redeemed (Discount is then 0 and
// Subtotal == Total).
public record EnrichedOrderView(
    string Id,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    DateTimeOffset PlacedAt,
    string? CancelReason,
    decimal Subtotal,
    decimal Discount,
    string? CouponCode,
    string? CustomerName)
{
    public static EnrichedOrderView From(OrderStatusView view, string? customerName) =>
        new(view.Id, view.CustomerId, view.Status, view.Lines, view.Total,
            view.PlacedAt, view.CancelReason, view.Subtotal, view.Discount, view.CouponCode, customerName);
}
