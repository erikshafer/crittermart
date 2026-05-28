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
public class BrowseProductsTests
{
    private readonly CatalogAppFixture _fixture;

    public BrowseProductsTests(CatalogAppFixture fixture) => _fixture = fixture;

    private async Task ResetCatalogAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    private Task PublishAsync(PublishProduct command) =>
        _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(command).ToUrl("/products");
            _.StatusCodeShouldBe(201);
        });

    // Workshop 001 § 6.1 slice 1.2: browse returns all published products.
    [Fact]
    public async Task browsing_returns_all_published_products_with_their_details()
    {
        await ResetCatalogAsync();
        await PublishAsync(new PublishProduct("crit-001", "Cosmic Critter Plush", "A plush companion from beyond the stars.", 24.99m));
        await PublishAsync(new PublishProduct("crit-002", "Nebula Newt", "A glow-in-the-dark vinyl newt.", 18.00m));

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/products");
            _.StatusCodeShouldBe(200);
        });

        var products = result.ReadAsJson<List<ProductCatalogView>>();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);

        var plush = products.Single(p => p.Sku == "crit-001");
        plush.Name.ShouldBe("Cosmic Critter Plush");
        plush.Description.ShouldBe("A plush companion from beyond the stars.");
        plush.Price.ShouldBe(24.99m);

        var newt = products.Single(p => p.Sku == "crit-002");
        newt.Name.ShouldBe("Nebula Newt");
        newt.Price.ShouldBe(18.00m);
    }

    // An empty catalog browses to an empty list (200), not a 404.
    [Fact]
    public async Task browsing_an_empty_catalog_returns_an_empty_list()
    {
        await ResetCatalogAsync();

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/products");
            _.StatusCodeShouldBe(200);
        });

        result.ReadAsJson<List<ProductCatalogView>>().ShouldBeEmpty();
    }
}
