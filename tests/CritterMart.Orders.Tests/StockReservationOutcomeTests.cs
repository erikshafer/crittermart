using Contracts = CritterMart.Contracts;
using CritterMart.Orders.Order;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 4.2 / 4.5: Inventory's reservation reply lands on the Order stream as a Klefter local
// commit. Tracked-session tests drive the inbound handlers against a seeded placed order; no
// broker (transports are stubbed in the fixture).
[Collection("orders")]
[Trait("Category", "Integration")]
public class StockReservationOutcomeTests
{
    private static readonly OrderLine Plush = new("crit-001", 2, "Cosmic Critter Plush", 24.99m);

    private readonly OrdersAppFixture _fixture;

    public StockReservationOutcomeTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    // Seed an order awaiting confirmation directly on its stream — the state Inventory replies into.
    private async Task<string> SeedPlacedOrderAsync(string customerId)
    {
        var orderId = Guid.NewGuid().ToString();
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        session.Events.StartStream<OrderStatusView>(
            orderId, new OrderPlaced(orderId, customerId, [Plush], Plush.Quantity * Plush.Price));
        await session.SaveChangesAsync();
        return orderId;
    }

    // Klefter grant: StockReserved → Order stream StockReserved, status stock_reserved.
    [Fact]
    public async Task a_granted_reservation_is_recorded_and_advances_the_status()
    {
        await ResetOrdersAsync();
        var orderId = await SeedPlacedOrderAsync("customer-X");

        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.StockReserved);

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(2); // OrderPlaced + StockReserved
        events[^1].Data.ShouldBeOfType<StockReserved>();
    }

    // Klefter refusal (slice 4.5): StockReservationFailed → failure commit + OrderCancelled, cancelled.
    [Fact]
    public async Task a_failed_reservation_records_the_failure_and_cancels_the_order()
    {
        await ResetOrdersAsync();
        var orderId = await SeedPlacedOrderAsync("customer-Y");

        await _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.StockReservationFailed(orderId, "insufficient"));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Cancelled);

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(3); // OrderPlaced + StockReservationFailed + OrderCancelled
        events[1].Data.ShouldBeOfType<StockReservationFailed>();
        events[2].Data.ShouldBeOfType<OrderCancelled>().Reason.ShouldBe(CancelReason.StockUnavailable);
    }

    // Idempotency (at-least-once): a late StockReserved on an already-terminal order is ignored.
    [Fact]
    public async Task a_late_outcome_on_a_terminal_order_is_ignored()
    {
        await ResetOrdersAsync();
        var orderId = await SeedPlacedOrderAsync("customer-Z");

        // Cancel it first (stock failure), then a delayed grant crosses the broker.
        await _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.StockReservationFailed(orderId, "insufficient"));
        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The terminal stream is unchanged — the late grant appended nothing.
        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(3); // still OrderPlaced + StockReservationFailed + OrderCancelled

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Cancelled);
    }
}
