using JasperFx.Events;
using Marten.Events.Aggregation;

namespace CritterMart.Orders.Order;

// The Bruun todo-list (Workshop 001 § 5 / § 7: OrdersAwaitingPayment*): one row per order that has
// not yet reached a terminal state, readable at GET /orders/awaiting-payment. The row is created
// when OrderPlaced folds and DELETED when any terminal event folds — a Marten conditional delete.
// The timeout handler does NOT read this view (the Order stream is the single source of truth for
// the cancellation decision; design.md Decision 3) — this is the observable face of the automation.
public class OrderAwaitingPayment
{
    public string Id { get; set; } = string.Empty;          // the orderId (stream key)
    public string CustomerId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTimeOffset Deadline { get; set; }            // when OrderPaymentTimeout will fire
}

// Inline single-stream projection (ADR 008) over the same Order stream that OrderStatusView folds —
// two projections, one stream: projection shape is a per-projection choice, and this one's is
// "a row exists only while the order is non-terminal". The configured PaymentDeadline duration is
// constructor-injected (instance registration in Program.cs) so the row's visible deadline matches
// the schedule PlaceOrder actually set.
public partial class OrdersAwaitingPaymentProjection : SingleStreamProjection<OrderAwaitingPayment, string>
{
    private readonly TimeSpan _paymentTimeout;

    public OrdersAwaitingPaymentProjection(TimeSpan paymentTimeout) => _paymentTimeout = paymentTimeout;

    // The IEvent wrapper exposes the event's append timestamp (Marten's using-metadata convention):
    // the deadline is placement time plus the configured timeout.
    public void Apply(IEvent<OrderPlaced> e, OrderAwaitingPayment view)
    {
        view.CustomerId = e.Data.CustomerId;
        view.Total = e.Data.Total;
        view.Deadline = e.Timestamp.Add(_paymentTimeout);
    }

    // Conditional deletes (Marten 9 ShouldDelete method convention): any terminal event removes the
    // row — confirmed orders and orders cancelled by ANY path (stock failure 4.5, payment decline
    // 4.6, or this slice's own timeout) all leave the todo-list.
    public bool ShouldDelete(OrderConfirmed e) => true;

    public bool ShouldDelete(OrderCancelled e) => true;
}
