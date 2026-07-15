using System.Security.Cryptography;
using CritterMart.ServiceDefaults;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CritterMart.TestSupport;

// Mints JWTs exactly as Identity does — dev PRIVATE key, dev issuer/audience — so a resource-server host
// under test (falling back to the dev PUBLIC key) validates them offline (ADR 023). This is the shared
// test seam for the hard cutover: the `sub` claim is the sole customer trust boundary, so every scenario
// that used to send X-Customer-Id now sends `Authorization: Bearer {token}` minted here.
public static class JwtTestTokens
{
    // `signingKeyPem`/`issuer` are overridable so failure tests can produce a wrong-signature /
    // wrong-issuer token; `lifetime` may be negative to produce an expired one.
    public static string MintToken(
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

    // The common case, shaped for Alba: `.WithRequestHeader("Authorization", JwtTestTokens.Bearer(id))`.
    public static string Bearer(string customerId) =>
        $"Bearer {MintToken(customerId, TimeSpan.FromHours(1))}";
}
