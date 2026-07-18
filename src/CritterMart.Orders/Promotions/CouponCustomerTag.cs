namespace CritterMart.Orders.Promotions;

// The strong-typed DCB tag for the COMPOSITE (coupon × customer) boundary (Workshop 003 slice 6.5;
// ADR 024 §38). CritterMart's second DCB — one redemption per customer — enforces an EXISTENCE invariant
// ("has this customer already redeemed this coupon?") rather than the first DCB's global COUNT cap.
//
// Verified against the resolved JasperFx.Events v2.27.0.0 (Marten 9.15.1): EventTagQuery has For / Or /
// Or<TEvent,TTag> / AndEventsOfType — but NO method to AND two DIFFERENT tag values (AndEventsOfType filters
// event TYPES, not tags), and ITagTypeRegistration stores a SINGLE scalar per tag. So the composite is not two
// tags AND-ed; it is ONE tag whose scalar value ENCODES the pair — structurally identical to CouponId, and
// queried through the same single-tag path the first DCB proved:
//   opts.Events.RegisterTagType<CouponCustomerTag>("couponcustomer").ForAggregate<CustomerCouponUsage>()
//   new EventTagQuery().Or<CouponCustomerTag>(CouponCustomerTag.For(couponId, customerId))
//
// The composite value "{couponId}|{customerId}" matches no CouponRedeemed property, so Marten's tag inference
// can neither manufacture nor omit it — the tag lands only by explicit IEvent.WithTag, exactly as CouponId does.
public record CouponCustomerTag(string Value)
{
    // The pipe is safe: couponId is a generated GUID string and customerId is a JWT `sub` — neither contains
    // a '|'. One canonical construction site keeps the encoding in one place (redeem builds it from the local
    // customerId; release rebuilds it from Order.CustomerId + Order.CouponId — the same pair, same string).
    public static CouponCustomerTag For(string couponId, string customerId) =>
        new($"{couponId}|{customerId}");
}
