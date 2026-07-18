namespace CritterMart.Orders.Promotions;

// A coupon was redeemed by an order at checkout (Workshop 003 slice 6.3, the DCB event). Appended to the
// ORDER stream it belongs to (ADR 024's "real order streams" intent), in the same transaction as
// OrderPlaced, and TAGGED with the strong-typed CouponId. The tag is what lets the DCB boundary find every
// redemption of this coupon regardless of which order stream carries it — the consistency boundary aligns
// with the coupon, not with any aggregate. `Discount` is the amount taken off (subtotal × discountPercent/100);
// `CouponCode` is the human-facing code the OrderStatusView surfaces for display ("Discount (FLASH20)").
//
// Slice 6.5: `PerCustomer` records whether this redemption was tagged with the composite (coupon × customer)
// tag too (i.e. the coupon is oneRedemptionPerCustomer). The Order aggregate folds it so the cancellation
// sites know to ALSO carry the composite tag on the compensating release — returning the customer's slot.
// Defaulted false so pre-6.5 CouponRedeemed events (no such JSON property) fold as global-cap-only.
public record CouponRedeemed(
    string OrderId, string CouponId, string CouponCode, decimal Discount, bool PerCustomer = false);
