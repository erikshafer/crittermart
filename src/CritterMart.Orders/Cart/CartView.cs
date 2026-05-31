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

    public void Apply(CartItemAdded e, CartView view) =>
        view.Lines.Add(new CartLine(e.Sku, e.Quantity, e.Snapshot.Name, e.Snapshot.Price));

    // Checkout closes the cart (slice 4.1). Lines are retained — the checked-out cart stays
    // readable history; only IsOpen flips, which the partial-unique index keys off of.
    public void Apply(CartCheckedOut e, CartView view) => view.IsOpen = false;
}
