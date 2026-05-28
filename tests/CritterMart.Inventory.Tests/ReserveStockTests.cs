using Alba;
using CritterMart.Inventory.Features;
using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Inventory.Tests;

[Collection("inventory")]
public class ReserveStockTests
{
    private readonly InventoryAppFixture _fixture;

    public ReserveStockTests(InventoryAppFixture fixture) => _fixture = fixture;

    private async Task ResetInventoryAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    private Task ReceiveAsync(string sku, int quantity) =>
        _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ReceiveStock(quantity)).ToUrl($"/stock/{sku}/receipts");
            _.StatusCodeShouldBe(204);
        });

    // Workshop 001 § 6.1 slice 2.2 happy path: reserve available stock.
    [Fact]
    public async Task reserving_available_stock_appends_the_event_and_adjusts_the_level()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ReserveStock("ord-A", 2)).ToUrl("/stock/crit-001/reservations");
            _.StatusCodeShouldBe(204);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view.ShouldNotBeNull();
        view.Available.ShouldBe(98);
        view.Reserved.ShouldBe(2);

        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(2);
        events[0].Data.ShouldBeOfType<StockReceived>();
        var reserved = events[1].Data.ShouldBeOfType<StockReserved>();
        reserved.OrderId.ShouldBe("ord-A");
        reserved.Quantity.ShouldBe(2);
    }

    // Workshop 001 § 6.1 slice 2.2 failure path: insufficient stock is refused, stream unchanged.
    [Fact]
    public async Task reserving_more_than_available_is_refused_and_leaves_the_stream_unchanged()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 1);

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ReserveStock("ord-B", 2)).ToUrl("/stock/crit-001/reservations");
            _.StatusCodeShouldBe(409);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The view is unchanged (still 1 available, 0 reserved).
        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view.ShouldNotBeNull();
        view.Available.ShouldBe(1);
        view.Reserved.ShouldBe(0);

        // No StockReserved was appended — only the original StockReceived.
        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(1);
        events[0].Data.ShouldBeOfType<StockReceived>();
    }
}
