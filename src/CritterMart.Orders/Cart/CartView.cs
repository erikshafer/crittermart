using JasperFx.Events;
using Marten.Events.Aggregation;

namespace CritterMart.Orders.Cart;

// A single line in the cart — SKU + quantity at the name/price snapshotted when added.
public record CartLine(string Sku, int Quantity, string Name, decimal Price);

// Inline snapshot of a Cart stream — the readable cart, projected from its events.
// Id is the stream key (the generated cartId). CustomerId is carried so the open cart
// can be resolved from a command that knows only the customer (design.md decision 2),
// and a partial unique index on it (registered in Program.cs) enforces one open cart
// per customer. IsOpen flips to false at checkout (4.1, CartCheckedOut) or abandonment
// (3.4, CartAbandoned); the index predicate scopes uniqueness to open carts, so a closed
// cart frees the customer to start a fresh one. LastActivityAt is the append timestamp of
// the newest activity event — the cart's activity clock, which the slice 3.4 fire-and-check
// abandonment decision reads.
public class CartView
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public List<CartLine> Lines { get; set; } = [];
    public DateTimeOffset LastActivityAt { get; set; }
}

// Single-stream projection (Marten 9 partial-class convention). Marten constructs the
// empty view for the genesis event and sets Id to the stream key (the cartId). The
// Apply methods are a pure fold over the stream — unit-tested without a database.
// Activity events arrive as IEvent<T> wrappers (Marten's using-metadata convention) so their
// append timestamps fold into LastActivityAt — the codebase's third IEvent<T> metadata fold.
public partial class CartViewProjection : SingleStreamProjection<CartView, string>
{
    public void Apply(IEvent<CartCreated> e, CartView view)
    {
        view.CustomerId = e.Data.CustomerId;
        view.IsOpen = true;
        view.LastActivityAt = e.Timestamp;
    }

    // Lines are keyed by SKU (slices 3.2/3.3 resolved 3.1's deferred merge question): adding a
    // SKU already in the cart merges quantities into its one line. The first add's snapshotted
    // name/price stays authoritative — consistent with snapshot-wins-until-checkout.
    public void Apply(IEvent<CartItemAdded> e, CartView view)
    {
        view.LastActivityAt = e.Timestamp;

        var index = view.Lines.FindIndex(l => l.Sku == e.Data.Sku);
        if (index < 0)
        {
            view.Lines.Add(new CartLine(e.Data.Sku, e.Data.Quantity, e.Data.Snapshot.Name, e.Data.Snapshot.Price));
        }
        else
        {
            view.Lines[index] = view.Lines[index] with { Quantity = view.Lines[index].Quantity + e.Data.Quantity };
        }
    }

    // Removing a SKU drops its line (slice 3.2). Removing the last line leaves the cart open and
    // empty — a legitimate state; PlaceOrder's CartEmpty guard protects checkout.
    public void Apply(IEvent<CartItemRemoved> e, CartView view)
    {
        view.LastActivityAt = e.Timestamp;
        view.Lines.RemoveAll(l => l.Sku == e.Data.Sku);
    }

    // A quantity change rewrites the line's quantity in place (slice 3.3); the snapshotted
    // name/price are untouched.
    public void Apply(IEvent<CartItemQuantityChanged> e, CartView view)
    {
        view.LastActivityAt = e.Timestamp;

        var index = view.Lines.FindIndex(l => l.Sku == e.Data.Sku);
        if (index >= 0)
        {
            view.Lines[index] = view.Lines[index] with { Quantity = e.Data.Quantity };
        }
    }

    // Checkout closes the cart (slice 4.1). Lines are retained — the checked-out cart stays
    // readable history; only IsOpen flips, which the partial-unique index keys off of. Terminal
    // events are deliberately NOT activity: they end the cart rather than shape it, so they take
    // the plain-event signature (no timestamp fold).
    public void Apply(CartCheckedOut e, CartView view) => view.IsOpen = false;

    // Abandonment closes the cart (slice 3.4) — the stream's second terminal event, same shape
    // as checkout: lines retained as readable history, IsOpen flips, the customer is freed to
    // start a fresh cart.
    public void Apply(CartAbandoned e, CartView view) => view.IsOpen = false;
}
