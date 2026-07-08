using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Identity.Features;

// Log out (Workshop 002 slice 5.11, ADR 023). The issued JWT is STATELESS — Identity keeps no session and no
// token store — so logout is fundamentally a CLIENT-side act: the SPA discards its held token and
// useCurrentCustomer returns to unauthenticated (see client/src/identity/authStore.ts). This endpoint exists
// only as an explicit, self-documenting server-side acknowledgement of that; it holds no state to clear and
// simply returns 200.
//
// Deferred (Workshop 002 § 8 Q15): there is NO server-side revocation this increment. A token already handed
// out stays cryptographically valid until it EXPIRES — clicking log out does not un-issue it. That is why the
// access-token lifetime is kept short (JwtSettings.AccessTokenLifetime). A denylist, or short access tokens +
// a refresh flow, is the modeled-not-built future increment.
public record LogOut;

public static class LogOutEndpoint
{
    [WolverinePost("/logout")]
    public static IResult Post(LogOut command) => Results.Ok();
}
