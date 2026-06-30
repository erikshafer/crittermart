using JasperFx.Events;
using Marten.Events.Aggregation;

namespace CritterMart.Orders.Shopping;

// The cart-side Bruun todo-list (Workshop 001 § 5 / § 7: CartsAwaitingActivity*): one row per
// open cart, readable at GET /carts/awaiting-activity. The row is created when CartCreated folds,
// its activity timestamp advances as activity events fold, and it is DELETED when either terminal
// event folds — a Marten conditional delete. The abandonment handler does NOT read this view (the
// Cart stream is the single source of truth for the abandonment decision; design.md Decision 2) —
// this is the observable face of the automation, mirroring Order/OrdersAwaitingPayment.
//
// The view stores LastActivityAt (a fact: the most recent activity event's append time); the visible
// Deadline is computed at READ time as LastActivityAt + the configured inactivity window. The policy
// lives on the read side so the projection stays STATELESS — under Marten 9.x an instance-registered
// inline projection is re-materialized and constructor-injected state (a captured TimeSpan) is lost
// (chore/004, the CW/Marten 9.12 upgrade). Mirrors OrdersAwaitingPayment.
public class CartAwaitingActivity
{
    public string Id { get; set; } = string.Empty;          // the cartId (stream key)
    public string CustomerId { get; set; } = string.Empty;
    public DateTimeOffset LastActivityAt { get; set; }      // most recent activity time; Deadline = LastActivityAt + window (on read)
}

// The read-shaped row returned by GET /carts/awaiting-activity: the stored LastActivityAt projected
// forward by the configured inactivity window into the visible Deadline. Distinct from the stored view
// so the projection stays a pure fact-recorder and the window is applied once, on read.
public record CartAwaitingActivityRow(string Id, string CustomerId, DateTimeOffset Deadline);

// Inline single-stream projection (ADR 008) over the same Cart stream that CartView folds — two
// inline projections plus one async projection (CartAbandonmentReport), all over the same events:
// projection shape and lifecycle are per-projection choices, not per-event ones. STATELESS by design
// (see the view remarks) — it records the latest activity timestamp; the endpoint adds the window on read.
public partial class CartsAwaitingActivityProjection : SingleStreamProjection<CartAwaitingActivity, string>
{
    // The IEvent wrapper exposes the event's append timestamp (Marten's using-metadata convention):
    // store it raw; the deadline is computed on read from the configured inactivity window.
    public void Apply(IEvent<CartCreated> e, CartAwaitingActivity view)
    {
        view.CustomerId = e.Data.CustomerId;
        view.LastActivityAt = e.Timestamp;
    }

    // Every activity event pushes the activity timestamp out — the visible mirror of fire-and-check: the
    // pending scheduled message is NOT moved (Wolverine can't); the fired handler re-aims instead.
    public void Apply(IEvent<CartItemAdded> e, CartAwaitingActivity view) =>
        view.LastActivityAt = e.Timestamp;

    public void Apply(IEvent<CartItemRemoved> e, CartAwaitingActivity view) =>
        view.LastActivityAt = e.Timestamp;

    public void Apply(IEvent<CartItemQuantityChanged> e, CartAwaitingActivity view) =>
        view.LastActivityAt = e.Timestamp;

    // Conditional deletes (Marten 9 ShouldDelete method convention): either terminal event —
    // checkout (slice 4.1) or abandonment (slice 3.4) — removes the row from the todo-list.
    public bool ShouldDelete(CartCheckedOut e) => true;

    public bool ShouldDelete(CartAbandoned e) => true;
}
