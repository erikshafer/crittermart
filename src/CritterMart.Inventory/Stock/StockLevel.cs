namespace CritterMart.Inventory.Stock;

// The StockLevel aggregate — a SKU's domain WRITE model (ADR 020). `sealed` and immutable: every
// Apply returns a NEW StockLevel via `with`, never mutates in place. It is the FetchForWriting target
// on all four stock write paths (ReceiveStock, ReserveStock, ReleaseStock, CommitStock) and is NEVER
// serialized over HTTP — GET /stock/{sku} binds the separate StockLevelView read model
// (StockLevelView.cs), never this type. Id is the stream key (the SKU).
//
// It stays in the `Stock/` folder with no rename: the ADR 021 verb-folder convention (which moved Cart to
// Shopping/ and Order to Ordering/) fires only where a singular noun folder would collide with its
// aggregate type. `StockLevel` ≠ `…Stock`, so there is no `CritterMart.Inventory.Stock` ↔ `StockLevel`
// collision and no rename is owed — Stock is the smallest of the three ADR 020 rollouts.
//
// Self-aggregating inline snapshot (ADR 008, refined by ADR 020): the static Create/Apply methods ARE the
// projection, registered via `Projections.Snapshot<StockLevel>(SnapshotLifecycle.Inline)`, so the aggregate
// is materialized in the same transaction as the event append. The fold is a pure function of the stream —
// unit-tested without a database (StockLevelProjectionTests). The folds take plain events — the level tracks
// no activity timestamp (unlike Cart), so no IEvent<T> wrapper is needed.
//
// Reservations is the load-bearing field (the analogue of Order.Status): it lists the order ids holding a
// live reservation on this SKU, and the ReserveStock / ReleaseStock / CommitStock handlers FetchForWriting
// and read it to stay idempotent under at-least-once delivery — reserve no-ops if the order is already
// present, release/commit no-op per SKU if it is absent. The read model can later drop or rename it without
// touching this write-side guard, which is the point of the split.
public sealed record StockLevel(
    string Id,
    int Available,
    int Reserved,
    int Committed,
    IReadOnlyList<string> Reservations)
{
    // Genesis: a SKU's first receipt starts the stream (ReceiveStock does FetchForWriting + AppendOne, which
    // creates the stream when the SKU is new). The level opens with the received quantity available.
    public static StockLevel Create(StockReceived e) => new(e.Sku, e.Quantity, Reserved: 0, Committed: 0, Reservations: []);

    // A later receipt onto an existing SKU accumulates available. StockReceived is the rare event that is BOTH
    // genesis (Create) and repeatable (Apply) — Marten dispatches Create when the snapshot is absent and Apply
    // when it exists. The old mutable projection had only one Apply<StockReceived> and leaned on Marten
    // default-constructing the empty class; an immutable record must split the two paths.
    public static StockLevel Apply(StockReceived e, StockLevel level) =>
        level with { Available = level.Available + e.Quantity };

    // Slice 4.2: a reservation moves stock from available to reserved and records the order id so a duplicate
    // delivery for the same order is a no-op (the handler's guard finds the order already present).
    public static StockLevel Apply(StockReserved e, StockLevel level) =>
        level with
        {
            Available = level.Available - e.Quantity,
            Reserved = level.Reserved + e.Quantity,
            Reservations = [.. level.Reservations, e.OrderId],
        };

    // Slice 2.3: the exact inverse — the reservation is given back to the pool and the order dropped from the
    // live reservations. Dropping the order id is what makes a duplicate release a no-op (the handler then
    // finds no reservation for the order and appends nothing).
    public static StockLevel Apply(StockReleased e, StockLevel level) =>
        level with
        {
            Available = level.Available + e.Quantity,
            Reserved = level.Reserved - e.Quantity,
            Reservations = [.. level.Reservations.Where(id => id != e.OrderId)],
        };

    // Slice 2.4: a reservation permanently consumed — reserved falls, committed rises, the order is dropped
    // from live reservations. The invariant Available + Reserved + Committed = ΣStockReceived holds after
    // every fold.
    public static StockLevel Apply(StockCommitted e, StockLevel level) =>
        level with
        {
            Reserved = level.Reserved - e.Quantity,
            Committed = level.Committed + e.Quantity,
            Reservations = [.. level.Reservations.Where(id => id != e.OrderId)],
        };
}
