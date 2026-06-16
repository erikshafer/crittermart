namespace CritterMart.Orders.Shopping;

// A line added to the cart — the Cart stream's second event kind (after CartCreated).
// Carries the snapshotted name + price so the cart needs no Catalog read. Cart lines are
// keyed by SKU: adding a SKU already in the cart merges quantities into its existing line
// (slice 3.3 resolved 3.1's deferred merge-by-SKU question). (Workshop 001 § 4.)
public record CartItemAdded(string Sku, int Quantity, ProductSnapshot Snapshot);
