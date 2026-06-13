namespace CritterMart.Contracts;

// Cross-BC command: Orders asks Inventory to commit the stock reserved for a confirmed order
// (Workshop 001 slice 2.4). The mirror of ReleaseStock — one message carries every line, and
// Inventory commits each line's reservation independently (per-SKU, not all-or-nothing: a line
// holding no reservation is a no-op). Same published-language / anti-corruption shape as
// ReserveStock and ReleaseStock (ADR 014).
public record CommitStock(string OrderId, IReadOnlyList<CommitStockLine> Lines);

public record CommitStockLine(string Sku, int Quantity);
