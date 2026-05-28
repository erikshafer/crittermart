using Alba;
using CritterMart.Inventory.Features;
using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Inventory.Tests;

[Collection("inventory")]
public class ReceiveStockTests
{
    private readonly InventoryAppFixture _fixture;

    public ReceiveStockTests(InventoryAppFixture fixture) => _fixture = fixture;

    private async Task ResetInventoryAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    private Task ReceiveAsync(string sku, int quantity) =>
        _fixture.Host.Scenario(_ =>
        {
            // A void Wolverine.HTTP endpoint returns 204 No Content.
            _.Post.Json(new ReceiveStock(quantity)).ToUrl($"/stock/{sku}/receipts");
            _.StatusCodeShouldBe(204);
        });

    // Workshop 001 § 6.1 slice 2.1, happy path: receive stock for a new SKU.
    [Fact]
    public async Task receiving_stock_for_a_new_sku_records_it_and_projects_the_level()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The inline StockLevelView snapshot projects available from the event.
        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view.ShouldNotBeNull();
        view.Available.ShouldBe(100);
        view.Reserved.ShouldBe(0);

        // The Stock stream records one StockReceived event.
        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(1);
        var received = events[0].Data.ShouldBeOfType<StockReceived>();
        received.Quantity.ShouldBe(100);
    }

    // Workshop 001 § 6.1 slice 2.1, happy path: receive additional stock onto an existing SKU.
    [Fact]
    public async Task receiving_additional_stock_accumulates_available()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReceiveAsync("crit-001", 50);

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view.ShouldNotBeNull();
        view.Available.ShouldBe(150);
        view.Reserved.ShouldBe(0);

        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(2);
    }

    // The level is readable over HTTP.
    [Fact]
    public async Task stock_level_is_readable_over_http()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/stock/crit-001");
            _.StatusCodeShouldBe(200);
        });

        var view = result.ReadAsJson<StockLevelView>();
        view.ShouldNotBeNull();
        view.Available.ShouldBe(100);
    }
}
