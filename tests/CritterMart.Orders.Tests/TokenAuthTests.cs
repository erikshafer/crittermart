using System.Security.Cryptography;
using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 5.10 — verify a token at a resource server (ADR 023). Orders validates Identity's JWT OFFLINE against
// the config-distributed public key (here the dev key) — signature/issuer/audience/lifetime — and sources the
// customer id from `sub`. Post-hard-cutover, `sub` is the SOLE trust boundary: the customer-keyed endpoints
// are [Authorize]'d, so a request with no valid token — including one waving the retired X-Customer-Id
// header — is rejected with 401 before any handler runs. Minting lives in the shared JwtTestTokens seam.
[Collection("orders")]
[Trait("Category", "Integration")]
public class TokenAuthTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);

    private readonly OrdersAppFixture _fixture;

    public TokenAuthTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    // Happy path: a valid Bearer token authenticates the request as its `sub`. The cart is created and
    // read purely off the token's claim, proving `sub` is the trust boundary.
    [Fact]
    public async Task a_valid_token_sources_the_customer_id_from_the_sub_claim()
    {
        await ResetOrdersAsync();
        var token = JwtTestTokens.MintToken("customer-tok", TimeSpan.FromHours(1));

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 2, CosmicCritterPlush)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.StatusCodeShouldBe(201);
        });

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.StatusCodeShouldBe(200);
        });

        var view = result.ReadAsJson<CartView>();
        view.ShouldNotBeNull();
        view.CustomerId.ShouldBe("customer-tok"); // sourced from `sub`, not any header
    }

    // Failure path — a token signed with the WRONG key fails the offline signature check → 401, decided
    // locally (no HTTP into Identity). A throwaway RSA key stands in for a forged/tampered token.
    [Fact]
    public async Task a_token_with_a_bad_signature_is_rejected_locally_with_401()
    {
        await ResetOrdersAsync();
        using var wrongKey = RSA.Create(2048);
        var forged = JwtTestTokens.MintToken(
            "customer-tok", TimeSpan.FromHours(1), wrongKey.ExportPkcs8PrivateKeyPem());

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", $"Bearer {forged}");
            _.StatusCodeShouldBe(401);
        });
    }

    // Failure path — an expired token fails the lifetime check → 401. Expired by an hour, well beyond
    // JwtBearer's default 5-minute clock skew.
    [Fact]
    public async Task an_expired_token_is_rejected_locally_with_401()
    {
        await ResetOrdersAsync();
        var expired = JwtTestTokens.MintToken("customer-tok", TimeSpan.FromHours(-1)); // expires before now

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", $"Bearer {expired}");
            _.StatusCodeShouldBe(401);
        });
    }

    // Hard cutover — a request with NO token at all is 401'd by [Authorize] before any handler runs.
    // (Pre-cutover this was the endpoints' "no identity → 400"; absent credentials are an AUTHENTICATION
    // failure now that the token is the only identity transport.)
    [Fact]
    public async Task a_request_with_no_token_is_rejected_with_401()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.StatusCodeShouldBe(401);
        });
    }

    // Hard cutover — the INVERSION of the layered cutover's fallback test: the retired X-Customer-Id
    // header must no longer resolve an identity. A header-only request is rejected with 401 and no cart
    // is created for the named customer — the header is dead as a trust boundary.
    [Fact]
    public async Task the_retired_x_customer_id_header_no_longer_resolves_identity()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, CosmicCritterPlush)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("X-Customer-Id", "customer-hdr");
            _.StatusCodeShouldBe(401);
        });

        // No Cart stream was started for the header-named customer — verified through the read model
        // with a VALID token for that same id.
        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-hdr"));
            _.StatusCodeShouldBe(404); // no open cart — the header-only POST appended nothing
        });
    }
}
