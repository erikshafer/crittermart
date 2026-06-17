using JasperFx.Events;

namespace CritterMart.Orders.Ordering;

// OrderStatusView — the order's READ model (ADR 020): the public projection W3/W4 bind via
// GET /orders/{orderId}. A DEDICATED inline projection from the order's events, decoupled from the
// Order aggregate — the read path never touches the protected write / PMvH model. The wire shape is
// { id, customerId, status, lines, total, placedAt, cancelReason }: the first five are preserved EXACTLY so
// the W3 frontend (OrderStatusViewSchema, PR #62) and W4 (PR #64) keep deserializing unchanged, and two
// ADDITIVE round-two fields (slice 025) the W4 tracking screen binds sit after them — `placedAt` (the
// order's placement time) and `cancelReason` (null until the order is cancelled). Note `total` is on the
// view (the server computed it), so the screen renders it directly rather than re-summing the lines.
//
// `placedAt` is the genesis OrderPlaced event's APPEND TIMESTAMP, surfaced from Marten event metadata via the
// IEvent<T> wrapper — "Marten's using-metadata convention", exactly as CartView surfaces LastActivityAt
// (CartView.cs:23). The Order aggregate deliberately does NOT carry it (Order.cs:23 — the write model needs
// no activity clock); the read view surfacing what the write model omits is the ADR 020 split in miniature.
// `cancelReason` folds the Reason that OrderCancelled already carries (the write aggregate ignores it; only
// this read view shows it), so the screen says WHICH failure befell the order rather than a bare "cancelled".
//
// Self-aggregating inline snapshot, registered `Projections.Snapshot<OrderStatusView>(SnapshotLifecycle.Inline)`.
// The fold mirrors the Order aggregate's (same five events; only Create reads the IEvent<T> wrapper, for the
// timestamp), so read and write stay consistent. This replaced the former OrderStatusViewProjection :
// SingleStreamProjection class in the ADR 020/021 rollout — the same folds, now static methods on an
// immutable record, matching the Cart/CartView pilot shape.
public sealed record OrderStatusView(
    string Id,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    DateTimeOffset PlacedAt,
    string? CancelReason)
{
    // Genesis (slice 4.1): the placed order — lines, total, the awaiting_confirmation status, and the
    // placement time (the OrderPlaced event's append timestamp, off the IEvent<T> wrapper — the view
    // surfacing metadata the Order aggregate never stores). CancelReason starts null; only a cancellation sets it.
    public static OrderStatusView Create(IEvent<OrderPlaced> e) =>
        new(e.Data.OrderId, e.Data.CustomerId, OrderStatus.AwaitingConfirmation, [.. e.Data.Items], e.Data.Total,
            e.Timestamp, CancelReason: null);

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

    // Terminal cancellation (slices 4.5 / 4.6 / 4.7 via OrderCancelled). Folds the event's Reason onto the
    // view (round-two slice 025): the write aggregate ignores OrderCancelled.Reason, but the read view
    // surfaces it so W4 shows the specific failure (stock_unavailable / payment_declined / payment_timeout),
    // not a bare "cancelled". StockReservationFailed/PaymentAuthFailed themselves are recorded on the stream
    // for audit but carry no view status change — the cancellation that follows them is what shows.
    public static OrderStatusView Apply(OrderCancelled e, OrderStatusView view) =>
        view with { Status = OrderStatus.Cancelled, CancelReason = e.Reason };
}
