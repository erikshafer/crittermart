using CritterMart.ServiceDefaults;

namespace CritterMart.Identity.Auth;

// Config-bound settings for the issued JWT (ADR 023, slice 5.9): the issuer + audience the token is stamped
// with (and the resource servers validate against) and the access-token lifetime. Bound from the `Jwt`
// config section in Program.cs, falling back to the shared dev defaults so the demo + tests need no wiring.
//
// A demo-paced default lifetime (1h) keeps the login → expire → 401 path observable without a multi-day wait,
// config-overridable for prod — the auth sibling of the EmailChangeDeadline / PaymentDeadline "demo-workable
// default, config-overridable" pattern (though NOT one of the four AppHost demo knobs slated for post-talk
// deletion — a short access-token TTL is realistic, not a demo hack).
public record JwtSettings
{
    public string Issuer { get; init; } = DevJwtDefaults.Issuer;
    public string Audience { get; init; } = DevJwtDefaults.Audience;
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromHours(1);
}
