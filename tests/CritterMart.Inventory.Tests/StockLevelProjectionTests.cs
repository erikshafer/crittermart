using CritterMart.Inventory.Stock;
using Shouldly;
using Xunit;

namespace CritterMart.Inventory.Tests;

// Pure-function unit tests for the StockLevel aggregate fold (ADR 020 — the domain WRITE model and the
// idempotency decision state the four stock handlers FetchForWriting and guard on) and the StockLevelView
// read model that folds the same events. Both are sealed, immutable records whose static Create/Apply methods
// ARE their self-aggregating snapshot projections; each Apply returns a NEW instance via `with`, so the fold
// is verified without a database, mocks, or a container. Untagged, so they run in the CI Category!=Integration
// job alongside the Orders fold tests.
//
// This is the ADR 020 Stock rollout: it replaced the former StockLevelViewProjection : SingleStreamProjection
// class (which mutated a shared StockLevelView instance) with these immutable-record folds. StockReceived is
// both genesis (Create) and repeatable (Apply) — the receipt that opens a SKU's stream vs. a later restock.
public class StockLevelProjectionTests
{
    // ── The StockLevel aggregate (write model / idempotency decision state) ──

    // StockReceived is genesis: the SKU's first receipt opens the level with the received quantity available,
    // nothing reserved or committed, and no live reservations. Id is the stream key (the SKU).
    [Fact]
    public void stock_received_opens_a_level_for_a_new_sku()
    {
        var level = StockLevel.Create(new StockReceived("crit-001", 100));

        level.Id.ShouldBe("crit-001");
        level.Available.ShouldBe(100);
        level.Reserved.ShouldBe(0);
        level.Committed.ShouldBe(0);
        level.Reservations.ShouldBeEmpty();
    }

    // A later receipt onto an existing SKU accumulates available — StockReceived's repeatable Apply path
    // (Marten dispatches Create on the genesis event, Apply on every subsequent one).
    [Fact]
    public void additional_stock_received_accumulates_available()
    {
        var level = StockLevel.Create(new StockReceived("crit-001", 100));

        level = StockLevel.Apply(new StockReceived("crit-001", 50), level);

        level.Available.ShouldBe(150);
    }

    // Slice 4.2: a reservation debits available, credits reserved, and records the order id so the handler's
    // guard finds it on a duplicate delivery and no-ops.
    [Fact]
    public void stock_reserved_moves_available_to_reserved_and_tracks_the_order()
    {
        var level = StockLevel.Create(new StockReceived("crit-001", 100));

        level = StockLevel.Apply(new StockReserved("crit-001", "ord-A", 2), level);

        level.Available.ShouldBe(98);
        level.Reserved.ShouldBe(2);
        level.Reservations.ShouldContain("ord-A");
    }

    // Slice 2.3: StockReleased is the exact inverse of StockReserved — available rises back, reserved falls
    // back, and the order is dropped from live reservations. Dropping the order id is what makes a duplicate
    // release a no-op (the handler then finds no reservation).
    [Fact]
    public void stock_released_reverses_a_reservation()
    {
        var level = StockLevel.Create(new StockReceived("crit-001", 100));
        level = StockLevel.Apply(new StockReserved("crit-001", "ord-C", 2), level);

        level = StockLevel.Apply(new StockReleased("crit-001", "ord-C", 2), level);

        level.Available.ShouldBe(100);
        level.Reserved.ShouldBe(0);
        level.Reservations.ShouldNotContain("ord-C");
    }

    // Slice 2.4: StockCommitted permanently consumes a reservation — reserved falls, committed rises, and the
    // order is dropped from live reservations. The invariant Available + Reserved + Committed = ΣStockReceived
    // holds after every fold.
    [Fact]
    public void stock_committed_converts_a_reservation_to_committed()
    {
        var level = StockLevel.Create(new StockReceived("crit-001", 100));
        level = StockLevel.Apply(new StockReserved("crit-001", "ord-A", 2), level);

        level = StockLevel.Apply(new StockCommitted("crit-001", "ord-A", 2), level);

        level.Available.ShouldBe(98);
        level.Reserved.ShouldBe(0);
        level.Committed.ShouldBe(2);
        level.Reservations.ShouldNotContain("ord-A");

        // Invariant: Available + Reserved + Committed = total received (100).
        (level.Available + level.Reserved + level.Committed).ShouldBe(100);
    }

    // ── The StockLevelView read model (ADR 020) — folds the same events, stays consistent with the aggregate ──

    // StockLevelView is decoupled from the StockLevel aggregate but folds the same genesis, so its public shape
    // (the GET /stock/{sku} wire) reflects the received stock.
    [Fact]
    public void stocklevelview_read_model_reflects_received_stock()
    {
        var view = StockLevelView.Create(new StockReceived("crit-001", 100));

        view.Id.ShouldBe("crit-001");
        view.Available.ShouldBe(100);
        view.Reserved.ShouldBe(0);
        view.Committed.ShouldBe(0);
    }

    // The read model walks the same reservation lifecycle as the aggregate — reserve then commit — and lands
    // on the same numbers, which is what keeps the served view consistent with the write-side guard.
    [Fact]
    public void stocklevelview_read_model_walks_the_reservation_lifecycle_consistently()
    {
        var view = StockLevelView.Create(new StockReceived("crit-001", 100));

        view = StockLevelView.Apply(new StockReserved("crit-001", "ord-A", 2), view);
        view.Available.ShouldBe(98);
        view.Reserved.ShouldBe(2);
        view.Reservations.ShouldContain("ord-A");

        view = StockLevelView.Apply(new StockCommitted("crit-001", "ord-A", 2), view);
        view.Reserved.ShouldBe(0);
        view.Committed.ShouldBe(2);
        view.Reservations.ShouldNotContain("ord-A");
    }
}
