using CritterMart.Inventory.Features;
using CritterMart.Inventory.Stock;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Inventory.Tests;

// Slice 2.4: committing reserved stock arrives as a cross-BC CommitStock message (reached when
// Orders confirms an order). The mirror of ReleaseStockTests — same handler shape, same
// idempotency guards, but reserved stock moves to committed instead of back to available.
[Collection("inventory")]
[Trait("Category", "Integration")]
public class CommitStockTests
{
    private readonly InventoryAppFixture _fixture;

    public CommitStockTests(InventoryAppFixture fixture) => _fixture = fixture;

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

    private Task ReserveAsync(string orderId, params (string Sku, int Qty)[] lines) =>
        _fixture.Host.InvokeMessageAndWaitAsync(
            new Contracts.ReserveStock(orderId, [.. lines.Select(l => new Contracts.ReserveStockLine(l.Sku, l.Qty))]));

    private static Contracts.CommitStock Commit(string orderId, params (string Sku, int Qty)[] lines) =>
        new(orderId, [.. lines.Select(l => new Contracts.CommitStockLine(l.Sku, l.Qty))]);

    [Fact]
    public async Task committing_a_held_reservation_converts_reserved_to_committed()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReserveAsync("ord-A", ("crit-001", 2));

        await _fixture.Host.InvokeMessageAndWaitAsync(Commit("ord-A", ("crit-001", 2)));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view.ShouldNotBeNull();
        view.Available.ShouldBe(98);
        view.Reserved.ShouldBe(0);
        view.Committed.ShouldBe(2);
        view.Reservations.ShouldNotContain("ord-A");

        // StockReceived + StockReserved + StockCommitted.
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(3);
    }

    [Fact]
    public async Task a_duplicate_commit_is_a_no_op()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReserveAsync("ord-A", ("crit-001", 2));

        await _fixture.Host.InvokeMessageAndWaitAsync(Commit("ord-A", ("crit-001", 2)));
        await _fixture.Host.InvokeMessageAndWaitAsync(Commit("ord-A", ("crit-001", 2)));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view!.Available.ShouldBe(98);
        view.Reserved.ShouldBe(0);
        view.Committed.ShouldBe(2);

        // Still only ONE StockCommitted — the duplicate appended nothing.
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(3);
    }

    [Fact]
    public async Task committing_an_order_with_no_reservation_on_the_sku_is_a_no_op()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReserveAsync("ord-A", ("crit-001", 2));

        // A different order (ord-B) never reserved crit-001 — committing it must change nothing.
        await _fixture.Host.InvokeMessageAndWaitAsync(Commit("ord-B", ("crit-001", 2)));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view!.Available.ShouldBe(98);
        view.Reserved.ShouldBe(2);
        view.Committed.ShouldBe(0);
        view.Reservations.ShouldContain("ord-A");

        // StockReceived + StockReserved only — no StockCommitted for the non-holding order.
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(2);
    }
}
