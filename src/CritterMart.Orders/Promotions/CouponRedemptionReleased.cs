namespace CritterMart.Orders.Promotions;

// A redemption was returned to the pool because its order was cancelled (Workshop 003 slice 6.4). Appended
// to the SAME order stream as its CouponRedeemed, in the same transaction as OrderCancelled, and TAGGED with
// the SAME CouponId. The compensation twin of CouponRedeemed — the tag-arithmetic mirror of Inventory's
// StockReserved/StockReleased pair. The DCB boundary and CouponUsageView count redemptions MINUS releases,
// so a cancelled order returns its flash-sale slot.
//
// Slice 6.6: `CustomerId` mirrors the twin's new member, so CustomerCouponUsageView can decrement the pair it
// incremented (see CouponRedeemed for why a tag cannot serve as a projection grouping key). Defaulted "" and
// last, the same non-breaking evolution — pre-6.6 releases fold unattributed.
public record CouponRedemptionReleased(string OrderId, string CouponId, string CustomerId = "");
