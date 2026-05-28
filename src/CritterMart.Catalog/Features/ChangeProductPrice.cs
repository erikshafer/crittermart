using CritterMart.Catalog.Products;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Catalog.Features;

// The Seller changes a published product's price (Workshop 001 slice 1.3).
// SKU comes from the route; the new price from the body.
public record ChangeProductPrice(decimal NewPrice);

public static class ChangeProductPriceEndpoint
{
    [WolverinePost("/products/{sku}/price")]
    public static async Task<IResult> Post(string sku, ChangeProductPrice command, IDocumentSession session)
    {
        // Load the existing product (the old price is knowable only from the document).
        // An unknown SKU is a 404 — a defensive guard; the workshop's 1.3 GWT is happy-only.
        var product = await session.LoadAsync<Product>(sku);
        if (product is null)
        {
            return Results.Problem(
                title: "ProductNotFound",
                detail: $"No product with SKU '{sku}' exists in the catalog.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var oldPrice = product.Price;

        // Document store: the Product's current price is the source of truth.
        product.Price = command.NewPrice;
        session.Store(product);

        // Audit log: append ProductPriceChanged (old + new) to the existing per-product
        // stream (Append, not StartStream — the stream exists from ProductPublished).
        session.Events.Append(
            sku,
            new ProductPriceChanged(sku, oldPrice, command.NewPrice, SellerIdentity.DefaultSeller, DateTimeOffset.UtcNow));

        // AutoApplyTransactions commits the document update + event append in one transaction.
        return Results.Ok();
    }
}
