using CritterMart.Orders.Promotions;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the CustomerCouponUsageView fold (slice 6.6, the advisory per-customer
// preview) — no database, no host, no container. The multi-stream ROUTING (which document an event lands
// in, keyed by "{couponId}|{customerId}") is configured declaratively via Identity<T> and proven end-to-end
// by the 6.6 integration tests in CouponTests; these cover the arithmetic and the key encoding itself.
public class CustomerCouponUsageViewProjectionTests
{
    private readonly CustomerCouponUsageViewProjection _projection = new();

    private static CouponRedeemed Redeemed(string couponId, string customerId) =>
        new("order-1", couponId, "FIRSTORDER", 3.00m, PerCustomer: true, CustomerId: customerId);

    private static CouponRedemptionReleased Released(string couponId, string customerId) =>
        new("order-1", couponId, customerId);

    // The reserve/release symmetry, per pair: a cancelled redemption returns the customer's slot, so the
    // preview goes back to `valid`. Same arithmetic as the CustomerCouponUsage DCB boundary — the two fold
    // the same two events and differ only in WHEN, not in WHAT.
    [Fact]
    public void a_redemption_and_its_release_net_to_zero()
    {
        var view = new CustomerCouponUsageView();

        _projection.Apply(Redeemed("coupon-1", "customer-X"), view);
        view.NetCount.ShouldBe(1);

        _projection.Apply(Released("coupon-1", "customer-X"), view);
        view.NetCount.ShouldBe(0);
    }

    // Each (coupon, customer) pair is an independent document — the composite key is what separates them,
    // mirroring the independence the composite DCB boundary already has on the write side.
    [Fact]
    public void each_coupon_customer_pair_keys_a_distinct_document()
    {
        CustomerCouponUsageView.KeyFor("coupon-1", "customer-X")
            .ShouldBe("coupon-1|customer-X");

        CustomerCouponUsageView.KeyFor("coupon-1", "customer-X")
            .ShouldNotBe(CustomerCouponUsageView.KeyFor("coupon-1", "customer-Y"));

        // Two customers' folds are independent: one customer's redemption cannot move the other's count.
        var x = new CustomerCouponUsageView { Id = CustomerCouponUsageView.KeyFor("coupon-1", "customer-X") };
        var y = new CustomerCouponUsageView { Id = CustomerCouponUsageView.KeyFor("coupon-1", "customer-Y") };

        _projection.Apply(Redeemed("coupon-1", "customer-X"), x);
        _projection.Apply(Redeemed("coupon-1", "customer-Y"), y);

        x.NetCount.ShouldBe(1);
        y.NetCount.ShouldBe(1);
    }

    // The forward-only consequence, made explicit (Workshop 003 §6.6, design.md decision 7). A pre-6.6
    // event carries no CustomerId, so it folds into an unattributed "{couponId}|" bucket that no query ever
    // constructs (an authenticated caller always has a non-empty `sub`). The preview therefore UNDER-warns —
    // degrading to exactly today's behavior — and can never wrongly accuse an entitled customer.
    [Fact]
    public void an_unattributed_redemption_keys_a_bucket_no_query_constructs()
    {
        // The defaulted CustomerId is how an old serialized event without the property deserializes.
        var legacy = new CouponRedeemed("order-0", "coupon-1", "FIRSTORDER", 3.00m, PerCustomer: true);
        legacy.CustomerId.ShouldBe("");

        CustomerCouponUsageView.KeyFor(legacy.CouponId, legacy.CustomerId)
            .ShouldBe("coupon-1|");

        // It is emphatically NOT any real customer's key, so it can never surface as `already_redeemed`.
        CustomerCouponUsageView.KeyFor(legacy.CouponId, legacy.CustomerId)
            .ShouldNotBe(CustomerCouponUsageView.KeyFor("coupon-1", "customer-X"));
    }
}
