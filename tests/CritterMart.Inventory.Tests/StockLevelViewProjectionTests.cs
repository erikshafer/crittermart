using CritterMart.Inventory.Stock;
using Shouldly;
using Xunit;

namespace CritterMart.Inventory.Tests;

// Pure-function unit tests for the StockLevelView fold — no database, no mocks, no container.
// Untagged, so they run in the CI `Category!=Integration` job alongside the Orders fold tests.
public class StockLevelViewProjectionTests
{
    private readonly StockLevelViewProjection _projection = new();

    // Slice 2.3: StockReleased is the exact inverse of StockReserved — available rises back,
    // reserved falls back, and the order is dropped from the live reservations list. Dropping the
    // order id is what makes a duplicate release a no-op (the handler then finds no reservation).
    [Fact]
    public void stock_released_reverses_a_reservation()
    {
        var view = new StockLevelView { Id = "crit-001", Available = 98, Reserved = 2 };
        view.Reservations.Add("ord-C");

        _projection.Apply(new StockReleased("crit-001", "ord-C", 2), view);

        view.Available.ShouldBe(100);
        view.Reserved.ShouldBe(0);
        view.Reservations.ShouldNotContain("ord-C");
    }
}
