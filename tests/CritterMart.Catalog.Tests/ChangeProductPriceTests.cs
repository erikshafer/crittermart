using Alba;
using CritterMart.Catalog.Features;
using CritterMart.Catalog.Products;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Catalog.Tests;

[Collection("catalog")]
public class ChangeProductPriceTests
{
    private readonly CatalogAppFixture _fixture;

    public ChangeProductPriceTests(CatalogAppFixture fixture) => _fixture = fixture;

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

    // Workshop 001 § 6.1 slice 1.3 happy path: change a published product's price.
    [Fact]
    public async Task changing_price_updates_the_document_and_records_old_and_new_on_the_stream()
    {
        await ResetCatalogAsync();
        await PublishAsync(new PublishProduct("crit-001", "Cosmic Critter Plush", "A plush companion from beyond the stars.", 24.99m));

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeProductPrice(19.99m)).ToUrl("/products/crit-001/price");
            _.StatusCodeShouldBe(200);
        });

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Document (source of truth) now reflects the new price.
        var product = await session.LoadAsync<Product>("crit-001");
        product.ShouldNotBeNull();
        product.Price.ShouldBe(19.99m);

        // The audit stream now holds two moments: ProductPublished then ProductPriceChanged.
        var events = await session.Events.FetchStreamAsync("crit-001");
        events.Count.ShouldBe(2);
        events[0].Data.ShouldBeOfType<ProductPublished>();
        var priceChanged = events[1].Data.ShouldBeOfType<ProductPriceChanged>();
        priceChanged.OldPrice.ShouldBe(24.99m);
        priceChanged.NewPrice.ShouldBe(19.99m);
    }

    // The catalog listing (slice 1.2) reflects the new price.
    [Fact]
    public async Task browsing_reflects_the_changed_price()
    {
        await ResetCatalogAsync();
        await PublishAsync(new PublishProduct("crit-001", "Cosmic Critter Plush", "A plush companion from beyond the stars.", 24.99m));

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeProductPrice(19.99m)).ToUrl("/products/crit-001/price");
            _.StatusCodeShouldBe(200);
        });

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/products");
            _.StatusCodeShouldBe(200);
        });

        var view = result.ReadAsJson<List<ProductCatalogView>>().Single(p => p.Sku == "crit-001");
        view.Price.ShouldBe(19.99m);
    }

    // Defensive guard (not a workshop scenario): changing the price of an unknown SKU is a 404.
    [Fact]
    public async Task changing_price_of_an_unknown_sku_is_rejected()
    {
        await ResetCatalogAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new ChangeProductPrice(9.99m)).ToUrl("/products/does-not-exist/price");
            _.StatusCodeShouldBe(404);
        });
    }
}
