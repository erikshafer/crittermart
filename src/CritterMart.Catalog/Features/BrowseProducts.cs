using CritterMart.Catalog.Products;
using Marten;
using Wolverine.Http;

namespace CritterMart.Catalog.Features;

// Read-only browse of the published catalog (Workshop 001 slice 1.2).
// ProductCatalogView is a query over Product documents, not a Marten projection
// (slice 1.1 design.md Decision 1): query the documents and project each one, so
// the response surfaces `sku` rather than the raw document `id`. An empty catalog
// returns an empty list (200), not a 404.
public static class BrowseProductsEndpoint
{
    [WolverineGet("/products")]
    public static async Task<IReadOnlyList<ProductCatalogView>> Get(IQuerySession session)
    {
        var products = await session.Query<Product>().ToListAsync();
        return products.Select(ProductCatalogView.FromDocument).ToList();
    }
}
