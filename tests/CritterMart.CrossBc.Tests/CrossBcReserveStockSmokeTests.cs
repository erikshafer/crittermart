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

// The slice-4.2 centerpiece, proven end-to-end: placing an order in Orders reserves stock in
// Inventory over the real RabbitMQ broker, and the grant returns as a Klefter StockReserved on
// the Order stream. One smoke — the tracked-session tests cover the branches; this proves the wire.
[Collection("crossbc")]
[Trait("Category", "Integration")]
public class CrossBcReserveStockSmokeTests
{
    private readonly CrossBcFixture _fixture;

    public CrossBcReserveStockSmokeTests(CrossBcFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task placing_an_order_reserves_stock_across_the_broker_and_records_the_klefter_commit()
    {
        // Inventory has stock for the SKU (posted as JSON — case-insensitive binding to ReceiveStock).
        await _fixture.InventoryHost.Scenario(_ =>
        {
            _.Post.Json(new { quantity = 100 }).ToUrl("/stock/crit-001/receipts");
            _.StatusCodeShouldBe(204);
        });

        // The customer has an open cart with that SKU.
        await _fixture.OrdersHost.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 2, new ProductSnapshot("Cosmic Critter Plush", 24.99m)))
                .ToUrl("/carts/customer-X/items");
            _.StatusCodeShouldBe(201);
        });

        // Place the order and wait for the full cross-broker round-trip to settle: ReserveStock
        // out to Inventory, StockReserved back to Orders, then the approve path continues through
        // CommitStock back to Inventory (slice 2.4). Wait for CommitStock — the chain's terminal
        // message — so no outbound messages leak into the next test's tracking scope.
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
                    _.Post.Json(new PlaceOrder("customer-X")).ToUrl("/orders");
                    _.StatusCodeShouldBe(201);
                });
                orderId = result.ReadAsJson<PlaceOrderResponse>()!.OrderId;
            }));

        // The Klefter StockReserved landed on the Order stream — the cross-broker grant was
        // recorded. (Since slice 4.3 the order then runs the in-process payment gate on to
        // confirmed; that chain is covered deterministically by PaymentAuthorizationTests. This
        // wire-smoke asserts only the cross-BC product: the grant is present on the stream.)
        var store = _fixture.OrdersHost.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var events = await session.Events.FetchStreamAsync(orderId);
        events.ShouldContain(e => e.Data is StockReserved);

        // Inventory reserved AND committed the stock (the approve path runs through to CommitStock).
        var inventoryStore = _fixture.InventoryHost.Services.GetRequiredService<IDocumentStore>();
        await using var inventorySession = inventoryStore.LightweightSession();
        var stock = await inventorySession.LoadAsync<InventoryApp::CritterMart.Inventory.Stock.StockLevelView>("crit-001");
        stock!.Available.ShouldBe(98);
        stock.Reserved.ShouldBe(0);
        stock.Committed.ShouldBe(2);
    }
}
