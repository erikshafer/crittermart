using Alba;
using CritterMart.Orders.Cart;
using CritterMart.Orders.Features;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

[Collection("orders")]
[Trait("Category", "Integration")]
public class AddToCartTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly OrdersAppFixture _fixture;

    public AddToCartTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    // POST an item for a customer; returns the cart's id (generated on the first add).
    private async Task<string> AddAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl($"/carts/{customerId}/items");
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<AddToCartResponse>()!.CartId;
    }

    // Workshop 001 § 6.1 slice 3.1, scenario 1: the first add creates a new cart.
    [Fact]
    public async Task adding_the_first_item_creates_a_new_cart_with_one_line()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The inline CartView snapshot shows a single line at the snapshot price.
        var view = await session.LoadAsync<CartView>(cartId);
        view.ShouldNotBeNull();
        view.CustomerId.ShouldBe("customer-X");
        view.IsOpen.ShouldBeTrue();
        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(1);
        line.Price.ShouldBe(24.99m);

        // The new stream records CartCreated then CartItemAdded.
        var events = await session.Events.FetchStreamAsync(cartId);
        events.Count.ShouldBe(2);
        var created = events[0].Data.ShouldBeOfType<CartCreated>();
        created.CustomerId.ShouldBe("customer-X");
        events[1].Data.ShouldBeOfType<CartItemAdded>();
    }

    // Workshop 001 § 6.1 slice 3.1, scenario 2: a second add appends to the same open cart.
    [Fact]
    public async Task adding_a_second_item_appends_to_the_same_cart()
    {
        await ResetOrdersAsync();

        var firstCartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        var secondCartId = await AddAsync("customer-X", "crit-002", 3, NebulaNewt);

        // No new cart: the second add resolved the same open cart.
        secondCartId.ShouldBe(firstCartId);

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The CartView now shows two lines, each at its snapshot price.
        var view = await session.LoadAsync<CartView>(firstCartId);
        view.ShouldNotBeNull();
        view.Lines.Count.ShouldBe(2);
        view.Lines.ShouldContain(l => l.Sku == "crit-001" && l.Quantity == 1 && l.Price == 24.99m);
        view.Lines.ShouldContain(l => l.Sku == "crit-002" && l.Quantity == 3 && l.Price == 18.00m);

        // Exactly one Cart stream exists for the customer, with three events.
        var carts = await session.Query<CartView>().Where(v => v.CustomerId == "customer-X").ToListAsync();
        carts.Count.ShouldBe(1);

        var events = await session.Events.FetchStreamAsync(firstCartId);
        events.Count.ShouldBe(3);
        events[0].Data.ShouldBeOfType<CartCreated>();
        events[1].Data.ShouldBeOfType<CartItemAdded>();
        events[2].Data.ShouldBeOfType<CartItemAdded>();
    }

    // The cart is readable over HTTP at GET /carts/{cartId}.
    [Fact]
    public async Task the_cart_is_readable_over_http()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/carts/{cartId}");
            _.StatusCodeShouldBe(200);
        });

        var view = result.ReadAsJson<CartView>();
        view.ShouldNotBeNull();
        view.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-001");
    }
}
