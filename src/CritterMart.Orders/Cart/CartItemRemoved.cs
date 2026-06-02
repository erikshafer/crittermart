namespace CritterMart.Orders.Cart;

// An item was removed from the cart (Workshop 001 § 4, slice 3.2). SKU-scoped: cart lines are
// keyed by SKU (one line per SKU since slices 3.2/3.3 resolved the merge-by-SKU question), so
// removing a SKU removes its single line from CartView. The stream keeps every add that line
// accumulated — removal is a new fact, not an erasure.
public record CartItemRemoved(string Sku);
