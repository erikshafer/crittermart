using System.Text.Json.Serialization;

namespace CritterMart.Orders.Ordering;

// A single line on a placed order — a cart line snapshotted onto the Order stream at checkout.
// The cart's snapshot price is authoritative through the order; there is no re-pricing.
public record OrderLine(string Sku, int Quantity, string Name, decimal Price);

// Genesis event of an Order stream (Workshop 001 slice 4.1). The Customer checked out their
// open cart: the cart's lines and computed pricing are frozen onto a new stream keyed by a
// generated orderId. Paired with CartCheckedOut on the Cart stream in the SAME transaction —
// the project's first multi-stream atomic write. (Workshop 001 § 2, § 4.)
//
// Slice 6.3 (Workshop 003) added the pricing breakdown: `Subtotal` is the pre-discount line total,
// `Discount` is the amount a redeemed coupon took off (zero when none), and `Total = Subtotal − Discount`
// is what reservation / payment / CommitStock act on. The discount lands HERE (Workshop 003 § 3 "where the
// discount lands"); the coupon's identity rides the paired CouponRedeemed. A no-coupon order uses the
// convenience constructor below — Subtotal == Total, Discount == 0 — so slice 4.1's call sites are unchanged.
// Two constructors (canonical 6-arg + no-coupon 4-arg convenience) mean System.Text.Json needs to be told
// which to deserialize with: [JsonConstructor] pins the canonical one. Declared with explicit init properties
// (rather than positional) so the attribute can sit on the constructor.
public record OrderPlaced
{
    public string OrderId { get; init; }
    public string CustomerId { get; init; }
    public IReadOnlyList<OrderLine> Items { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Total { get; init; }

    [JsonConstructor]
    public OrderPlaced(
        string orderId,
        string customerId,
        IReadOnlyList<OrderLine> items,
        decimal subtotal,
        decimal discount,
        decimal total)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        Subtotal = subtotal;
        Discount = discount;
        Total = total;
    }

    // No-coupon convenience: an undiscounted order where Subtotal == Total and Discount == 0. Keeps every
    // slice-4.1 construction site (`new OrderPlaced(orderId, customerId, items, total)`) correct and unchanged.
    public OrderPlaced(string orderId, string customerId, IReadOnlyList<OrderLine> items, decimal total)
        : this(orderId, customerId, items, total, 0m, total) { }
}
