using Marten.Events.Aggregation;

namespace CritterMart.Orders.Cart;

// A single line in the cart — SKU + quantity at the name/price snapshotted when added.
public record CartLine(string Sku, int Quantity, string Name, decimal Price);

// Inline snapshot of a Cart stream — the readable cart, projected from its events.
// Id is the stream key (the generated cartId). CustomerId is carried so the open cart
// can be resolved from a command that knows only the customer (design.md decision 2),
// and a partial unique index on it (registered in Program.cs) enforces one open cart
// per customer. IsOpen flips to false at checkout (4.1, CartCheckedOut); abandon (3.4)
// will also close it. The index predicate scopes uniqueness to open carts, so a closed
// cart frees the customer to start a fresh one.
public class CartView
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public List<CartLine> Lines { get; set; } = [];
}

// Single-stream projection (Marten 9 partial-class convention). Marten constructs the
// empty view for the genesis event and sets Id to the stream key (the cartId). The
// Apply methods are a pure fold over the stream — unit-tested without a database.
public partial class CartViewProjection : SingleStreamProjection<CartView, string>
{
    public void Apply(CartCreated e, CartView view)
    {
        view.CustomerId = e.CustomerId;
        view.IsOpen = true;
    }

    // Lines are keyed by SKU (slices 3.2/3.3 resolved 3.1's deferred merge question): adding a
    // SKU already in the cart merges quantities into its one line. The first add's snapshotted
    // name/price stays authoritative — consistent with snapshot-wins-until-checkout.
    public void Apply(CartItemAdded e, CartView view)
    {
        var index = view.Lines.FindIndex(l => l.Sku == e.Sku);
        if (index < 0)
        {
            view.Lines.Add(new CartLine(e.Sku, e.Quantity, e.Snapshot.Name, e.Snapshot.Price));
        }
        else
        {
            view.Lines[index] = view.Lines[index] with { Quantity = view.Lines[index].Quantity + e.Quantity };
        }
    }

    // Removing a SKU drops its line (slice 3.2). Removing the last line leaves the cart open and
    // empty — a legitimate state; PlaceOrder's CartEmpty guard protects checkout.
    public void Apply(CartItemRemoved e, CartView view) =>
        view.Lines.RemoveAll(l => l.Sku == e.Sku);

    // A quantity change rewrites the line's quantity in place (slice 3.3); the snapshotted
    // name/price are untouched.
    public void Apply(CartItemQuantityChanged e, CartView view)
    {
        var index = view.Lines.FindIndex(l => l.Sku == e.Sku);
        if (index >= 0)
        {
            view.Lines[index] = view.Lines[index] with { Quantity = e.Quantity };
        }
    }

    // Checkout closes the cart (slice 4.1). Lines are retained — the checked-out cart stays
    // readable history; only IsOpen flips, which the partial-unique index keys off of.
    public void Apply(CartCheckedOut e, CartView view) => view.IsOpen = false;
}
