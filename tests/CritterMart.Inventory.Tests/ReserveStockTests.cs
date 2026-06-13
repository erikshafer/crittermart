using Alba;
using CritterMart.Inventory.Features;
using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Inventory.Tests;

// Slice 4.2: reserving stock now arrives as a cross-BC ReserveStock message (the interim HTTP
// route is gone). These are Wolverine tracked-session tests — InvokeMessageAndWaitAsync drives
// the handler and the tracked session captures the cascaded reply, with no real broker.
[Collection("inventory")]
[Trait("Category", "Integration")]
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

    private static Contracts.ReserveStock Reserve(string orderId, params (string Sku, int Qty)[] lines) =>
        new(orderId, [.. lines.Select(l => new Contracts.ReserveStockLine(l.Sku, l.Qty))]);

    // Workshop 001 § 6.1 slice 4.2 / spec: reserve every line atomically, publish StockReserved back.
    [Fact]
    public async Task reserving_every_available_line_reserves_atomically_and_publishes_back()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReceiveAsync("crit-002", 50);

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(
            Reserve("ord-A", ("crit-001", 2), ("crit-002", 3)));

        // The granted outcome is cascaded back to Orders.
        tracked.Sent.SingleMessage<Contracts.StockReserved>().OrderId.ShouldBe("ord-A");

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var crit001 = await session.LoadAsync<StockLevelView>("crit-001");
        crit001.ShouldNotBeNull();
        crit001.Available.ShouldBe(98);
        crit001.Reserved.ShouldBe(2);

        var crit002 = await session.LoadAsync<StockLevelView>("crit-002");
        crit002.ShouldNotBeNull();
        crit002.Available.ShouldBe(47);
        crit002.Reserved.ShouldBe(3);
    }

    // Spec: any short line refuses the WHOLE order — no stream modified, StockReservationFailed back.
    [Fact]
    public async Task a_short_line_refuses_the_whole_order_and_publishes_failure()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReceiveAsync("crit-002", 1);

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(
            Reserve("ord-B", ("crit-001", 2), ("crit-002", 3)));

        var failed = tracked.Sent.SingleMessage<Contracts.StockReservationFailed>();
        failed.OrderId.ShouldBe("ord-B");
        failed.Reason.ShouldBe("insufficient");

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Neither SKU was touched — the available line was not reserved because the other was short.
        var crit001 = await session.LoadAsync<StockLevelView>("crit-001");
        crit001!.Available.ShouldBe(100);
        crit001.Reserved.ShouldBe(0);
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(1);

        var crit002 = await session.LoadAsync<StockLevelView>("crit-002");
        crit002!.Available.ShouldBe(1);
        crit002.Reserved.ShouldBe(0);
        (await session.Events.FetchStreamAsync("crit-002")).Count.ShouldBe(1);
    }

    // Spec: duplicate delivery for an already-reserved order does not double-reserve (idempotent).
    [Fact]
    public async Task a_duplicate_reserve_for_the_same_order_does_not_double_reserve()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);

        await _fixture.Host.InvokeMessageAndWaitAsync(Reserve("ord-A", ("crit-001", 2)));
        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(Reserve("ord-A", ("crit-001", 2)));

        // The duplicate re-publishes the granted outcome (Orders is itself idempotent)...
        tracked.Sent.SingleMessage<Contracts.StockReserved>().OrderId.ShouldBe("ord-A");

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // ...but the level is unchanged and only ONE StockReserved was ever appended.
        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view!.Available.ShouldBe(98);
        view.Reserved.ShouldBe(2);
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(2); // StockReceived + 1 StockReserved
    }
}
