using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 3.5 — "View my open cart" (GET /carts/mine). The read counterpart to the customer-keyed
// write side: resolves the customer's single open CartView by identity (the JWT `sub` claim), with
// no cartId. Closes the pre-frontend audit's blocking Gap #1 over the existing open-cart index.
[Collection("orders")]
[Trait("Category", "Integration")]
public class ViewMyCartTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly OrdersAppFixture _fixture;

    public ViewMyCartTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        await _fixture.ResetAllDataAsync();
    }

    private async Task AddAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });
    }

    private async Task PlaceOrderAsync(string customerId)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url("/orders");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });
    }

    // Workshop 001 § 6 slice 3.5, happy path: the single open cart is resolved BY IDENTITY and
    // returned with its SKU-keyed lines at snapshot prices. A second customer's cart exists too, so
    // the test proves the resolution is customer-keyed, not "return any open cart."
    [Fact]
    public async Task viewing_my_cart_returns_my_open_cart_resolved_by_identity()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 2, CosmicCritterPlush);
        await AddAsync("customer-X", "crit-002", 3, NebulaNewt);
        await AddAsync("customer-Y", "crit-001", 1, CosmicCritterPlush);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(200);
        });

        var view = result.ReadAsJson<CartView>();
        view.ShouldNotBeNull();
        view.CustomerId.ShouldBe("customer-X");
        view.IsOpen.ShouldBeTrue();
        view.Lines.Count.ShouldBe(2);
        view.Lines.ShouldContain(l => l.Sku == "crit-001" && l.Quantity == 2 && l.Price == 24.99m);
        view.Lines.ShouldContain(l => l.Sku == "crit-002" && l.Quantity == 3 && l.Price == 18.00m);
    }

    // Workshop 001 § 6 slice 3.5, edge (cold start): a customer who never created a cart resolves to
    // "no open cart" — 404, not an error. The storefront renders an empty cart.
    [Fact]
    public async Task viewing_my_cart_with_no_open_cart_returns_404()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(404);
        });
    }

    // Slice 3.5 edge (terminal cart): a checked-out cart has IsOpen=false and no longer resolves as
    // open, so the read returns 404 — the customer is freed to start a fresh cart. CartAbandoned
    // (3.4) is the other terminal event and behaves identically; checkout is the one reachable from
    // this fixture's HTTP surface.
    [Fact]
    public async Task viewing_my_cart_after_checkout_returns_404()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        await PlaceOrderAsync("customer-X");

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(404);
        });
    }

    // Hard cutover (ADR 023): a token-less request is 401'd by [Authorize] — unauthenticated, kept
    // distinct from the 404 that means "no open cart." (Supersedes design.md Decision 5's "missing
    // header → 400": absent credentials stopped being a malformed request when the header retired.)
    [Fact]
    public async Task viewing_my_cart_without_a_token_returns_401()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.StatusCodeShouldBe(401);
        });
    }
}
