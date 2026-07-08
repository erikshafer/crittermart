using CritterMart.Identity.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace CritterMart.Identity.Features;

// Log in — verify the password and issue a JWT (Workshop 002 slice 5.9, ADR 023). On a successful password
// check Identity mints a signed JWT (sub = customer id) and returns it; the token is NOT persisted.
public record LogIn(string Email, string Password);

public record LogInResponse(string Token, string CustomerId);

public static class LogInEndpoint
{
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // No ValidateAsync guard here — a login failure is NOT a modeled domain conflict to report richly; it is
    // a flat 401 that must reveal nothing. Both "unknown email" and "wrong password" return the SAME 401 with
    // no distinguishing detail (spec 5.9: no user enumeration).
    [WolverinePost("/login")]
    public static async Task<IResult> Post(
        LogIn command,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        JwtTokenIssuer issuer)
    {
        var email = Normalize(command.Email);

        // FindByEmailAsync matches on NormalizedEmail (set at registration). A null user and a bad password
        // are handled identically below — we still return 401, never "no such user".
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // CheckPasswordSignInAsync verifies the hashed password (and honors lockout, though round one enables
        // none). It does NOT establish a cookie/session — Identity is a bearer-token issuer, not a cookie
        // auth server (ADR 023). lockoutOnFailure:false keeps a wrong password from counting toward a lockout
        // we don't configure.
        var result = await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        var token = issuer.Issue(user.Id);
        return Results.Ok(new LogInResponse(token, user.Id));
    }
}
