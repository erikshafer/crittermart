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

// Slices 2.5–2.7 wiring, end to end against the real Inventory host (Alba + Testcontainers Postgres, external
// transports stubbed). These assert the Replenishment saga document in Marten saga storage — which proves the
// saga rides Inventory's existing IntegrateWithWolverine() with no extra registration (design.md decision 8),
// and that a covering receipt deletes the saga (MarkCompleted). The saga-correlated message paths are driven
// with InvokeMessageAndWaitAsync (the repo idiom); one test wraps the HTTP receipt in TrackActivity to prove
// ReceiveStock publishes RestockArrived.
[Collection("inventory")]
[Trait("Category", "Integration")]
public class ReplenishmentSagaIntegrationTests
{
    private readonly InventoryAppFixture _fixture;

    public ReplenishmentSagaIntegrationTests(InventoryAppFixture fixture) => _fixture = fixture;

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

    private async Task<Replenishment?> LoadSagaAsync(string sku)
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        return await session.LoadAsync<Replenishment>(sku);
    }

    // Spec scenario "Open a replenishment saga on a shortfall": the slice-2.2 refusal is UNCHANGED and the
    // saga opens alongside it, recording the shortfall and firing the supplier-notification stub.
    [Fact]
    public async Task a_shortfall_opens_a_replenishment_saga_and_leaves_the_refusal_unchanged()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 1);

        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(Reserve("ord-B", ("crit-001", 2)));

        // The refusal path is untouched: the same StockReservationFailed still goes back to Orders.
        var failed = tracked.Sent.SingleMessage<Contracts.StockReservationFailed>();
        failed.OrderId.ShouldBe("ord-B");
        failed.Reason.ShouldBe("insufficient");

        // ...and the saga's supplier-notification stub fired for the 1-unit shortfall (2 requested, 1 available).
        tracked.Sent.SingleMessage<RequestRestock>().ShouldBe(new RequestRestock("crit-001", 1));

        // The Replenishment saga is open in saga storage with the outstanding shortfall.
        var saga = await LoadSagaAsync("crit-001");
        saga.ShouldNotBeNull();
        saga.Outstanding.ShouldBe(1);

        // No stream was modified (slice 2.2 invariant): only the one StockReceived from setup.
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        (await session.Events.FetchStreamAsync("crit-001")).Count.ShouldBe(1);
    }

    // Spec scenario "Restock that covers the shortfall completes the saga": driven end to end through the real
    // ReceiveStock HTTP endpoint (which publishes RestockArrived), wrapped in TrackActivity so the cascaded
    // saga handling completes before we assert. Proves both the publish wiring and MarkCompleted deletion.
    [Fact]
    public async Task a_covering_receipt_resolves_and_deletes_the_saga()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 1);
        await _fixture.Host.InvokeMessageAndWaitAsync(Reserve("ord-B", ("crit-001", 2)));
        (await LoadSagaAsync("crit-001")).ShouldNotBeNull(); // open with outstanding 1

        await _fixture.Host.TrackActivity().ExecuteAndWaitAsync(_ => ReceiveAsync("crit-001", 100));

        // The saga completed and was deleted from saga storage (MarkCompleted).
        (await LoadSagaAsync("crit-001")).ShouldBeNull();

        // The receipt's StockReceived is recorded exactly as slice 2.1 specifies (the saga adds no stream
        // event): 1 (setup) + 100 (restock) available.
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var view = await session.LoadAsync<StockLevelView>("crit-001");
        view!.Available.ShouldBe(101);
    }

    // Spec scenario "Partial restock reduces the shortfall and the saga stays open" — driven by delivering
    // RestockArrived directly to the open saga (deterministic), asserting the reduced-but-open state.
    [Fact]
    public async Task a_partial_restock_reduces_the_shortfall_and_keeps_the_saga_open()
    {
        await ResetInventoryAsync();
        await ReceiveAsync("crit-001", 2);
        await _fixture.Host.InvokeMessageAndWaitAsync(Reserve("ord-B", ("crit-001", 10))); // shortfall 8

        await _fixture.Host.InvokeMessageAndWaitAsync(new RestockArrived("crit-001", 3));

        var saga = await LoadSagaAsync("crit-001");
        saga.ShouldNotBeNull();
        saga.Outstanding.ShouldBe(5); // 8 - 3, still open
    }

    // Spec scenario "Restock for a SKU with no open saga is a no-op": the saga's NotFound(RestockArrived)
    // keeps Wolverine from throwing on the unmatched message; no saga is created.
    [Fact]
    public async Task a_restock_with_no_open_saga_is_a_silent_no_op()
    {
        await ResetInventoryAsync();

        await Should.NotThrowAsync(() =>
            _fixture.Host.InvokeMessageAndWaitAsync(new RestockArrived("crit-404", 50)));

        (await LoadSagaAsync("crit-404")).ShouldBeNull();
    }

    // Spec scenario "Timeout after the saga already resolved is a no-op": the saga's NotFound(ReplenishTimeout)
    // keeps a late-firing timeout silent, since the runtime has no scheduled-message cancellation.
    [Fact]
    public async Task a_timeout_for_an_absent_saga_is_a_silent_no_op()
    {
        await ResetInventoryAsync();

        await Should.NotThrowAsync(() =>
            _fixture.Host.InvokeMessageAndWaitAsync(new ReplenishTimeout("crit-405", TimeSpan.FromMinutes(2))));

        (await LoadSagaAsync("crit-405")).ShouldBeNull();
    }
}
