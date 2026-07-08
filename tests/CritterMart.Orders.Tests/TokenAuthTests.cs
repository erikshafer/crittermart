using System.Security.Cryptography;
using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using CritterMart.ServiceDefaults;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 5.10 — verify a token at a resource server (ADR 023). Orders validates Identity's JWT OFFLINE against
// the config-distributed public key (here the dev key) — signature/issuer/audience/lifetime — and sources the
// customer id from `sub`. The layered cutover keeps the X-Customer-Id header as a dev-only fallback.
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

    // Mint a token exactly as Identity does — dev PRIVATE key, dev issuer/audience — so the Orders host
    // (falling back to the dev PUBLIC key) validates it offline. `signingKeyPem`/`issuer` are overridable so
    // the failure tests can produce a wrong-signature / wrong-issuer token.
    private static string MintToken(
        string customerId, TimeSpan lifetime, string? signingKeyPem = null, string? issuer = null)
    {
        // NOT disposed: IdentityModel's CryptoProviderFactory caches a SignatureProvider keyed by the key
        // material and reuses it across calls with the same dev key — disposing this RSA (via `using`) would
        // leave that cached provider holding a disposed key, throwing ObjectDisposedException on the NEXT
        // mint with the same key. The production JwtTokenIssuer likewise holds its RSA for the process
        // lifetime. Test-lived instances are fine to let the GC reclaim.
        var rsa = RSA.Create();
        rsa.ImportFromPem(signingKeyPem ?? DevJwtDefaults.DevPrivateKeyPem);
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? DevJwtDefaults.Issuer,
            Audience = DevJwtDefaults.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            Claims = new Dictionary<string, object> { ["sub"] = customerId },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // Happy path: a valid Bearer token authenticates the request as its `sub`. No X-Customer-Id header is
    // sent — the cart is created and read purely off the token's claim, proving `sub` is the trust boundary.
    [Fact]
    public async Task a_valid_token_sources_the_customer_id_from_the_sub_claim()
    {
        await ResetOrdersAsync();
        var token = MintToken("customer-tok", TimeSpan.FromHours(1));

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
        var forged = MintToken("customer-tok", TimeSpan.FromHours(1), wrongKey.ExportPkcs8PrivateKeyPem());

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
        var expired = MintToken("customer-tok", TimeSpan.FromHours(-1)); // expires before now

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("Authorization", $"Bearer {expired}");
            _.StatusCodeShouldBe(401);
        });
    }

    // Layered cutover: with NO Bearer token, the dev-only X-Customer-Id header still resolves identity.
    // (This is the fallback the seeder, the existing tests, and demo-traffic depend on this PR.)
    [Fact]
    public async Task the_x_customer_id_fallback_still_resolves_identity()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart("crit-001", 1, CosmicCritterPlush)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("X-Customer-Id", "customer-hdr");
            _.StatusCodeShouldBe(201);
        });

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/mine");
            _.WithRequestHeader("X-Customer-Id", "customer-hdr");
            _.StatusCodeShouldBe(200);
        });

        result.ReadAsJson<CartView>()!.CustomerId.ShouldBe("customer-hdr");
    }
}
