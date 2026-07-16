namespace CritterMart.Orders.Promotions;

// A coupon was redeemed by an order at checkout (Workshop 003 slice 6.3, the DCB event). Appended to the
// ORDER stream it belongs to (ADR 024's "real order streams" intent), in the same transaction as
// OrderPlaced, and TAGGED with the strong-typed CouponId. The tag is what lets the DCB boundary find every
// redemption of this coupon regardless of which order stream carries it — the consistency boundary aligns
// with the coupon, not with any aggregate. `Discount` is the amount taken off (subtotal × discountPercent/100);
// `CouponCode` is the human-facing code the OrderStatusView surfaces for display ("Discount (FLASH20)").
public record CouponRedeemed(string OrderId, string CouponId, string CouponCode, decimal Discount);
