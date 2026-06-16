extern alias InventoryApp;
using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

namespace CritterMart.CrossBc.Tests;

// Slice 2.4, proven end-to-end: an approved payment confirms the order in Orders AND commits the
// reserved stock in Inventory over the real broker. The mirror of CrossBcReleaseStockSmokeTests —
// the approve path's Inventory consequence, completing the stock lifecycle.
[Collection("crossbc")]
[Trait("Category", "Integration")]
public class CrossBcCommitStockSmokeTests
{
    private readonly CrossBcFixture _fixture;

    public CrossBcCommitStockSmokeTests(CrossBcFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task an_approved_payment_confirms_the_order_and_commits_stock_across_the_broker()
    {
        // A distinct SKU/customer so this smoke is independent of the other smokes' state.
        await _fixture.InventoryHost.Scenario(_ =>
        {
            _.Post.Json(new { quantity = 50 }).ToUrl("/stock/crit-020/receipts");
            _.StatusCodeShouldBe(204);
        });

        await _fixture.OrdersHost.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-020", 3, new ProductSnapshot("Galactic Gecko", 15.00m)))
                .ToUrl("/carts/commit-customer/items");
            _.StatusCodeShouldBe(201);
        });

        // The default provider approves — no stub swap needed.
        var orderId = string.Empty;
        await _fixture.OrdersHost.TrackActivity()
            .AlsoTrack(_fixture.InventoryHost)
            .IncludeExternalTransports()
            .Timeout(TimeSpan.FromSeconds(60))
            .WaitForMessageToBeReceivedAt<Contracts.CommitStock>(_fixture.InventoryHost)
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async context =>
            {
                var result = await _fixture.OrdersHost.Scenario(_ =>
                {
                    _.Post.Json(new PlaceOrder("commit-customer")).ToUrl("/orders");
                    _.StatusCodeShouldBe(201);
                });
                orderId = result.ReadAsJson<PlaceOrderResponse>()!.OrderId;
            }));

        // The Order reached its terminal confirmation.
        var store = _fixture.OrdersHost.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var view = await session.LoadAsync<OrderStatusView>(orderId);
        view!.Status.ShouldBe(OrderStatus.Confirmed);

        // And Inventory committed the stock: reserved is back to 0, committed equals the order qty.
        var inventoryStore = _fixture.InventoryHost.Services.GetRequiredService<IDocumentStore>();
        await using var inventorySession = inventoryStore.LightweightSession();
        var stock = await inventorySession.LoadAsync<InventoryApp::CritterMart.Inventory.Stock.StockLevelView>("crit-020");
        stock.ShouldNotBeNull();
        stock.Available.ShouldBe(47);
        stock.Reserved.ShouldBe(0);
        stock.Committed.ShouldBe(3);
        stock.Reservations.ShouldNotContain(orderId);
    }
}
