namespace CritterMart.Orders.Cart;

// A line added to the cart — the Cart stream's second event kind (after CartCreated).
// Carries the snapshotted name + price so the cart needs no Catalog read. Each add is
// its own line in slice 3.1 (quantity-merge by SKU is a 3.3 concern). (Workshop 001 § 4.)
public record CartItemAdded(string Sku, int Quantity, ProductSnapshot Snapshot);
