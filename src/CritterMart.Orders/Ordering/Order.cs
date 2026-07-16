namespace CritterMart.Orders.Ordering;

// The Order aggregate — the domain WRITE model (ADR 020) and the process-manager-via-handlers
// STATE. `sealed` and immutable: every Apply returns a NEW Order via `with`, never mutates in place.
// It is the FetchForWriting / StartStream target on the order's write paths and is NEVER serialized
// over HTTP — W3/W4 bind the separate OrderStatusView read model (OrderStatusView.cs), never this type.
//
// It lives in the `Ordering/` **verb feature folder** (namespace `CritterMart.Orders.Ordering`), not an
// `Order/` folder — the ADR 021 convention keeps the aggregate's canonical domain-noun name `Order` with
// no collision (a verb namespace never clashes with a noun type). The Cart aggregate set this template in
// the `Shopping/` folder (#59); this is the Order rollout.
//
// Self-aggregating inline snapshot (ADR 008, refined by ADR 020): the static Create/Apply methods ARE the
// projection, registered via `Projections.Snapshot<Order>(SnapshotLifecycle.Inline)`, so the aggregate is
// materialized in the same transaction as the event append. The fold is a pure function of the stream —
// unit-tested without a database (OrderProjectionTests). Id is the stream key (the generated orderId, which
// PlaceOrder uses as both the StartStream key and OrderPlaced.OrderId).
//
// Status is the load-bearing field: the cross-BC outcome handlers (StockReservationOutcomeHandlers,
// PaymentHandlers, PaymentTimeoutHandler) FetchForWriting<Order> and guard on this Status to stay
// idempotent (act only while AwaitingConfirmation / StockReserved), reading Total + Lines to shape the
// next cascade (AuthorizePayment, CommitStock, ReleaseStock). Folds are plain-event args — unlike Cart,
// the order tracks no activity timestamp, so no IEvent<T> wrapper is needed.
public sealed record Order(
    string Id,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    string? CouponId = null)
{
    // Genesis: the customer checked out their open cart (PlaceOrder starts the stream as
    // StartStream<Order>(orderId, new OrderPlaced(...))). The placed order awaits confirmation; the
    // cart's snapshotted lines + computed total are frozen on and never re-priced. Total is the DISCOUNTED
    // total (Subtotal − Discount) — what the downstream reservation/payment gates act on.
    public static Order Create(OrderPlaced e) =>
        new(e.OrderId, e.CustomerId, OrderStatus.AwaitingConfirmation, [.. e.Items], e.Total);

    // A coupon was redeemed at checkout (slice 6.3): the aggregate remembers WHICH coupon, so the three
    // cancellation sites (slice 6.4) can append the compensating CouponRedemptionReleased iff CouponId is set.
    // The write model tracks only the id it needs to decide the release — not the code or discount (those are
    // the read view's / the event's concern), matching Order's "carry only what the handlers read" shape.
    public static Order Apply(CritterMart.Orders.Promotions.CouponRedeemed e, Order order) =>
        order with { CouponId = e.CouponId };

    // The redemption was released on cancellation (slice 6.4): clear the id — the release is terminal (rides
    // the once-appended OrderCancelled), so this never fires twice and the id never resurrects.
    public static Order Apply(CritterMart.Orders.Promotions.CouponRedemptionReleased e, Order order) =>
        order with { CouponId = null };

    // Stock gate cleared (slice 4.2): StockReservedHandler appends this while AwaitingConfirmation, then
    // cascades AuthorizePayment. The Status advance is what its own idempotency guard reads on a replay.
    public static Order Apply(StockReserved e, Order order) => order with { Status = OrderStatus.StockReserved };

    // Payment gate cleared (slice 4.3). PaymentAuthFailed has no Apply — like StockReservationFailed it is
    // recorded for audit but drives no status change; the OrderCancelled that follows is the transition.
    public static Order Apply(PaymentAuthorized e, Order order) => order with { Status = OrderStatus.PaymentAuthorized };

    // Terminal success (slice 4.4): appended together with PaymentAuthorized, so the order settles on
    // confirmed — payment_authorized is the transient intermediate.
    public static Order Apply(OrderConfirmed e, Order order) => order with { Status = OrderStatus.Confirmed };

    // Terminal cancellation (slices 4.5 stock-fail / 4.6 payment-decline / 4.7 timeout, all via
    // OrderCancelled). The terminal guard in every handler reads this to no-op a late or duplicate reply.
    public static Order Apply(OrderCancelled e, Order order) => order with { Status = OrderStatus.Cancelled };
}
