using JasperFx.Events.Aggregation;

namespace CritterMart.Orders.Promotions;

// CustomerCouponUsage — the DCB BOUNDARY STATE for the composite (coupon × customer) boundary (Workshop 003
// slice 6.5; ADR 024 §38). The second boundary aggregate, mirroring CouponUsage but scoped to ONE pair: the
// write-side decision state FetchForWritingByTags<CustomerCouponUsage> materializes ON DEMAND from every event
// tagged with a specific CouponCustomerTag (this coupon, this customer), and is thrown away — NEVER persisted,
// NEVER queried by the UI.
//
// Where CouponUsage answers a COUNT question ("how many redemptions of this coupon, vs the cap?"),
// CustomerCouponUsage answers an EXISTENCE question ("has this customer redeemed this coupon at all?"): the
// checkout check is `NetCount >= 1`. Because the composite tag pre-scopes the read to exactly one pair, the
// aggregate is a trivial counter — no event-data comparison, unlike the skill's Polecat course×student example
// (which gathers via a multi-tag OR and disambiguates in the aggregate). NetCount is 0 or 1 in steady state
// (a release returns it to 0); it can transiently read the same 0 in two racing sessions, which the DCB
// optimistic-concurrency assertion + PlaceOrder's reload-and-retry loop resolve to exactly one survivor.
//
// Id-less [BoundaryAggregate] (the textbook DCB shape — the boundary aligns with the PAIR, not a stream), the
// mutable-class + void-Apply convention Marten DCB boundary aggregates use (JasperFx.Events.Aggregation) — the
// same deliberate divergence from CritterMart's immutable-record read models CouponUsage documents. This second
// id-less boundary aggregate inherits the Marten 9.15.1 DeleteAllDocumentsAsync rough edge, handled by the
// SQL-truncate test reset (OrdersAppFixture.ResetAllDataAsync, generalized to mt_event_tag_%).
[BoundaryAggregate]
public class CustomerCouponUsage
{
    // Net redemptions of THIS coupon by THIS customer. Redemptions raise it, releases lower it; the checkout
    // per-customer check is `NetCount >= 1` (already redeemed → reject).
    public int NetCount { get; set; }

    public void Apply(CouponRedeemed _) => NetCount++;

    public void Apply(CouponRedemptionReleased _) => NetCount--;
}
