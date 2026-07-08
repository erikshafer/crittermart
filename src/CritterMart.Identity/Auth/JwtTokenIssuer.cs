using System.Security.Cryptography;
using CritterMart.ServiceDefaults;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CritterMart.Identity.Auth;

// Mints the self-validated JWT (ADR 023, slice 5.9). Identity is the system's SOLE issuer: it alone holds
// the RSA PRIVATE key and can sign; the resource servers hold only the public key and can only verify
// offline. This is the "issuer" half of the issuer/resource-server split.
//
// Uses the MODERN Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler, deliberately NOT the legacy
// System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler — the legacy handler silently remaps the `sub`
// claim to ClaimTypes.NameIdentifier on the way out, which (paired with inbound claim-mapping on the
// resource server) turns "read `sub`" into a guessing game. JsonWebTokenHandler + a raw `sub` in the claims
// dictionary keeps `sub` readable as `sub` end-to-end. The resource server pairs this with
// options.MapInboundClaims = false (see Orders' AddJwtBearer).
//
// Registered as a singleton: the RSA key + SigningCredentials are built once at startup from `Jwt:PrivateKey`
// config (falling back to the shared dev key) and reused for every token.
public sealed class JwtTokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _credentials;

    public JwtTokenIssuer(JwtSettings settings, IConfiguration config)
    {
        _settings = settings;

        var rsa = RSA.Create();
        // Private key = mint. Prod supplies real key material via Jwt:PrivateKey; dev falls back to the
        // committed dev key so the demo/tests work with zero wiring (DevJwtDefaults documents the DEV-ONLY
        // caveat). The RSA instance is held for the process lifetime by the SigningCredentials below.
        rsa.ImportFromPem(config["Jwt:PrivateKey"] ?? DevJwtDefaults.DevPrivateKeyPem);
        _credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
    }

    // Mint a token whose `sub` is the customer id, stamped with the configured issuer/audience/lifetime.
    public string Issue(string customerId)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(_settings.AccessTokenLifetime),
            // The customer id as a raw `sub` claim (dictionary form avoids ClaimsIdentity's type-mapping).
            Claims = new Dictionary<string, object> { ["sub"] = customerId },
            SigningCredentials = _credentials,
        };

        return Handler.CreateToken(descriptor);
    }
}
