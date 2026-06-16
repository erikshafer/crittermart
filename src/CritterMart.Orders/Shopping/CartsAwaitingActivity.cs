using JasperFx.Events;
using Marten.Events.Aggregation;

namespace CritterMart.Orders.Shopping;

// The cart-side Bruun todo-list (Workshop 001 § 5 / § 7: CartsAwaitingActivity*): one row per
// open cart, readable at GET /carts/awaiting-activity. The row is created when CartCreated folds,
// its deadline advances as activity events fold, and it is DELETED when either terminal event
// folds — a Marten conditional delete. The abandonment handler does NOT read this view (the Cart
// stream is the single source of truth for the abandonment decision; design.md Decision 2) —
// this is the observable face of the automation, mirroring Order/OrdersAwaitingPayment.
public class CartAwaitingActivity
{
    public string Id { get; set; } = string.Empty;          // the cartId (stream key)
    public string CustomerId { get; set; } = string.Empty;
    public DateTimeOffset Deadline { get; set; }            // when the cart becomes abandonable
}

// Inline single-stream projection (ADR 008) over the same Cart stream that CartView folds — two
// inline projections plus one async projection (CartAbandonmentReport), all over the same events:
// projection shape and lifecycle are per-projection choices, not per-event ones. The configured
// window is constructor-injected (instance registration in Program.cs) so the row's visible
// deadline matches what the abandonment handler will actually decide.
public partial class CartsAwaitingActivityProjection : SingleStreamProjection<CartAwaitingActivity, string>
{
    private readonly TimeSpan _activityWindow;

    public CartsAwaitingActivityProjection(TimeSpan activityWindow) => _activityWindow = activityWindow;

    // The IEvent wrapper exposes the event's append timestamp (Marten's using-metadata convention):
    // the deadline is the activity time plus the configured window.
    public void Apply(IEvent<CartCreated> e, CartAwaitingActivity view)
    {
        view.CustomerId = e.Data.CustomerId;
        view.Deadline = e.Timestamp.Add(_activityWindow);
    }

    // Every activity event pushes the deadline out — the visible mirror of fire-and-check: the
    // pending scheduled message is NOT moved (Wolverine can't); the fired handler re-aims instead.
    public void Apply(IEvent<CartItemAdded> e, CartAwaitingActivity view) =>
        view.Deadline = e.Timestamp.Add(_activityWindow);

    public void Apply(IEvent<CartItemRemoved> e, CartAwaitingActivity view) =>
        view.Deadline = e.Timestamp.Add(_activityWindow);

    public void Apply(IEvent<CartItemQuantityChanged> e, CartAwaitingActivity view) =>
        view.Deadline = e.Timestamp.Add(_activityWindow);

    // Conditional deletes (Marten 9 ShouldDelete method convention): either terminal event —
    // checkout (slice 4.1) or abandonment (slice 3.4) — removes the row from the todo-list.
    public bool ShouldDelete(CartCheckedOut e) => true;

    public bool ShouldDelete(CartAbandoned e) => true;
}
