namespace CritterMart.Contracts;

// Cross-BC command: Orders asks Inventory to release the stock it reserved for an order that is
// being cancelled (Workshop 001 slice 4.6 / 2.3, first reached on payment decline). The symmetric
// counterpart of ReserveStock — one message carries every line, and Inventory releases each line's
// reservation independently (per-SKU, not all-or-nothing: a line holding no reservation is a no-op).
//
// Deliberate divergence from the Workshop's wording (§ 2.3 / § 4.6 wrote this as a published
// OrderCancelled { orderId } event). Inventory needs the SKUs + quantities to release and stores no
// per-order line map, and keeping the wire language about *stock* (not *orders*) is the published-
// language / anti-corruption choice (ADR 014). See the change's design.md, Decision 1.
public record ReleaseStock(string OrderId, IReadOnlyList<ReleaseStockLine> Lines);

// A single line to release: how much of a SKU the order had reserved.
public record ReleaseStockLine(string Sku, int Quantity);
