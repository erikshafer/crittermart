using Alba;
using Xunit;

namespace CritterMart.Catalog.Tests;

// Per-service CORS assertion — the carry-forward from retro 011 ("assert the preflight against a known
// endpoint when a real origin is injected"). The fixture injects the storefront origin into
// Cors:AllowedOrigins exactly as the AppHost does in production (Cors__AllowedOrigins__0), so this
// proves the real config-driven allowlist, not the Development fallback. A cross-origin GET from the SPA
// origin comes back with Access-Control-Allow-Origin; a request from an unlisted origin does not. This
// is the allowlist a browser preflight checks — made assertable now that the no-BFF, three-service
// posture (ADR 018) has a real origin to allow.
[Collection("catalog")]
[Trait("Category", "Integration")]
public class CorsPolicyTests
{
    private readonly CatalogAppFixture _fixture;

    public CorsPolicyTests(CatalogAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task allows_the_storefront_origin()
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/products");
            _.WithRequestHeader("Origin", CatalogAppFixture.SpaOrigin);
            _.Header("Access-Control-Allow-Origin").SingleValueShouldEqual(CatalogAppFixture.SpaOrigin);
            _.IgnoreStatusCode();
        });
    }

    [Fact]
    public async Task does_not_allow_an_unlisted_origin()
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/products");
            _.WithRequestHeader("Origin", "http://malicious.example");
            _.Header("Access-Control-Allow-Origin").ShouldNotBeWritten();
            _.IgnoreStatusCode();
        });
    }
}
