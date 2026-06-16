using JasperFx.Events;

namespace CritterMart.Orders.Cart;

// The Cart aggregate — the domain WRITE model (ADR 020). `sealed` and immutable: every Apply returns a
// NEW Cart via `with`, never mutates in place. It is the FetchForWriting / StartStream target and the
// home of the open-cart invariant (one open cart per customer, enforced by the partial-unique index in
// Program.cs). It is owned by the domain and is NEVER serialized over HTTP — the storefront binds the
// separate CartView read model (CartView.cs), never this type.
//
// Self-aggregating inline snapshot (ADR 008, refined by ADR 020): the static Create/Apply methods ARE the
// projection, registered via `Projections.Snapshot<Cart>(SnapshotLifecycle.Inline)`, so the aggregate is
// materialized in the same transaction as the event append. The fold is a pure function of the stream —
// unit-tested without a database (CartProjectionTests). Id comes from the genesis event's CartId, which is
// the stream key (AddToCart starts the stream as `StartStream<Cart>(cartId, new CartCreated(cartId, …))`).
public sealed record Cart(
    string Id,
    string CustomerId,
    bool IsOpen,
    IReadOnlyList<CartLine> Lines,
    DateTimeOffset LastActivityAt)
{
    // Genesis: the customer's first add starts the stream (AddToCart) when they have no open cart.
    public static Cart Create(IEvent<CartCreated> e) =>
        new(e.Data.CartId, e.Data.CustomerId, IsOpen: true, Lines: [], LastActivityAt: e.Timestamp);

    // Activity events advance LastActivityAt to their append timestamp — the cart's activity clock the
    // slice-3.4 abandonment automation reads. Lines are SKU-keyed; CartLines owns the merge semantics.
    public static Cart Apply(IEvent<CartItemAdded> e, Cart cart) =>
        cart with { Lines = CartLines.Add(cart.Lines, e.Data), LastActivityAt = e.Timestamp };

    public static Cart Apply(IEvent<CartItemRemoved> e, Cart cart) =>
        cart with { Lines = CartLines.Remove(cart.Lines, e.Data.Sku), LastActivityAt = e.Timestamp };

    public static Cart Apply(IEvent<CartItemQuantityChanged> e, Cart cart) =>
        cart with { Lines = CartLines.ChangeQuantity(cart.Lines, e.Data.Sku, e.Data.Quantity), LastActivityAt = e.Timestamp };

    // Terminal events close the cart (checkout 4.1 / abandonment 3.4): IsOpen flips, freeing the
    // partial-unique open-cart index so the customer can start a fresh cart. Lines are retained as
    // history. Terminal events are NOT activity — they end the cart rather than shape it (no timestamp fold).
    public static Cart Apply(CartCheckedOut e, Cart cart) => cart with { IsOpen = false };

    public static Cart Apply(CartAbandoned e, Cart cart) => cart with { IsOpen = false };
}
