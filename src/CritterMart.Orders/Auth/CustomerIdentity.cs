using System.Security.Claims;

namespace CritterMart.Orders.Auth;

// The authenticated customer id for a customer-keyed endpoint (ADR 023, hard cutover). The `sub` claim of a
// validated JWT is the SOLE trust boundary: the customer-keyed endpoints are [Authorize]'d, so the JwtBearer
// middleware has already rejected any missing, tampered, wrong-issuer/audience, or expired token with 401 —
// decided locally against the config-distributed public key — before a handler runs. The round-one
// X-Customer-Id header fallback (the layered cutover's dev-only shim) is retired; no request header can name
// a customer. MapInboundClaims = false (Program.cs) keeps `sub` readable as `sub`.
public static class CustomerIdentity
{
    // Guaranteed present behind [Authorize]: every token Identity mints carries `sub` (JwtTokenIssuer).
    // A missing claim here means a misconfigured issuer, not a client error — throw, don't 4xx.
    public static string CustomerId(this ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException(
            "Authorized request carries no `sub` claim — Orders only trusts tokens minted by Identity, which always sets `sub`.");
}
