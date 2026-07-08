using Microsoft.AspNetCore.Http;

namespace CritterMart.Orders.Auth;

// Resolves the authenticated customer id for a customer-keyed endpoint under the LAYERED cutover (ADR 023,
// slice 5.10). The `sub` claim of a validated JWT is the trust boundary; the round-one X-Customer-Id header
// survives only as a DEV-ONLY fallback so the seeder, the existing tests, and demo-traffic.ps1 keep working
// in this PR (DEBT-tracked for removal — the frontend no longer sends it). Four cases, in priority order:
//
//   1. A VALID Bearer token → its `sub` claim wins (the trust boundary). AddJwtBearer validated it offline.
//   2. An INVALID/expired Bearer token was presented (Authorization: Bearer … but authentication failed, so
//      http.User carries no `sub`) → 401, rejected LOCALLY, no fallback. This is the spec's "invalid or
//      expired token is rejected locally" — no HTTP into Identity to decide it.
//   3. No Bearer token, but an X-Customer-Id header → the dev-only fallback identity.
//   4. Neither → no identity; the caller returns its existing "identity required" 400.
//
// The endpoints are deliberately NOT blanket-[Authorize]'d — that would reject the header-fallback path the
// existing tests rely on. This helper enforces "must have an identity" instead, distinguishing a bad token
// (401) from no identity at all (the endpoint's 400).
public static class CustomerIdentity
{
    public static bool TryResolve(
        HttpContext http, string? headerCustomerId, out string customerId, out IResult? failure)
    {
        customerId = "";
        failure = null;

        // 1. A validated token's `sub` (MapInboundClaims = false keeps it readable as `sub`).
        var sub = http.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub))
        {
            customerId = sub;
            return true;
        }

        // 2. A Bearer token was presented but did not authenticate → reject locally with 401.
        var authorization = http.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            failure = Results.Unauthorized();
            return false;
        }

        // 3. Dev-only X-Customer-Id fallback (no token presented).
        if (!string.IsNullOrWhiteSpace(headerCustomerId))
        {
            customerId = headerCustomerId;
            return true;
        }

        // 4. No identity at all — the caller supplies the response (keeps the existing 400).
        return false;
    }
}
