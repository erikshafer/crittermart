using CritterMart.Catalog.Products;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace CritterMart.Catalog.Features;

// The Seller publishes a product onto the storefront catalog (Workshop 001 slice 1.1).
public record PublishProduct(string Sku, string Name, string Description, decimal Price);

// Identity is stubbed for round one (ADR 009): a single-seller storefront, so the
// acting seller is supplied here as if it came from a real identity system.
public static class SellerIdentity
{
    public const string DefaultSeller = "seller-001";
}

public static class PublishProductEndpoint
{
    // Railway-style guard: product SKUs are unique. A duplicate is an expected,
    // modeled outcome — return ProblemDetails (flow control), never throw. Because
    // this short-circuits, the duplicate path stores no document and appends no
    // event, so the failure is idempotent.
    public static async Task<ProblemDetails> ValidateAsync(PublishProduct command, IDocumentSession session)
    {
        var alreadyPublished = await session.CheckExistsAsync<Product>(command.Sku);

        return alreadyPublished
            ? new ProblemDetails
            {
                Title = "ProductAlreadyPublished",
                Detail = $"A product with SKU '{command.Sku}' is already published.",
                Status = StatusCodes.Status409Conflict
            }
            : WolverineContinue.NoProblems;
    }

    [WolverinePost("/products")]
    public static CreationResponse Post(PublishProduct command, IDocumentSession session)
    {
        // Document store: the Product document is the source of truth.
        var product = new Product
        {
            Id = command.Sku,
            Name = command.Name,
            Description = command.Description,
            Price = command.Price
        };
        session.Store(product);

        // Audit log: append ProductPublished to the per-product stream (keyed by SKU).
        session.Events.StartStream(
            command.Sku,
            new ProductPublished(
                command.Sku,
                command.Name,
                command.Description,
                command.Price,
                SellerIdentity.DefaultSeller,
                DateTimeOffset.UtcNow));

        // AutoApplyTransactions commits the document + event in one transaction.
        return new CreationResponse($"/products/{command.Sku}");
    }
}
