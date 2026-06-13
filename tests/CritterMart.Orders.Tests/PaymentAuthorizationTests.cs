using Contracts = CritterMart.Contracts;
using Alba;
using CritterMart.Orders.Order;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 4.3 (authorize payment) + 4.4 (confirm). When the stock gate clears, StockReservedHandler
// cascades AuthorizePayment in-process; the stubbed provider decides; PaymentDecisionHandler
// records the Klefter commit and, on approval, confirms. These tracked-session tests drive the
// whole in-process chain from a single Contracts.StockReserved against a seeded placed order — no
// broker (transports stubbed in the fixture), and local routing keeps the payment hops in-process.
[Collection("orders")]
[Trait("Category", "Integration")]
public class PaymentAuthorizationTests
{
    private static readonly OrderLine Plush = new("crit-001", 2, "Cosmic Critter Plush", 24.99m);
    private static readonly decimal Total = Plush.Quantity * Plush.Price;

    private readonly OrdersAppFixture _fixture;

    public PaymentAuthorizationTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    // Seed an order at the stock gate is unnecessary — we seed a freshly placed order and let the
    // inbound StockReserved drive it through the gate, exactly as Inventory's reply would.
    private static async Task<string> SeedPlacedOrderAsync(IDocumentStore store, string customerId)
    {
        var orderId = Guid.NewGuid().ToString();
        await using var session = store.LightweightSession();
        session.Events.StartStream<OrderStatusView>(
            orderId, new OrderPlaced(orderId, customerId, [Plush], Total));
        await session.SaveChangesAsync();
        return orderId;
    }

    // Happy path: stock reserved → AuthorizePayment cascaded → stub approves → PaymentAuthorized
    // (Klefter) → both gates closed → OrderConfirmed → CommitStock cascaded to Inventory.
    [Fact]
    public async Task an_approved_payment_authorizes_and_confirms_the_order()
    {
        await ResetOrdersAsync();
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        var orderId = await SeedPlacedOrderAsync(store, "customer-X");

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Confirmed);

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(4); // OrderPlaced + StockReserved + PaymentAuthorized + OrderConfirmed
        events[2].Data.ShouldBeOfType<PaymentAuthorized>();
        events[3].Data.ShouldBeOfType<OrderConfirmed>();

        var authorized = (PaymentAuthorized)events[2].Data;
        authorized.AuthCode.ShouldStartWith("stub-");
        authorized.Amount.ShouldBe(Total);

        // Slice 2.4: the confirm path now cascades CommitStock to Inventory (no local handler,
        // so it is routed out — the tracked session captures it with transports stubbed).
        var commit = tracked.Sent.SingleMessage<Contracts.CommitStock>();
        commit.OrderId.ShouldBe(orderId);
        commit.Lines.ShouldHaveSingleItem();
        commit.Lines[0].Sku.ShouldBe(Plush.Sku);
        commit.Lines[0].Quantity.ShouldBe(Plush.Quantity);
    }

    // Slice 4.6 decline-cancel: a declining provider records PaymentAuthFailed, then the aggregate
    // decision cancels the order (OrderCancelled { payment_declined }) and cascades a single
    // ReleaseStock carrying the order's lines back to Inventory. This supersedes slice 4.3's interim
    // non-terminal decline — the order now reaches its terminal cancelled state and gives stock back.
    [Fact]
    public async Task a_declined_payment_cancels_the_order_and_releases_the_reserved_stock()
    {
        await ResetOrdersAsync();

        // Swap in a declining provider — the chosen stub policy: no magic values in the domain
        // payload, just a different IPaymentProvider registration on a one-off host.
        await using var host = await AlbaHost.For<Program>(x => x.ConfigureServices(services =>
        {
            services.DisableAllExternalWolverineTransports();
            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<IPaymentProvider, DecliningPaymentProvider>();
        }));

        var store = host.Services.GetRequiredService<IDocumentStore>();
        var orderId = await SeedPlacedOrderAsync(store, "customer-Y");

        var tracked = await host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        // Exactly one cross-BC release is cascaded, carrying the order's line (ReleaseStock has no
        // local handler, so it is routed out — the tracked session captures it with transports stubbed).
        var release = tracked.Sent.SingleMessage<Contracts.ReleaseStock>();
        release.OrderId.ShouldBe(orderId);
        release.Lines.ShouldHaveSingleItem();
        release.Lines[0].Sku.ShouldBe(Plush.Sku);
        release.Lines[0].Quantity.ShouldBe(Plush.Quantity);

        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Cancelled); // terminal now, not stock_reserved

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(4); // OrderPlaced + StockReserved + PaymentAuthFailed + OrderCancelled
        events[2].Data.ShouldBeOfType<PaymentAuthFailed>().Reason.ShouldBe("declined");
        events[3].Data.ShouldBeOfType<OrderCancelled>().Reason.ShouldBe(CancelReason.PaymentDeclined);
    }

    // Idempotency: a duplicate/late StockReserved on an already-confirmed order re-triggers
    // nothing — StockReservedHandler's guard sees a non-awaiting status and cascades no payment.
    [Fact]
    public async Task a_duplicate_stock_reserved_does_not_re_run_payment()
    {
        await ResetOrdersAsync();
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        var orderId = await SeedPlacedOrderAsync(store, "customer-Z");

        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));
        await _fixture.Host.InvokeMessageAndWaitAsync(new Contracts.StockReserved(orderId));

        await using var session = store.LightweightSession();

        var events = await session.Events.FetchStreamAsync(orderId);
        events.Count.ShouldBe(4); // still OrderPlaced + StockReserved + PaymentAuthorized + OrderConfirmed

        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Confirmed);
    }
}

// Test double for the decline branch — the swappable seam the stub policy relies on.
public class DecliningPaymentProvider : IPaymentProvider
{
    public Task<PaymentDecision> AuthorizeAsync(AuthorizePayment command) =>
        Task.FromResult(new PaymentDecision(command.OrderId, Approved: false, AuthCode: null, Reason: "declined"));
}
