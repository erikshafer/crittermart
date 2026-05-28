namespace CritterMart.Catalog.Products;

// The Product document is the catalog's source of truth (Workshop 001 § 2).
// Identity IS the SKU: SKUs are immutable domain identifiers, so a natural key
// gives uniqueness for free and doubles as the audit stream key.
public class Product
{
    public string Id { get; set; } = string.Empty; // == SKU
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Read shape over Product documents — a query projection, not a Marten IProjection.
// (ADR 008's inline-projection rule is scoped to event-sourced aggregates; Catalog
// is a document store.)
public record ProductCatalogView(string Sku, string Name, string Description, decimal Price)
{
    public static ProductCatalogView FromDocument(Product product) =>
        new(product.Id, product.Name, product.Description, product.Price);
}
