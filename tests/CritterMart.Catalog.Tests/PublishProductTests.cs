using Alba;
using CritterMart.Catalog.Features;
using CritterMart.Catalog.Products;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Catalog.Tests;

[Collection("catalog")]
[Trait("Category", "Integration")]
public class PublishProductTests
{
    private readonly CatalogAppFixture _fixture;

    public PublishProductTests(CatalogAppFixture fixture) => _fixture = fixture;

    private async Task ResetCatalogAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    // Workshop 001 § 6.1 happy path: publish a new product.
    [Fact]
    public async Task publishing_a_new_product_records_it_and_surfaces_it_in_the_catalog()
    {
        await ResetCatalogAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post
                .Json(new PublishProduct("crit-001", "Cosmic Critter Plush", "A plush companion from beyond the stars.", 24.99m))
                .ToUrl("/products");
            _.StatusCodeShouldBe(201);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The Product document is the source of truth; the catalog read shape sees it.
        var product = await session.LoadAsync<Product>("crit-001");
        product.ShouldNotBeNull();
        var view = ProductCatalogView.FromDocument(product);
        view.Name.ShouldBe("Cosmic Critter Plush");
        view.Price.ShouldBe(24.99m);

        // Exactly one ProductPublished lifecycle moment on the per-product stream.
        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(1);
        events[0].Data.ShouldBeOfType<ProductPublished>();
    }

    // Workshop 001 § 6.1 failure path: duplicate SKU is rejected, idempotently.
    [Fact]
    public async Task publishing_a_duplicate_sku_is_rejected_without_a_shadow_event()
    {
        await ResetCatalogAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post
                .Json(new PublishProduct("crit-001", "Cosmic Critter Plush", "A plush companion from beyond the stars.", 24.99m))
                .ToUrl("/products");
            _.StatusCodeShouldBe(201);
        });

        await _fixture.Host.Scenario(_ =>
        {
            _.Post
                .Json(new PublishProduct("crit-001", "Cosmic Critter Plush (relisted)", "Fresh start.", 19.99m))
                .ToUrl("/products");
            _.StatusCodeShouldBe(409);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Existing document is untouched: the rejected publish overwrote nothing.
        var product = await session.LoadAsync<Product>("crit-001");
        product.ShouldNotBeNull();
        product.Name.ShouldBe("Cosmic Critter Plush");
        product.Price.ShouldBe(24.99m);

        // No shadow lifecycle moment: still exactly one ProductPublished on the stream.
        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(1);
    }
}
