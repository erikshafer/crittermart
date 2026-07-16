using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
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
        await _fixture.ResetAllDataAsync();
    }

    // POST an item for a customer; returns the cart's id (generated on the first add).
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

    // Slice 3.4: creating a cart also schedules its inactivity deadline. The tracked session
    // captures it as scheduled — not executed — so this test never waits for real time; the
    // fired timeout's behavior lives in CartAbandonmentTests.
    [Fact]
    public async Task creating_a_cart_schedules_an_activity_timeout()
    {
        await ResetOrdersAsync();

        var cartId = string.Empty;
        var tracked = await _fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        });

        var timeout = tracked.Scheduled.SingleMessage<CartActivityTimeout>();
        timeout.CartId.ShouldBe(cartId);
    }

    // Slice 3.4, fire-and-check: a SUBSEQUENT add to the same open cart schedules nothing — one
    // timeout per cart suffices; this add's event timestamp is the activity the fired timeout
    // will check (and re-aim itself against).
    [Fact]
    public async Task adding_to_an_existing_cart_schedules_no_further_timeout()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        var tracked = await _fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            await AddAsync("customer-X", "crit-002", 3, NebulaNewt);
        });

        tracked.Scheduled.AllMessages().OfType<CartActivityTimeout>().ShouldBeEmpty();
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

    // Boundary guard (change 026): a command with no product snapshot is malformed input. The cart never
    // reads the Catalog, so the snapshot is a cart line's only source of product truth — an absent one has
    // nothing to build a line from. The Validate guard rejects it with 400 BEFORE the handler runs, so no
    // Cart stream is created and no event is appended. (Before the guard the null snapshot reached the
    // CartLines.Add fold and surfaced as a 500 NRE.)
    [Fact]
    public async Task adding_an_item_without_a_product_snapshot_is_rejected_and_creates_no_cart()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, null!)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(400);
        });

        // The short-circuit appended nothing: the customer has no cart at all.
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var carts = await session.Query<CartView>().Where(v => v.CustomerId == "customer-X").ToListAsync();
        carts.ShouldBeEmpty();
    }

    // The snapshot must be usable, not merely present: a blank product name can't title a cart line.
    [Fact]
    public async Task adding_an_item_with_a_blank_product_name_is_rejected()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, new ProductSnapshot("", 24.99m)))
                .ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(400);
        });
    }

    // A negative snapshot price is nonsensical for a cart line and is refused at the boundary.
    [Fact]
    public async Task adding_an_item_with_a_negative_price_is_rejected()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, new ProductSnapshot("Cosmic Critter Plush", -1m)))
                .ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-X"));
            _.StatusCodeShouldBe(400);
        });
    }

    // Hard cutover (ADR 023): an add with a VALID snapshot but no Bearer token is rejected with 401 by
    // [Authorize] before any guard or handler runs — an unauthenticated request, not a malformed one
    // (the pre-cutover "missing header → 400" died with the header).
    [Fact]
    public async Task adding_without_a_token_returns_401()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, CosmicCritterPlush)).ToUrl("/carts/mine/items");
            _.StatusCodeShouldBe(401);
        });

        // The middleware short-circuit appended nothing: the token-less request created no cart at all.
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var carts = await session.Query<CartView>().ToListAsync();
        carts.ShouldBeEmpty();
    }
}
