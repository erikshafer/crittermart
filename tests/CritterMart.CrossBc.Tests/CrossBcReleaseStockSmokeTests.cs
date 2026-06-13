extern alias InventoryApp;
using Alba;
using CritterMart.Orders.Cart;
using CritterMart.Orders.Features;
using CritterMart.Orders.Order;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

namespace CritterMart.CrossBc.Tests;

// Slice 4.6 + 2.3, proven end-to-end over the real broker: a declined payment cancels the order in
// Orders AND releases the reserved stock back in Inventory — the project's first cancellation that
// crosses a BC boundary *back*. The decline is driven by flipping the shared configurable stub off
// (the crossbc collection is serial, so this does not race the reserve smoke). One smoke — the
// tracked-session tests cover the branches; this proves the ReleaseStock wire round-trips.
[Collection("crossbc")]
[Trait("Category", "Integration")]
public class CrossBcReleaseStockSmokeTests
{
    private readonly CrossBcFixture _fixture;

    public CrossBcReleaseStockSmokeTests(CrossBcFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task a_declined_payment_cancels_the_order_and_releases_stock_across_the_broker()
    {
        // A distinct SKU/customer so this smoke is independent of the reserve smoke's crit-001 state.
        await _fixture.InventoryHost.Scenario(_ =>
        {
            _.Post.Json(new { quantity = 50 }).ToUrl("/stock/crit-009/receipts");
            _.StatusCodeShouldBe(204);
        });

        await _fixture.OrdersHost.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-009", 2, new ProductSnapshot("Wandering Wombat", 30.00m)))
                .ToUrl("/carts/decline-customer/items");
            _.StatusCodeShouldBe(201);
        });

        // Force the decline for this order, then restore the default so the reserve smoke (whichever
        // order the collection runs them in) still sees an approving provider.
        var provider = _fixture.OrdersHost.Services.GetRequiredService<ConfigurableStubPaymentProvider>();
        provider.ShouldApprove = false;

        var orderId = string.Empty;
        try
        {
            // Wait for the full cross-broker round-trip to settle: ReserveStock → StockReserved back →
            // payment declined → OrderCancelled → ReleaseStock out → Inventory releases the reservation.
            await _fixture.OrdersHost.TrackActivity()
                .AlsoTrack(_fixture.InventoryHost)
                .IncludeExternalTransports()
                .Timeout(TimeSpan.FromSeconds(60))
                .WaitForMessageToBeReceivedAt<Contracts.ReleaseStock>(_fixture.InventoryHost)
                .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async context =>
                {
                    var result = await _fixture.OrdersHost.Scenario(_ =>
                    {
                        _.Post.Json(new PlaceOrder("decline-customer")).ToUrl("/orders");
                        _.StatusCodeShouldBe(201);
                    });
                    orderId = result.ReadAsJson<PlaceOrderResponse>()!.OrderId;
                }));
        }
        finally
        {
            provider.ShouldApprove = true;
        }

        // The Order stream reached its terminal cancellation (payment_declined).
        var store = _fixture.OrdersHost.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Cancelled);
        var events = await session.Events.FetchStreamAsync(orderId);
        var cancelled = events.Select(e => e.Data).OfType<OrderCancelled>().ShouldHaveSingleItem();
        cancelled.Reason.ShouldBe(CancelReason.PaymentDeclined);

        // And Inventory really released the reservation: the SKU is back to its full available level.
        var inventoryStore = _fixture.InventoryHost.Services.GetRequiredService<IDocumentStore>();
        await using var inventorySession = inventoryStore.LightweightSession();
        var stock = await inventorySession.LoadAsync<InventoryApp::CritterMart.Inventory.Stock.StockLevelView>("crit-009");
        stock!.Available.ShouldBe(50);
        stock.Reserved.ShouldBe(0);
    }
}
