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

// Slice 2.3: releasing reserved stock arrives as a cross-BC ReleaseStock message (first reached
// when Orders cancels on payment decline, slice 4.6). The compensating counterpart of ReserveStock,
// and a one-way hop — no reply is published. Tracked-session tests drive the handler with no real
// broker; reservations are seeded through the real ReserveStock handler so the round-trip is exercised.
[Collection("inventory")]
[Trait("Category", "Integration")]
public class ReleaseStockTests
{
    private readonly InventoryAppFixture _fixture;

    public ReleaseStockTests(InventoryAppFixture fixture) => _fixture = fixture;

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

    private static Contracts.ReleaseStock Release(string orderId, params (string Sku, int Qty)[] lines) =>
        new(orderId, [.. lines.Select(l => new Contracts.ReleaseStockLine(l.Sku, l.Qty))]);

    // Spec happy path: a held reservation is released — available rises back, reserved falls, and
    // the order is dropped from the SKU's reservations. One StockReleased appended (after the grant).
    [Fact]
    public async Task releasing_a_held_reservation_restores_the_level()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReserveAsync("ord-C", ("crit-001", 2));

        await _fixture.Host.InvokeMessageAndWaitAsync(Release("ord-C", ("crit-001", 2)));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view.ShouldNotBeNull();
        view.Available.ShouldBe(100);
        view.Reserved.ShouldBe(0);
        view.Reservations.ShouldNotContain("ord-C");

        // StockReceived + StockReserved + StockReleased.
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(3);
    }

    // Spec idempotency: a duplicate ReleaseStock for an already-released order is a no-op. The fold
    // dropped the order from Reservations on the first release, so the guard finds nothing the second
    // time and appends no event — correct under at-least-once delivery.
    [Fact]
    public async Task a_duplicate_release_is_a_no_op()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReserveAsync("ord-C", ("crit-001", 2));

        await _fixture.Host.InvokeMessageAndWaitAsync(Release("ord-C", ("crit-001", 2)));
        await _fixture.Host.InvokeMessageAndWaitAsync(Release("ord-C", ("crit-001", 2)));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view!.Available.ShouldBe(100);
        view.Reserved.ShouldBe(0);

        // Still only ONE StockReleased — the duplicate appended nothing.
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(3);
    }

    // Spec no-op + reordering safety: release is keyed on the SKU actually holding a reservation for
    // THIS order, not on event order. A release for an order with no reservation on the SKU (a late
    // or never-granted order) leaves the holding order's reservation untouched. This is the guarantee
    // the delayed-StockReserved cross-BC race relies on (Workshop § 4.7), pinned here for 4.7's reuse.
    [Fact]
    public async Task releasing_an_order_with_no_reservation_on_the_sku_is_a_no_op()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 100);
        await ReserveAsync("ord-A", ("crit-001", 2));

        // A different order (ord-C) never reserved crit-001 — releasing it must change nothing.
        await _fixture.Host.InvokeMessageAndWaitAsync(Release("ord-C", ("crit-001", 2)));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view!.Available.ShouldBe(98); // ord-A's reservation is intact
        view.Reserved.ShouldBe(2);
        view.Reservations.ShouldContain("ord-A");

        // StockReceived + StockReserved only — no StockReleased for the non-holding order.
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(2);
    }
}
