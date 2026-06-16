using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

[Collection("orders")]
[Trait("Category", "Integration")]
public class ChangeCartItemQuantityTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);

    private readonly OrdersAppFixture _fixture;

    public ChangeCartItemQuantityTests(OrdersAppFixture fixture) => _fixture = fixture;

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
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl($"/carts/{customerId}/items");
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<AddToCartResponse>()!.CartId;
    }

    // Workshop 001 § 6.1 slice 3.3, happy path: the line shows the new quantity at the
    // unchanged snapshot price.
    [Fact]
    public async Task changing_quantity_updates_the_line()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeCartItemQuantity(3)).ToUrl("/carts/customer-X/items/crit-001/quantity");
            _.StatusCodeShouldBe(204);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The line's quantity changed; the snapshotted name/price did not.
        var view = await session.LoadAsync<CartView>(cartId);
        view.ShouldNotBeNull();
        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(3);
        line.Price.ShouldBe(24.99m);

        // The stream records the change as its own event kind.
        var events = await session.Events.FetchStreamAsync(cartId);
        var changed = events[^1].Data.ShouldBeOfType<CartItemQuantityChanged>();
        changed.Quantity.ShouldBe(3);
    }

    // Workshop 001 § 6.1 slice 3.3, failure path: zero (or negative) is rejected — removing an
    // item is RemoveCartItem's job, not a zero quantity. 400: malformed input, not a conflict.
    [Fact]
    public async Task changing_quantity_to_zero_is_rejected()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeCartItemQuantity(0)).ToUrl("/carts/customer-X/items/crit-001/quantity");
            _.StatusCodeShouldBe(400);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // No event appended; the line still shows the original quantity.
        var events = await session.Events.FetchStreamAsync(cartId);
        events.Count.ShouldBe(2);

        var view = await session.LoadAsync<CartView>(cartId);
        view.ShouldNotBeNull();
        view.Lines.ShouldHaveSingleItem().Quantity.ShouldBe(1);
    }

    // Changing a quantity presumes the line exists — mirror of slice 3.2's CartItemNotPresent
    // (design.md faithfulness note 2: an obvious extension, not in the workshop text).
    [Fact]
    public async Task changing_quantity_of_an_item_not_in_the_cart_is_rejected()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-002", 3, new ProductSnapshot("Nebula Newt", 18.00m));

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeCartItemQuantity(3)).ToUrl("/carts/customer-X/items/crit-001/quantity");
            _.StatusCodeShouldBe(409);
        });
    }

    // Editing requires an open cart: a customer with no cart at all gets NoOpenCart.
    [Fact]
    public async Task changing_quantity_with_no_open_cart_is_rejected()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeCartItemQuantity(3)).ToUrl("/carts/nobody/items/crit-001/quantity");
            _.StatusCodeShouldBe(409);
        });
    }
}
