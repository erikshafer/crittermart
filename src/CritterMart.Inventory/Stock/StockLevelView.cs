namespace CritterMart.Inventory.Stock;

// StockLevelView — a SKU's READ model (ADR 020): the public projection served over GET /stock/{sku}. A
// DEDICATED inline projection from the SKU's Stock events, decoupled from the StockLevel aggregate — the
// read path never touches the protected write model. Its shape currently mirrors the aggregate (a SKU's
// public level ≈ its decision state), but it is free to diverge — StockLevel can grow write-only decision
// fields without leaking them here, and this view could drop `reservations` (an internal guard list) from
// the wire without touching the write guard. The wire shape `{ id, available, reserved, committed,
// reservations }` is preserved EXACTLY so every reader of GET /stock/{sku} (the four handler tests, the
// CrossBc smoke tests, the demo) is unchanged.
//
// Self-aggregating inline snapshot, registered `Projections.Snapshot<StockLevelView>(SnapshotLifecycle.Inline)`.
// The fold mirrors the StockLevel aggregate's (same events), so read and write stay consistent. This replaced
// the former StockLevelViewProjection : SingleStreamProjection class in the ADR 020 rollout — the same
// arithmetic, now static methods on an immutable record, matching the Cart/Order pilot shape. StockReceived is
// both genesis (Create) and repeatable (Apply), exactly as on the aggregate.
public sealed record StockLevelView(
    string Id,
    int Available,
    int Reserved,
    int Committed,
    IReadOnlyList<string> Reservations)
{
    public static StockLevelView Create(StockReceived e) => new(e.Sku, e.Quantity, Reserved: 0, Committed: 0, Reservations: []);

    public static StockLevelView Apply(StockReceived e, StockLevelView view) =>
        view with { Available = view.Available + e.Quantity };

    public static StockLevelView Apply(StockReserved e, StockLevelView view) =>
        view with
        {
            Available = view.Available - e.Quantity,
            Reserved = view.Reserved + e.Quantity,
            Reservations = [.. view.Reservations, e.OrderId],
        };

    public static StockLevelView Apply(StockReleased e, StockLevelView view) =>
        view with
        {
            Available = view.Available + e.Quantity,
            Reserved = view.Reserved - e.Quantity,
            Reservations = [.. view.Reservations.Where(id => id != e.OrderId)],
        };

    public static StockLevelView Apply(StockCommitted e, StockLevelView view) =>
        view with
        {
            Reserved = view.Reserved - e.Quantity,
            Committed = view.Committed + e.Quantity,
            Reservations = [.. view.Reservations.Where(id => id != e.OrderId)],
        };
}
