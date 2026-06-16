using JasperFx.Events;

namespace CritterMart.Orders.Shopping;

// CartView — the cart's READ model (ADR 020): the public projection the storefront binds via
// GET /carts/mine (and GET /carts/{cartId}). A DEDICATED inline projection from the cart's events,
// decoupled from the Cart aggregate — the read path never touches the protected write model. Its shape
// currently mirrors the aggregate (this cart's public view ≈ its state), but it is free to diverge — add a
// computed total, drop write-only fields — without touching Cart. CustomerId + IsOpen are carried so
// `/carts/mine` resolves the customer's one open cart by identity; the wire shape is preserved exactly so
// the W2 frontend (CartViewSchema, PR #58) is unchanged.
//
// Self-aggregating inline snapshot, registered `Projections.Snapshot<CartView>(SnapshotLifecycle.Inline)`.
// The fold mirrors the aggregate's via the shared CartLines helper, so read and write stay consistent.
public sealed record CartView(
    string Id,
    string CustomerId,
    bool IsOpen,
    IReadOnlyList<CartLine> Lines,
    DateTimeOffset LastActivityAt)
{
    public static CartView Create(IEvent<CartCreated> e) =>
        new(e.Data.CartId, e.Data.CustomerId, IsOpen: true, Lines: [], LastActivityAt: e.Timestamp);

    public static CartView Apply(IEvent<CartItemAdded> e, CartView view) =>
        view with { Lines = CartLines.Add(view.Lines, e.Data), LastActivityAt = e.Timestamp };

    public static CartView Apply(IEvent<CartItemRemoved> e, CartView view) =>
        view with { Lines = CartLines.Remove(view.Lines, e.Data.Sku), LastActivityAt = e.Timestamp };

    public static CartView Apply(IEvent<CartItemQuantityChanged> e, CartView view) =>
        view with { Lines = CartLines.ChangeQuantity(view.Lines, e.Data.Sku, e.Data.Quantity), LastActivityAt = e.Timestamp };

    public static CartView Apply(CartCheckedOut e, CartView view) => view with { IsOpen = false };

    public static CartView Apply(CartAbandoned e, CartView view) => view with { IsOpen = false };
}
