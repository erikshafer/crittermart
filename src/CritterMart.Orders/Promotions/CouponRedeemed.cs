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
//
// Slice 6.6: `CustomerId` names WHO redeemed. Through 6.5 the (coupon × customer) pair lived only as a
// CouponCustomerTag — enough for the write-time DCB boundary (opened by tag query), but NOT projectable: a
// MultiStreamProjection routes by an event MEMBER (Identity<CouponRedeemed>(e => …)), and a tag is a
// write-side query mechanism, not a grouping key. So CustomerCouponUsageView cannot exist without this field.
// Set UNCONDITIONALLY (not gated on PerCustomer): who redeemed is a plain fact about the event, and recording
// it uniformly avoids a second forward-only cliff if a coupon's per-customer policy is ever flipped on later.
// Defaulted "" and positioned LAST so pre-6.6 events fold unattributed and existing construction sites compile.
public record CouponRedeemed(
    string OrderId, string CouponId, string CouponCode, decimal Discount, bool PerCustomer = false,
    string CustomerId = "");
