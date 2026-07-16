using CritterMart.Orders.Ordering;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

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
        await _fixture.ResetAllDataAsync();
    }

    // Seed an order awaiting confirmation directly on its stream — the state Inventory replies into.
    private async Task<string> SeedPlacedOrderAsync(string customerId)
    {
        var orderId = Guid.NewGuid().ToString();
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        session.Events.StartStream<Order>(
            orderId, new OrderPlaced(orderId, customerId, [Plush], Plush.Quantity * Plush.Price));
        await session.SaveChangesAsync();
        return orderId;
    }

    // Klefter grant: the inbound StockReserved is recorded as an Order-stream StockReserved
    // commit. Since slice 4.3, a granted reservation also opens the payment gate, so stock_reserved
    // is now a transient step rather than the terminal — the fixture's approving stub carries the
    // order on to confirmed. This test pins only that the grant is recorded; the full payment
    // chain (PaymentAuthorized → OrderConfirmed) is covered by PaymentAuthorizationTests.
    [Fact]
    public async Task a_granted_reservation_is_recorded_as_a_klefter_commit()
    {
        await ResetOrdersAsync();
        var orderId = await SeedPlacedOrderAsync("customer-X");

        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var events = await session.Events.FetchStreamAsync(orderId);
        events[1].Data.ShouldBeOfType<StockReserved>(); // Inventory's grant, recorded on the Order stream
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
