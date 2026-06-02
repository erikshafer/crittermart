namespace CritterMart.Orders.Cart;

// The quantity of an existing cart line changed (Workshop 001 § 4, slice 3.3). Carries the new
// absolute quantity, not a delta — the Customer says "I want 3", not "+2". Zero is not a valid
// quantity; removing an item is its own command (RemoveCartItem) and its own event.
public record CartItemQuantityChanged(string Sku, int Quantity);
