using JasperFx.Events;

namespace CritterMart.Orders.Cart;

// The ShoppingCart aggregate — the domain WRITE model (ADR 020). `sealed` and immutable: every Apply
// returns a NEW ShoppingCart via `with`, never mutates in place. It is the FetchForWriting / StartStream
// target and the home of the open-cart invariant (one open cart per customer, enforced by the partial-
// unique index in Program.cs). It is owned by the domain and is NEVER serialized over HTTP — the storefront
// binds the separate CartView read model (CartView.cs), never this type.
//
// Named `ShoppingCart` (the natural ecommerce term, echoing the `shopping-cart` OpenSpec capability) rather
// than `Cart`: a `Cart` type would collide with its own namespace `CritterMart.Orders.Cart` (CS0118) in
// cross-namespace callers. The events/commands/read-model stay `Cart*` (the ubiquitous shorthand); the
// aggregate alone carries the full compound. The Order area takes the other tack — a verb folder
// `Ordering/` keeps its aggregate the canonical `Order` (ADR 020, retro 002).
//
// Self-aggregating inline snapshot (ADR 008, refined by ADR 020): the static Create/Apply methods ARE the
// projection, registered via `Projections.Snapshot<ShoppingCart>(SnapshotLifecycle.Inline)`, so the aggregate
// is materialized in the same transaction as the event append. The fold is a pure function of the stream —
// unit-tested without a database (CartProjectionTests). Id comes from the genesis event's CartId, which is the
// stream key (AddToCart starts the stream as `StartStream<ShoppingCart>(cartId, new CartCreated(cartId, …))`).
public sealed record ShoppingCart(
    string Id,
    string CustomerId,
    bool IsOpen,
    IReadOnlyList<CartLine> Lines,
    DateTimeOffset LastActivityAt)
{
    // Genesis: the customer's first add starts the stream (AddToCart) when they have no open cart.
    public static ShoppingCart Create(IEvent<CartCreated> e) =>
        new(e.Data.CartId, e.Data.CustomerId, IsOpen: true, Lines: [], LastActivityAt: e.Timestamp);

    // Activity events advance LastActivityAt to their append timestamp — the cart's activity clock the
    // slice-3.4 abandonment automation reads. Lines are SKU-keyed; CartLines owns the merge semantics.
    public static ShoppingCart Apply(IEvent<CartItemAdded> e, ShoppingCart cart) =>
        cart with { Lines = CartLines.Add(cart.Lines, e.Data), LastActivityAt = e.Timestamp };

    public static ShoppingCart Apply(IEvent<CartItemRemoved> e, ShoppingCart cart) =>
        cart with { Lines = CartLines.Remove(cart.Lines, e.Data.Sku), LastActivityAt = e.Timestamp };

    public static ShoppingCart Apply(IEvent<CartItemQuantityChanged> e, ShoppingCart cart) =>
        cart with { Lines = CartLines.ChangeQuantity(cart.Lines, e.Data.Sku, e.Data.Quantity), LastActivityAt = e.Timestamp };

    // Terminal events close the cart (checkout 4.1 / abandonment 3.4): IsOpen flips, freeing the
    // partial-unique open-cart index so the customer can start a fresh cart. Lines are retained as
    // history. Terminal events are NOT activity — they end the cart rather than shape it (no timestamp fold).
    public static ShoppingCart Apply(CartCheckedOut e, ShoppingCart cart) => cart with { IsOpen = false };

    public static ShoppingCart Apply(CartAbandoned e, ShoppingCart cart) => cart with { IsOpen = false };
}
