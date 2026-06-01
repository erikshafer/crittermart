using Alba;
using Contracts = CritterMart.Contracts;
using CritterMart.Orders.Cart;
using CritterMart.Orders.Features;
using CritterMart.Orders.Order;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Orders.Tests;

[Collection("orders")]
[Trait("Category", "Integration")]
public class PlaceOrderTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly OrdersAppFixture _fixture;

    public PlaceOrderTests(OrdersAppFixture fixture) => _fixture = fixture;

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

    private async Task<string> PlaceOrderAsync(string customerId)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new PlaceOrder(customerId)).ToUrl("/orders");
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<PlaceOrderResponse>()!.OrderId;
    }

    // Workshop 001 § 6.1 slice 4.1, happy path: checkout turns the open cart into an order and
    // closes the cart — both stream writes commit in one transaction.
    [Fact]
    public async Task placing_an_order_creates_an_order_and_checks_out_the_cart()
    {
        await ResetOrdersAsync();

        var cartId = await AddAsync("customer-X", "crit-001", 2, CosmicCritterPlush);
        await AddAsync("customer-X", "crit-002", 3, NebulaNewt);

        var orderId = await PlaceOrderAsync("customer-X");

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The Order stream holds OrderPlaced; OrderStatusView shows the lines, total, status.
        var order = await session.LoadAsync<OrderStatusView>(orderId);
        order.ShouldNotBeNull();
        order.CustomerId.ShouldBe("customer-X");
        order.Status.ShouldBe(OrderStatus.AwaitingConfirmation);
        order.Lines.Count.ShouldBe(2);
        order.Total.ShouldBe(2 * 24.99m + 3 * 18.00m); // 103.98 — sum of quantity × snapshot price

        var orderEvents = await session.Events.FetchStreamAsync(orderId);
        orderEvents.Count.ShouldBe(1);
        orderEvents[0].Data.ShouldBeOfType<OrderPlaced>();

        // The same transaction checked out the cart: CartCheckedOut appended, IsOpen flipped.
        var cart = await session.LoadAsync<CartView>(cartId);
        cart.ShouldNotBeNull();
        cart.IsOpen.ShouldBeFalse();

        var cartEvents = await session.Events.FetchStreamAsync(cartId);
        cartEvents[^1].Data.ShouldBeOfType<CartCheckedOut>();
    }

    // Workshop 001 § 6.1 slice 4.2: placing an order cascades a single whole-order ReserveStock
    // to Inventory carrying every line. The tracked session captures the cascaded message.
    [Fact]
    public async Task placing_an_order_cascades_a_whole_order_reserve_stock_request()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 2, CosmicCritterPlush);
        await AddAsync("customer-X", "crit-002", 3, NebulaNewt);

        var orderId = string.Empty;
        var tracked = await _fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            orderId = await PlaceOrderAsync("customer-X");
        });

        var reserve = tracked.Sent.SingleMessage<Contracts.ReserveStock>();
        reserve.OrderId.ShouldBe(orderId);
        reserve.Lines.Count.ShouldBe(2);
        reserve.Lines.ShouldContain(l => l.Sku == "crit-001" && l.Quantity == 2);
        reserve.Lines.ShouldContain(l => l.Sku == "crit-002" && l.Quantity == 3);
    }

    // Workshop 001 § 6 slice 4.7: placing an order also schedules the payment-deadline
    // self-message. The tracked session captures it as scheduled — not executed — so this test
    // never waits for real time; the timeout's behavior when it fires lives in PaymentTimeoutTests.
    [Fact]
    public async Task placing_an_order_schedules_a_payment_timeout()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        var orderId = string.Empty;
        var tracked = await _fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            orderId = await PlaceOrderAsync("customer-X");
        });

        var timeout = tracked.Scheduled.SingleMessage<OrderPaymentTimeout>();
        timeout.OrderId.ShouldBe(orderId);
    }

    // Workshop 001 § 6.1 slice 4.1, failure path: no open cart → rejected, no Order stream.
    [Fact]
    public async Task placing_an_order_with_no_open_cart_is_rejected()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new PlaceOrder("nobody")).ToUrl("/orders");
            _.StatusCodeShouldBe(409);
        });
    }

    // Workshop 001 § 6.1 slice 4.1, failure path: a second placement (the cart is already
    // checked out) is rejected and creates no second order.
    [Fact]
    public async Task placing_an_order_twice_is_rejected_the_second_time()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        await PlaceOrderAsync("customer-X");

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new PlaceOrder("customer-X")).ToUrl("/orders");
            _.StatusCodeShouldBe(409);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var orders = await session.Query<OrderStatusView>()
            .Where(o => o.CustomerId == "customer-X")
            .ToListAsync();
        orders.Count.ShouldBe(1);
    }

    // The order is readable over HTTP at GET /orders/{orderId}.
    [Fact]
    public async Task the_order_is_readable_over_http()
    {
        await ResetOrdersAsync();

        await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        var orderId = await PlaceOrderAsync("customer-X");

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/orders/{orderId}");
            _.StatusCodeShouldBe(200);
        });

        var view = result.ReadAsJson<OrderStatusView>();
        view.ShouldNotBeNull();
        view.Status.ShouldBe(OrderStatus.AwaitingConfirmation);
    }
}
