namespace CritterMart.Catalog.Products;

// Audit-only lifecycle moment, appended to the per-product event stream.
// Not state-reconstruction material — the Product document remains source of truth.
// (Workshop 001 § 2, § 4 Catalog vocabulary.)
public record ProductPublished(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    string PublishedBy,
    DateTimeOffset PublishedAt);
