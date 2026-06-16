namespace CritterMart.Orders.Shopping;

// A single cart line — a SKU at its quantity, with the name + price snapshotted when the SKU was first
// added (so the cart needs no Catalog read). A shared domain value object: the Cart aggregate and the
// CartView read model both fold lines, and CartAbandoned snapshots them. Immutable; a quantity change
// produces a new line via `with`.
public sealed record CartLine(string Sku, int Quantity, string Name, decimal Price);

// The SKU-keyed line-fold semantics, shared by the Cart aggregate (the write model) and the CartView read
// projection so the two never drift: adding a SKU already present merges into its one line, and the first
// add's snapshot name/price stay authoritative — only quantity accumulates. Pure functions over an
// immutable list; both projections call these from their `with` expressions.
internal static class CartLines
{
    public static IReadOnlyList<CartLine> Add(IReadOnlyList<CartLine> lines, CartItemAdded added)
    {
        if (lines.All(l => l.Sku != added.Sku))
        {
            return [.. lines, new CartLine(added.Sku, added.Quantity, added.Snapshot.Name, added.Snapshot.Price)];
        }

        return lines
            .Select(l => l.Sku == added.Sku ? l with { Quantity = l.Quantity + added.Quantity } : l)
            .ToList();
    }

    public static IReadOnlyList<CartLine> Remove(IReadOnlyList<CartLine> lines, string sku) =>
        lines.Where(l => l.Sku != sku).ToList();

    public static IReadOnlyList<CartLine> ChangeQuantity(IReadOnlyList<CartLine> lines, string sku, int quantity) =>
        lines.Select(l => l.Sku == sku ? l with { Quantity = quantity } : l).ToList();
}
