namespace CritterMart.Orders.Ordering;

// A single line on a placed order — a cart line snapshotted onto the Order stream at checkout.
// The cart's snapshot price is authoritative through the order; there is no re-pricing.
public record OrderLine(string Sku, int Quantity, string Name, decimal Price);

// Genesis event of an Order stream (Workshop 001 slice 4.1). The Customer checked out their
// open cart: the cart's lines and computed total are frozen onto a new stream keyed by a
// generated orderId. Paired with CartCheckedOut on the Cart stream in the SAME transaction —
// the project's first multi-stream atomic write. (Workshop 001 § 2, § 4.)
public record OrderPlaced(string OrderId, string CustomerId, IReadOnlyList<OrderLine> Items, decimal Total);
