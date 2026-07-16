namespace CritterMart.Orders.Promotions;

// A redemption was returned to the pool because its order was cancelled (Workshop 003 slice 6.4). Appended
// to the SAME order stream as its CouponRedeemed, in the same transaction as OrderCancelled, and TAGGED with
// the SAME CouponId. The compensation twin of CouponRedeemed — the tag-arithmetic mirror of Inventory's
// StockReserved/StockReleased pair. The DCB boundary and CouponUsageView count redemptions MINUS releases,
// so a cancelled order returns its flash-sale slot.
public record CouponRedemptionReleased(string OrderId, string CouponId);
