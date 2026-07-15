using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

[Collection("orders")]
[Trait("Category", "Integration")]
public class RemoveCartItemTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly OrdersAppFixture _fixture;

    public RemoveCartItemTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    private async Task<string> AddAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<AddToCartResponse>()!.CartId;
    }

    // Workshop 001 § 6.1 slice 3.2, happy path: removing a SKU appends CartItemRemoved and the
    // CartView line disappears; the other line is untouched.
    [Fact]
    public async Task removing_an_item_drops_its_line_from_the_cart()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        await AddAsync("customer-X", "crit-002", 3, NebulaNewt);

        await _fixture.Host.Scenario(_ =>
        {
            _.Delete.Url("/carts/mine/items/crit-001");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(204);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The CartView keeps only the other SKU's line.
        var view = await session.LoadAsync<CartView>(cartId);
        view.ShouldNotBeNull();
        view.IsOpen.ShouldBeTrue();
        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-002");

        // The stream records the removal as a new fact — nothing is erased.
        var events = await session.Events.FetchStreamAsync(cartId);
        var removed = events[^1].Data.ShouldBeOfType<CartItemRemoved>();
        removed.Sku.ShouldBe("crit-001");
    }

    // Workshop 001 § 6.1 slice 3.2, failure path: removing a SKU not in the cart is rejected
    // with CartItemNotPresent and appends no event.
    [Fact]
    public async Task removing_an_item_not_in_the_cart_is_rejected()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-002", 3, NebulaNewt);

        await _fixture.Host.Scenario(_ =>
        {
            _.Delete.Url("/carts/mine/items/crit-001");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(409);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // No event was appended: the stream still holds only CartCreated + the one CartItemAdded.
        var events = await session.Events.FetchStreamAsync(cartId);
        events.Count.ShouldBe(2);
    }

    // Editing requires an open cart: a customer with no cart at all gets NoOpenCart.
    [Fact]
    public async Task removing_with_no_open_cart_is_rejected()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Delete.Url("/carts/mine/items/crit-001");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("nobody"));
            _.StatusCodeShouldBe(409);
        });
    }

    // Removing the last line leaves the cart open and empty (design.md decision 5) — it is NOT
    // closed or abandoned; the customer can keep adding to it.
    [Fact]
    public async Task removing_the_last_item_leaves_an_open_empty_cart()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        await _fixture.Host.Scenario(_ =>
        {
            _.Delete.Url("/carts/mine/items/crit-001");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(204);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<CartView>(cartId);
        view.ShouldNotBeNull();
        view.IsOpen.ShouldBeTrue();
        view.Lines.ShouldBeEmpty();
    }

    // Hard cutover (ADR 023): a remove with no Bearer token is rejected with 401 by [Authorize] before
    // any open-cart resolution runs — unauthenticated, mirroring the cart read and the other two cart
    // commands (the pre-cutover "missing header → 400" died with the header).
    [Fact]
    public async Task removing_without_a_token_returns_401()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Delete.Url("/carts/mine/items/crit-001");
            _.StatusCodeShouldBe(401);
        });
    }
}
