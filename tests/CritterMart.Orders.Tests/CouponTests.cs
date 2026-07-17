using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Promotions;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Tests;

// Workshop 003 slices 6.1 (define) / 6.3 (redeem-with-DCB) / 6.4 (release-on-cancel). Integration tests
// over the real Marten DCB path against the throwaway Postgres container (ADR 024). The concurrency test is
// the invariant's whole point: many checkouts race a capped coupon and exactly `cap` survive.
[Collection("orders")]
[Trait("Category", "Integration")]
public class CouponTests
{
    private static readonly ProductSnapshot Plush = new("Cosmic Critter Plush", 20.00m);

    private readonly OrdersAppFixture _fixture;

    public CouponTests(OrdersAppFixture fixture) => _fixture = fixture;

    // Boundary-aggregate-safe reset (see OrdersAppFixture.ResetAllDataAsync): TRUNCATEs the tables directly
    // rather than via Marten's Clean, which trips over the id-less DCB boundary aggregate once it is active.
    private Task ResetOrdersAsync() => _fixture.ResetAllDataAsync();

    private IDocumentStore Store => _fixture.Host.Services.GetRequiredService<IDocumentStore>();

    // POST /coupons and return the generated couponId (resolved from CouponView by code).
    private async Task DefineCouponAsync(string code, int discountPercent, int cap, int expectedStatus = 201)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new DefineCoupon(code, discountPercent, cap)).ToUrl("/coupons");
            _.StatusCodeShouldBe(expectedStatus);
        });
    }

    private async Task<string> CouponIdAsync(string code)
    {
        await using var session = Store.LightweightSession();
        var view = await session.Query<CouponView>().FirstAsync(c => c.Code == code);
        return view.Id;
    }

    private async Task AddOneToCartAsync(string customerId)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, Plush)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });
    }

    // Place an order (optionally with a coupon), returning (status, orderId?) without asserting the status.
    private async Task<(int Status, string? OrderId)> PlaceAsync(string customerId, string? couponCode = null)
    {
        var url = couponCode is null ? "/orders" : $"/orders?couponCode={couponCode}";
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url(url);
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.IgnoreStatusCode();
        });

        var status = result.Context.Response.StatusCode;
        var orderId = status == 201 ? result.ReadAsJson<PlaceOrderResponse>()?.OrderId : null;
        return (status, orderId);
    }

    private async Task<int> UsageNetCountAsync(string couponId)
    {
        await using var session = Store.LightweightSession();
        var view = await session.LoadAsync<CouponUsageView>(couponId);
        return view?.NetCount ?? 0;
    }

    // GET /coupons/{code}/validate — the advisory read (slice 6.2). Always 200; the answer is discriminated
    // by Status (valid/invalid/exhausted).
    private async Task<CouponValidation> ValidateCouponAsync(string code)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/coupons/{code}/validate");
            _.StatusCodeShouldBe(200);
        });
        return result.ReadAsJson<CouponValidation>()!;
    }

    // ── 6.1 Define a coupon ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task defining_a_coupon_creates_the_definition_and_view()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FLASH20", 20, 3);

        await using var session = Store.LightweightSession();
        var view = await session.Query<CouponView>().FirstOrDefaultAsync(c => c.Code == "FLASH20");
        view.ShouldNotBeNull();
        view.DiscountPercent.ShouldBe(20);
        view.Cap.ShouldBe(3);
    }

    [Fact]
    public async Task defining_a_duplicate_code_is_rejected()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FLASH20", 20, 3);
        await DefineCouponAsync("FLASH20", 15, 5, expectedStatus: 409);

        await using var session = Store.LightweightSession();
        var count = await session.Query<CouponView>().CountAsync(c => c.Code == "FLASH20");
        count.ShouldBe(1);
    }

    [Theory]
    [InlineData(20, 0)]    // cap < 1
    [InlineData(0, 3)]     // discount <= 0
    [InlineData(150, 3)]   // discount > 100
    public async Task defining_a_nonsensical_coupon_is_rejected(int discountPercent, int cap)
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("BADCOUPON", discountPercent, cap, expectedStatus: 400);

        await using var session = Store.LightweightSession();
        (await session.Query<CouponView>().AnyAsync(c => c.Code == "BADCOUPON")).ShouldBeFalse();
    }

    // ── 6.2 Validate & price a coupon at cart review (advisory, read-only) ───────────────────────────

    [Fact]
    public async Task validating_a_redeemable_coupon_reports_valid_with_the_discount_and_writes_nothing()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FLASH20", 20, 3);
        var couponId = await CouponIdAsync("FLASH20");

        var result = await ValidateCouponAsync("FLASH20");
        result.Code.ShouldBe("FLASH20");
        result.Status.ShouldBe(CouponValidationStatus.Valid);
        result.DiscountPercent.ShouldBe(20);

        // Advisory read — it must not have moved the usage count (nothing redeemed).
        (await UsageNetCountAsync(couponId)).ShouldBe(0);
    }

    [Fact]
    public async Task validating_an_unknown_code_reports_invalid()
    {
        await ResetOrdersAsync();

        var result = await ValidateCouponAsync("BOGUS");
        result.Code.ShouldBe("BOGUS");
        result.Status.ShouldBe(CouponValidationStatus.Invalid);
        result.DiscountPercent.ShouldBeNull();
    }

    [Fact]
    public async Task validating_a_coupon_at_its_cap_reports_exhausted()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("SOLO", 20, 1);   // cap 1: one redemption exhausts it

        await AddOneToCartAsync("customer-A");
        var (status, _) = await PlaceAsync("customer-A", "SOLO");
        status.ShouldBe(201);   // the one slot is now taken

        var result = await ValidateCouponAsync("SOLO");
        result.Code.ShouldBe("SOLO");
        result.Status.ShouldBe(CouponValidationStatus.Exhausted);
        result.DiscountPercent.ShouldBeNull();
    }

    // ── 6.3 Redeem a coupon at checkout ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task redeeming_a_coupon_applies_the_discount_and_records_usage()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FLASH20", 20, 3);
        var couponId = await CouponIdAsync("FLASH20");

        await AddOneToCartAsync("customer-X");
        var (status, orderId) = await PlaceAsync("customer-X", "FLASH20");
        status.ShouldBe(201);

        await using var session = Store.LightweightSession();
        var order = await session.LoadAsync<OrderStatusView>(orderId!);
        order.ShouldNotBeNull();
        order.Subtotal.ShouldBe(20.00m);
        order.Discount.ShouldBe(4.00m);   // 20% of 20.00
        order.Total.ShouldBe(16.00m);
        order.CouponCode.ShouldBe("FLASH20");

        // The tagged CouponRedeemed rode the same order stream (mechanic a).
        var events = await session.Events.FetchStreamAsync(orderId!);
        events.ShouldContain(e => e.Data is CouponRedeemed);

        (await UsageNetCountAsync(couponId)).ShouldBe(1);
    }

    [Fact]
    public async Task redeeming_an_unknown_code_is_rejected_and_creates_no_order()
    {
        await ResetOrdersAsync();
        await AddOneToCartAsync("customer-X");

        var (status, _) = await PlaceAsync("customer-X", "NOPE");
        status.ShouldBe(409);

        await using var session = Store.LightweightSession();
        (await session.Query<OrderStatusView>().AnyAsync(o => o.CustomerId == "customer-X")).ShouldBeFalse();
    }

    [Fact]
    public async Task redeeming_a_coupon_at_its_cap_is_rejected_with_no_order()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FLASH20", 20, 2);
        var couponId = await CouponIdAsync("FLASH20");

        // Two customers redeem to reach the cap.
        foreach (var c in new[] { "cust-1", "cust-2" })
        {
            await AddOneToCartAsync(c);
            (await PlaceAsync(c, "FLASH20")).Status.ShouldBe(201);
        }
        (await UsageNetCountAsync(couponId)).ShouldBe(2);

        // The third is refused — the cap is reached — and no order stream is created.
        await AddOneToCartAsync("cust-3");
        var (status, _) = await PlaceAsync("cust-3", "FLASH20");
        status.ShouldBe(409);

        await using var session = Store.LightweightSession();
        (await session.Query<OrderStatusView>().AnyAsync(o => o.CustomerId == "cust-3")).ShouldBeFalse();
        (await UsageNetCountAsync(couponId)).ShouldBe(2);
    }

    [Fact]
    public async Task concurrent_redemptions_never_exceed_the_cap()
    {
        await ResetOrdersAsync();
        const int cap = 3;
        const int racers = 6;
        await DefineCouponAsync("FLASH20", 20, cap);
        var couponId = await CouponIdAsync("FLASH20");

        var customers = Enumerable.Range(1, racers).Select(i => $"racer-{i}").ToArray();
        foreach (var c in customers)
        {
            await AddOneToCartAsync(c);
        }

        // Fire every checkout at once — the DCB boundary must let exactly `cap` through.
        var results = await Task.WhenAll(customers.Select(c => PlaceAsync(c, "FLASH20")));

        results.Count(r => r.Status == 201).ShouldBe(cap);
        results.Count(r => r.Status == 409).ShouldBe(racers - cap);
        (await UsageNetCountAsync(couponId)).ShouldBe(cap);

        // Exactly `cap` order streams exist for the racers.
        await using var session = Store.LightweightSession();
        var orders = await session.Query<OrderStatusView>()
            .Where(o => o.CouponCode == "FLASH20")
            .CountAsync();
        orders.ShouldBe(cap);
    }

    [Fact]
    public async Task a_no_coupon_order_carries_no_discount()
    {
        await ResetOrdersAsync();
        await AddOneToCartAsync("customer-X");

        var (status, orderId) = await PlaceAsync("customer-X");
        status.ShouldBe(201);

        await using var session = Store.LightweightSession();
        var order = await session.LoadAsync<OrderStatusView>(orderId!);
        order!.Subtotal.ShouldBe(20.00m);
        order.Discount.ShouldBe(0m);
        order.Total.ShouldBe(20.00m);
        order.CouponCode.ShouldBeNull();
    }

    // ── 6.4 Release a redemption on cancellation ────────────────────────────────────────────────────

    [Fact]
    public async Task cancelling_a_redeemed_order_returns_the_slot()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FLASH20", 20, 1);   // cap 1 — the slot is precious
        var couponId = await CouponIdAsync("FLASH20");

        await AddOneToCartAsync("customer-X");
        var (status, orderId) = await PlaceAsync("customer-X", "FLASH20");
        status.ShouldBe(201);
        (await UsageNetCountAsync(couponId)).ShouldBe(1);

        // Cancel via a stock-reservation failure (slice 4.5) — the release rides the cancellation.
        await _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.StockReservationFailed(orderId!, "insufficient"));

        await using (var session = Store.LightweightSession())
        {
            var events = await session.Events.FetchStreamAsync(orderId!);
            events.ShouldContain(e => e.Data is CouponRedemptionReleased);
        }

        // The slot is back in the pool.
        (await UsageNetCountAsync(couponId)).ShouldBe(0);

        // And a fresh redemption succeeds against the cap-1 coupon — the slot was genuinely returned.
        await AddOneToCartAsync("customer-Y");
        (await PlaceAsync("customer-Y", "FLASH20")).Status.ShouldBe(201);
        (await UsageNetCountAsync(couponId)).ShouldBe(1);
    }

    [Fact]
    public async Task cancelling_a_no_coupon_order_appends_no_release()
    {
        await ResetOrdersAsync();

        await AddOneToCartAsync("customer-X");
        var (status, orderId) = await PlaceAsync("customer-X");
        status.ShouldBe(201);

        await _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.StockReservationFailed(orderId!, "insufficient"));

        await using var session = Store.LightweightSession();
        var events = await session.Events.FetchStreamAsync(orderId!);
        events.ShouldNotContain(e => e.Data is CouponRedemptionReleased);
        // OrderPlaced + StockReservationFailed + OrderCancelled — exactly slice 4.5, unchanged.
        events.Count.ShouldBe(3);
    }
}
