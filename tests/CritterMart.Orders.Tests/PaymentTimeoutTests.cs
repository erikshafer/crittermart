using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Tests;

// Slice 4.7 (cancel on payment timeout): the fired OrderPaymentTimeout self-message IS the deadline
// passing — so these tests invoke it directly instead of waiting for real time (design.md
// Decision 5). Tracked sessions prove the cancel + the always-release cascade, and the
// terminal-state guard's no-ops. The Bruun todo-list endpoint is exercised over HTTP.
[Collection("orders")]
[Trait("Category", "Integration")]
public class PaymentTimeoutTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly OrderLine Plush = new("crit-001", 2, "Cosmic Critter Plush", 24.99m);
    private static readonly decimal Total = Plush.Quantity * Plush.Price;

    private readonly OrdersAppFixture _fixture;

    public PaymentTimeoutTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        await _fixture.ResetAllDataAsync();
    }

    // Seed an order directly on its stream — direct appends fold the inline projections but fire
    // no handlers, which is exactly the state a scheduled timeout finds when it wakes up.

    // An order at awaiting_confirmation: placed, but Inventory's reply never arrived.
    private async Task<string> SeedPlacedOrderAsync(string customerId)
    {
        var orderId = Guid.NewGuid().ToString();
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        session.Events.StartStream<Order>(
            orderId, new OrderPlaced(orderId, customerId, [Plush], Total));
        await session.SaveChangesAsync();
        return orderId;
    }

    // An order at stock_reserved: the stock gate cleared, but payment never answered.
    private async Task<string> SeedOrderAtPaymentGateAsync(string customerId)
    {
        var orderId = Guid.NewGuid().ToString();
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        session.Events.StartStream<Order>(
            orderId,
            new OrderPlaced(orderId, customerId, [Plush], Total),
            new StockReserved(orderId));
        await session.SaveChangesAsync();
        return orderId;
    }

    // Workshop § 6 slice 4.7 happy path: the order cleared the stock gate but payment never
    // answered. The fired timeout cancels it and hands the reserved stock back.
    [Fact]
    public async Task a_timeout_cancels_an_order_stuck_at_the_payment_gate_and_releases_its_stock()
    {
        await ResetOrdersAsync();
        var orderId = await SeedOrderAtPaymentGateAsync("customer-X");

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new OrderPaymentTimeout(orderId));

        // The cancel is recorded and visible.
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Cancelled);

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(3); // OrderPlaced + StockReserved + OrderCancelled
        events[2].Data.ShouldBeOfType<OrderCancelled>().Reason.ShouldBe(CancelReason.PaymentTimeout);

        // The reserved stock goes back: exactly one ReleaseStock cascaded with the order's lines.
        var release = tracked.Sent.SingleMessage<Contracts.ReleaseStock>();
        release.OrderId.ShouldBe(orderId);
        release.Lines.ShouldHaveSingleItem();
        release.Lines[0].Sku.ShouldBe(Plush.Sku);
        release.Lines[0].Quantity.ShouldBe(Plush.Quantity);

        // And the todo-list row is gone (the conditional delete on OrderCancelled).
        var row = await session.LoadAsync<OrderAwaitingPayment>(orderId);
        row.ShouldBeNull();
    }

    // Workshop § 4.7 delayed-grant race (design.md Decision 2): the order never recorded a
    // StockReserved — Inventory's reply may have been lost while it holds a real reservation.
    // The timeout still releases; Inventory's per-SKU guard decides whether anything is held.
    [Fact]
    public async Task a_timeout_cancels_an_unanswered_order_and_still_releases()
    {
        await ResetOrdersAsync();
        var orderId = await SeedPlacedOrderAsync("customer-Y"); // OrderPlaced only — awaiting_confirmation

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new OrderPaymentTimeout(orderId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Cancelled);

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(2); // OrderPlaced + OrderCancelled
        events[1].Data.ShouldBeOfType<OrderCancelled>().Reason.ShouldBe(CancelReason.PaymentTimeout);

        // The release is cascaded even though THIS stream never saw the grant.
        var release = tracked.Sent.SingleMessage<Contracts.ReleaseStock>();
        release.OrderId.ShouldBe(orderId);
    }

    // The terminal-state guard, success side: the order confirmed before its timer fired. The
    // timer losing this race is its normal fate for every successful order.
    [Fact]
    public async Task a_timeout_is_a_no_op_on_a_confirmed_order()
    {
        await ResetOrdersAsync();
        var orderId = await SeedPlacedOrderAsync("customer-Z");

        // Drive the order to confirmed through the real handler chain (stock grant → payment
        // approved by the fixture's stub → confirmed).
        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new OrderPaymentTimeout(orderId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Nothing changed: same four events, still confirmed, nothing cascaded.
        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(4); // OrderPlaced + StockReserved + PaymentAuthorized + OrderConfirmed

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Confirmed);

        tracked.Sent.AllMessages().OfType<Contracts.ReleaseStock>().ShouldBeEmpty();
    }

    // The terminal-state guard, duplicate side: at-least-once delivery can fire the same timeout
    // twice. The second finds the stream already cancelled and does nothing.
    [Fact]
    public async Task a_duplicate_timeout_is_a_no_op()
    {
        await ResetOrdersAsync();
        var orderId = await SeedOrderAtPaymentGateAsync("customer-W");

        await _fixture.Host.InvokeMessageAndWaitAsync(new OrderPaymentTimeout(orderId));
        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new OrderPaymentTimeout(orderId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Still exactly one cancellation — the duplicate appended nothing and released nothing.
        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(3); // OrderPlaced + StockReserved + OrderCancelled

        tracked.Sent.AllMessages().OfType<Contracts.ReleaseStock>().ShouldBeEmpty();
    }

    // The Bruun todo-list over HTTP: a placed order appears with its deadline; a settled order
    // disappears (the conditional delete). Drives the full pipeline — cart, checkout, projection,
    // endpoint — rather than seeding streams.
    [Fact]
    public async Task the_awaiting_payment_list_shows_open_orders_and_drops_settled_ones()
    {
        await ResetOrdersAsync();

        // Place an order through the front door.
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(Plush.Sku, Plush.Quantity, CosmicCritterPlush))
                .ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-V"));
            _.StatusCodeShouldBe(201);
        });
        var placed = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url("/orders");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-V"));
            _.StatusCodeShouldBe(201);
        });
        var orderId = placed.ReadAsJson<PlaceOrderResponse>()!.OrderId;

        // The order is on the todo-list, deadline in the future.
        var listed = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/orders/awaiting-payment");
            _.StatusCodeShouldBe(200);
        });
        var rows = listed.ReadAsJson<List<OrderAwaitingPaymentRow>>()!;
        var row = rows.ShouldHaveSingleItem();
        row.Id.ShouldBe(orderId);
        row.Total.ShouldBe(Total);
        row.Deadline.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        // Settle the order (grant → stub approves → confirmed) — the row disappears.
        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        var afterSettle = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/orders/awaiting-payment");
            _.StatusCodeShouldBe(200);
        });
        afterSettle.ReadAsJson<List<OrderAwaitingPaymentRow>>()!.ShouldBeEmpty();
    }
}
