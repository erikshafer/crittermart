using JasperFx.Events;
using Marten.Events.Aggregation;

namespace CritterMart.Orders.Ordering;

// The Bruun todo-list (Workshop 001 § 5 / § 7: OrdersAwaitingPayment*): one row per order that has
// not yet reached a terminal state, readable at GET /orders/awaiting-payment. The row is created
// when OrderPlaced folds and DELETED when any terminal event folds — a Marten conditional delete.
// The timeout handler does NOT read this view (the Order stream is the single source of truth for
// the cancellation decision; design.md Decision 3) — this is the observable face of the automation.
//
// The view stores PlacedAt (a fact: the OrderPlaced append time); the visible Deadline is computed at
// READ time in the endpoint as PlacedAt + the configured payment timeout. The deadline policy lives on
// the read side, NOT in the projection, on purpose: under Marten 9.x an inline projection registered as
// an instance is re-materialized by the runtime, so constructor-injected state (a captured TimeSpan) is
// lost and reads back as default(TimeSpan). Keeping the projection STATELESS sidesteps that — the
// timeout is config/DI, which the endpoint has and the projection does not. (Surfaced by the CW/Marten
// 9.12 upgrade, chore/004: the prior ctor-injected deadline silently became "now".)
public class OrderAwaitingPayment
{
    public string Id { get; set; } = string.Empty;          // the orderId (stream key)
    public string CustomerId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTimeOffset PlacedAt { get; set; }            // OrderPlaced append time; Deadline = PlacedAt + timeout (on read)
}

// The read-shaped row returned by GET /orders/awaiting-payment: the stored PlacedAt projected forward by
// the configured payment timeout into the visible Deadline. Distinct from the stored view so the
// projection stays a pure fact-recorder and the policy (the timeout) is applied once, on read.
public record OrderAwaitingPaymentRow(string Id, string CustomerId, decimal Total, DateTimeOffset Deadline);

// Inline single-stream projection (ADR 008) over the same Order stream that OrderStatusView folds —
// two projections, one stream: projection shape is a per-projection choice, and this one's is
// "a row exists only while the order is non-terminal". STATELESS by design (see the view remarks) —
// it records the placement timestamp; the endpoint adds the configured timeout on read.
public partial class OrdersAwaitingPaymentProjection : SingleStreamProjection<OrderAwaitingPayment, string>
{
    // The IEvent wrapper exposes the event's append timestamp (Marten's using-metadata convention):
    // store it raw; the deadline is computed on read from the configured timeout.
    public void Apply(IEvent<OrderPlaced> e, OrderAwaitingPayment view)
    {
        view.CustomerId = e.Data.CustomerId;
        view.Total = e.Data.Total;
        view.PlacedAt = e.Timestamp;
    }

    // Conditional deletes (Marten 9 ShouldDelete method convention): any terminal event removes the
    // row — confirmed orders and orders cancelled by ANY path (stock failure 4.5, payment decline
    // 4.6, or this slice's own timeout) all leave the todo-list.
    public bool ShouldDelete(OrderConfirmed e) => true;

    public bool ShouldDelete(OrderCancelled e) => true;
}
