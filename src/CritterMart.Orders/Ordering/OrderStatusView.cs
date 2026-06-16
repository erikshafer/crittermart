namespace CritterMart.Orders.Ordering;

// OrderStatusView — the order's READ model (ADR 020): the public projection W3/W4 bind via
// GET /orders/{orderId}. A DEDICATED inline projection from the order's events, decoupled from the
// Order aggregate — the read path never touches the protected write / PMvH model. Its shape currently
// mirrors the aggregate (this order's public view ≈ its decision state), but it is free to diverge — the
// aggregate can grow write-only decision fields (retry counts, gate flags) without leaking them here. The
// wire shape `{ id, customerId, status, lines, total }` is preserved EXACTLY so the W3 frontend
// (OrderStatusViewSchema, PR #62) and the deferred W4 are unchanged. Note `total` is on the view (the
// server computed it), so the screen renders it directly rather than re-summing the lines.
//
// Self-aggregating inline snapshot, registered `Projections.Snapshot<OrderStatusView>(SnapshotLifecycle.Inline)`.
// The fold mirrors the Order aggregate's (same five events), so read and write stay consistent. This replaced
// the former OrderStatusViewProjection : SingleStreamProjection class in the ADR 020/021 rollout — the same
// folds, now static methods on an immutable record, matching the Cart/CartView pilot shape.
public sealed record OrderStatusView(
    string Id,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderLine> Lines,
    decimal Total)
{
    // Genesis (slice 4.1): the placed order — lines, total, and the awaiting_confirmation status.
    public static OrderStatusView Create(OrderPlaced e) =>
        new(e.OrderId, e.CustomerId, OrderStatus.AwaitingConfirmation, [.. e.Items], e.Total);

    // Klefter grant (slice 4.2): the stock gate is cleared.
    public static OrderStatusView Apply(StockReserved e, OrderStatusView view) =>
        view with { Status = OrderStatus.StockReserved };

    // Klefter grant (slice 4.3): the payment gate is cleared. PaymentAuthFailed has no Apply — like
    // StockReservationFailed it is recorded for audit but drives no status change; the OrderCancelled that
    // follows it (slice 4.6) is what the Customer sees.
    public static OrderStatusView Apply(PaymentAuthorized e, OrderStatusView view) =>
        view with { Status = OrderStatus.PaymentAuthorized };

    // Terminal success (slice 4.4): both gates closed. Appended together with PaymentAuthorized, so the
    // view settles on confirmed — payment_authorized is the transient intermediate.
    public static OrderStatusView Apply(OrderConfirmed e, OrderStatusView view) =>
        view with { Status = OrderStatus.Confirmed };

    // Terminal cancellation (slice 4.5 via OrderCancelled). StockReservationFailed itself is recorded on the
    // stream for audit but carries no view status change — the cancellation that follows it is what shows.
    public static OrderStatusView Apply(OrderCancelled e, OrderStatusView view) =>
        view with { Status = OrderStatus.Cancelled };
}
