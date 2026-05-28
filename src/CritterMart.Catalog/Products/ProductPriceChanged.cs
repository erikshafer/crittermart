namespace CritterMart.Catalog.Products;

// Audit-only lifecycle moment: the second event kind on a product's stream
// (after ProductPublished). Carries the old and new price so the audit trail
// preserves how the price evolved; the Product document remains source of truth.
// (Workshop 001 § 4 Catalog vocabulary, slice 1.3.)
public record ProductPriceChanged(
    string Sku,
    decimal OldPrice,
    decimal NewPrice,
    string ChangedBy,
    DateTimeOffset ChangedAt);
