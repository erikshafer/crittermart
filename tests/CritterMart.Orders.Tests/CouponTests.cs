using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Promotions;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.AspNetCore.Mvc;
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
    private async Task DefineCouponAsync(
        string code, int discountPercent, int cap, bool oneRedemptionPerCustomer = false, int expectedStatus = 201)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new DefineCoupon(code, discountPercent, cap, oneRedemptionPerCustomer)).ToUrl("/coupons");
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

    // GET /coupons/{code}/validate — the advisory read (slice 6.2, enriched by 6.6). Always 200; the answer
    // is discriminated by Status (valid/invalid/exhausted/already_redeemed).
    //
    // Slice 6.6: the endpoint is OPTIONALLY authenticated. `customerId: null` (the default) sends NO bearer
    // token — the anonymous path, whose answer is pinned byte-for-byte to slice 6.2 and must never be 401.
    // Supplying a customerId sends the bearer, unlocking the per-customer `already_redeemed` answer.
    private async Task<CouponValidation> ValidateCouponAsync(string code, string? customerId = null)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/coupons/{code}/validate");
            if (customerId is not null)
            {
                _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            }
            _.StatusCodeShouldBe(200);
        });
        return result.ReadAsJson<CouponValidation>()!;
    }

    // Place an order expecting a ProblemDetails refusal, returning (status, title, detail) so a test can
    // assert the machine-readable title and the human copy independently (slice 6.6 moves only the latter).
    private async Task<(int Status, string? Title, string? Detail)> PlaceExpectingProblemAsync(
        string customerId, string couponCode)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url($"/orders?couponCode={couponCode}");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.IgnoreStatusCode();
        });

        var problem = result.ReadAsJson<ProblemDetails>();
        return (result.Context.Response.StatusCode, problem?.Title, problem?.Detail);
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

    // ── 6.5 One redemption per customer (the composite (coupon × customer) DCB) ──────────────────────

    [Fact]
    public async Task defining_a_per_customer_coupon_records_the_flag()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await using var session = Store.LightweightSession();
        var view = await session.Query<CouponView>().FirstAsync(c => c.Code == "FIRSTORDER");
        view.OneRedemptionPerCustomer.ShouldBeTrue();
    }

    [Fact]
    public async Task a_per_customer_coupon_admits_a_customer_once_then_rejects_a_second_redemption()
    {
        await ResetOrdersAsync();
        // High global cap so ONLY the per-customer boundary can bite.
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        // First redemption succeeds.
        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);

        // A second redemption by the SAME customer — a fresh open cart, a later order — is refused by the
        // composite boundary's existence check, with no order stream created.
        await AddOneToCartAsync("customer-X");
        var (status, _) = await PlaceAsync("customer-X", "FIRSTORDER");
        status.ShouldBe(409);

        await using var session = Store.LightweightSession();
        // Exactly one order by customer-X carries the coupon — the second never became an order.
        var placed = await session.Query<OrderStatusView>()
            .Where(o => o.CustomerId == "customer-X" && o.CouponCode == "FIRSTORDER")
            .CountAsync();
        placed.ShouldBe(1);
    }

    [Fact]
    public async Task a_per_customer_coupon_still_admits_a_different_customer()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);

        // A DIFFERENT customer is a distinct (coupon × customer) pair — an independent composite boundary at 0.
        await AddOneToCartAsync("customer-Y");
        (await PlaceAsync("customer-Y", "FIRSTORDER")).Status.ShouldBe(201);
    }

    [Fact]
    public async Task concurrent_redemptions_by_different_customers_of_a_per_customer_coupon_all_succeed()
    {
        await ResetOrdersAsync();
        const int customers = 6;
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        var ids = Enumerable.Range(1, customers).Select(i => $"firstorder-{i}").ToArray();
        foreach (var c in ids)
        {
            await AddOneToCartAsync(c);
        }

        // Each customer holds an INDEPENDENT (FIRSTORDER × customer) composite boundary, so a concurrent
        // burst must not false-conflict on the per-customer boundary; the high global cap admits them all.
        var results = await Task.WhenAll(ids.Select(c => PlaceAsync(c, "FIRSTORDER")));
        results.Count(r => r.Status == 201).ShouldBe(customers);

        await using var session = Store.LightweightSession();
        var orders = await session.Query<OrderStatusView>()
            .Where(o => o.CouponCode == "FIRSTORDER")
            .CountAsync();
        orders.ShouldBe(customers);
    }

    [Fact]
    public async Task cancelling_a_per_customer_redemption_lets_the_customer_redeem_again()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await AddOneToCartAsync("customer-X");
        var (status, orderId) = await PlaceAsync("customer-X", "FIRSTORDER");
        status.ShouldBe(201);

        // A second attempt is refused while the first redemption stands.
        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(409);

        // Cancel the first order (stock-reservation failure, slice 4.5) — the release carries the composite
        // tag too, so the per-customer boundary decrements and the customer's slot returns.
        await _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.StockReservationFailed(orderId!, "insufficient"));

        await using (var session = Store.LightweightSession())
        {
            var events = await session.Events.FetchStreamAsync(orderId!);
            events.ShouldContain(e => e.Data is CouponRedemptionReleased);
        }

        // And now the same customer can redeem it again — the slot was genuinely returned.
        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);
    }

    [Fact]
    public async Task a_non_per_customer_coupon_lets_one_customer_redeem_more_than_once()
    {
        await ResetOrdersAsync();
        // FLASH20 is global-cap-only (oneRedemptionPerCustomer defaults false); cap 3 leaves room for two.
        await DefineCouponAsync("FLASH20", 20, 3);
        var couponId = await CouponIdAsync("FLASH20");

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FLASH20")).Status.ShouldBe(201);

        // The SAME customer redeems again — no composite boundary is opened, so only the global cap applies.
        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FLASH20")).Status.ShouldBe(201);

        (await UsageNetCountAsync(couponId)).ShouldBe(2);
    }

    // ── 6.6 Preview a per-customer coupon & explain the refusal ──────────────────────────────────────
    //
    // The storefront half of 6.5: the validate query becomes optionally authenticated and gains a fourth
    // status, and the checkout refusal gains customer-facing copy. NOTHING here writes an event.

    [Fact]
    public async Task validating_a_per_customer_coupon_warns_the_customer_who_already_redeemed()
    {
        await ResetOrdersAsync();
        // High global cap so ONLY the per-customer dimension can produce a non-valid answer.
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);
        var couponId = await CouponIdAsync("FIRSTORDER");

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);

        // Signed in, and the advisory view holds this pair at net 1 → the personal reason, before checkout.
        var result = await ValidateCouponAsync("FIRSTORDER", "customer-X");
        result.Code.ShouldBe("FIRSTORDER");
        result.Status.ShouldBe(CouponValidationStatus.AlreadyRedeemed);
        result.DiscountPercent.ShouldBeNull();

        // Advisory read — it moved nothing.
        (await UsageNetCountAsync(couponId)).ShouldBe(1);
    }

    [Fact]
    public async Task validating_a_per_customer_coupon_shows_the_discount_to_a_customer_who_has_not_redeemed()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);

        // customer-Y holds a DISTINCT pair at net 0 — one customer's redemption is not another's.
        var result = await ValidateCouponAsync("FIRSTORDER", "customer-Y");
        result.Status.ShouldBe(CouponValidationStatus.Valid);
        result.DiscountPercent.ShouldBe(15);
    }

    [Fact]
    public async Task validating_a_per_customer_coupon_anonymously_gives_the_unchanged_global_answer()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);

        // NO bearer token. The pinned slice-6.2 contract: a 200 (never 401), the global-cap answer, and
        // `already_redeemed` unreachable — the query holds no identity, so it makes no personal claim.
        var result = await ValidateCouponAsync("FIRSTORDER");
        result.Status.ShouldBe(CouponValidationStatus.Valid);
        result.DiscountPercent.ShouldBe(15);
    }

    [Fact]
    public async Task validating_a_global_cap_only_coupon_never_reports_already_redeemed()
    {
        await ResetOrdersAsync();
        // FLASH20 carries no per-customer policy, so the per-customer view is not consulted at all —
        // redeeming it twice is that coupon's CHOSEN policy, not a condition to warn about.
        await DefineCouponAsync("FLASH20", 20, 3);

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FLASH20")).Status.ShouldBe(201);

        var result = await ValidateCouponAsync("FLASH20", "customer-X");
        result.Status.ShouldBe(CouponValidationStatus.Valid);
        result.DiscountPercent.ShouldBe(20);
    }

    [Fact]
    public async Task the_personal_reason_outranks_the_crowd_reason()
    {
        await ResetOrdersAsync();
        // cap 2 AND per-customer: two customers exhaust the coupon globally, and one of them is also
        // personally spent — so both refusal conditions hold simultaneously for customer-X.
        await DefineCouponAsync("FIRSTORDER", 15, 2, oneRedemptionPerCustomer: true);
        var couponId = await CouponIdAsync("FIRSTORDER");

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);
        await AddOneToCartAsync("customer-Y");
        (await PlaceAsync("customer-Y", "FIRSTORDER")).Status.ShouldBe(201);
        (await UsageNetCountAsync(couponId)).ShouldBe(2);   // globally exhausted

        // already_redeemed, NOT exhausted — mirroring checkout's ordering exactly. The two reasons lead to
        // different remedies ("try another code" vs "try again later"); blaming the crowd for a personal
        // refusal would teach the wrong one.
        var mine = await ValidateCouponAsync("FIRSTORDER", "customer-X");
        mine.Status.ShouldBe(CouponValidationStatus.AlreadyRedeemed);

        // A customer who never redeemed it still hears the crowd reason — the ladder is per-caller.
        var theirs = await ValidateCouponAsync("FIRSTORDER", "customer-Z");
        theirs.Status.ShouldBe(CouponValidationStatus.Exhausted);
    }

    [Fact]
    public async Task a_cancelled_redemption_restores_the_preview()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await AddOneToCartAsync("customer-X");
        var (status, orderId) = await PlaceAsync("customer-X", "FIRSTORDER");
        status.ShouldBe(201);
        (await ValidateCouponAsync("FIRSTORDER", "customer-X")).Status
            .ShouldBe(CouponValidationStatus.AlreadyRedeemed);

        // Cancel via a stock-reservation failure (slice 4.5) — the release carries CustomerId, so the
        // advisory view decrements the same pair document the redemption incremented.
        await _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.StockReservationFailed(orderId!, "insufficient"));

        // The advisory view and the DCB boundary agree, because both fold the same two events — they differ
        // in WHEN, not in WHAT. (The boundary's agreement is proven by the 6.5 re-redemption test.)
        var result = await ValidateCouponAsync("FIRSTORDER", "customer-X");
        result.Status.ShouldBe(CouponValidationStatus.Valid);
        result.DiscountPercent.ShouldBe(15);
    }

    [Fact]
    public async Task the_per_customer_refusal_keeps_its_title_and_gains_customer_facing_copy()
    {
        await ResetOrdersAsync();
        await DefineCouponAsync("FIRSTORDER", 15, 100000, oneRedemptionPerCustomer: true);

        await AddOneToCartAsync("customer-X");
        (await PlaceAsync("customer-X", "FIRSTORDER")).Status.ShouldBe(201);

        await AddOneToCartAsync("customer-X");
        var (status, title, detail) = await PlaceExpectingProblemAsync("customer-X", "FIRSTORDER");

        // UNCHANGED — the machine-readable contract. Slice 6.6 moves only the human sentence.
        status.ShouldBe(409);
        title.ShouldBe("CouponAlreadyRedeemedByCustomer");

        // The 6.6 copy: names the personal reason, hands the decision back, no interpolated code.
        detail.ShouldBe("You've already used this coupon — remove it to continue, or try another.");
    }
}
