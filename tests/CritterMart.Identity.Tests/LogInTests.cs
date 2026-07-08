using System.Security.Cryptography;
using Alba;
using CritterMart.Identity.Customers;
using CritterMart.Identity.Features;
using CritterMart.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace CritterMart.Identity.Tests;

// Slice 5.9 — log in and issue a JWT (ADR 023). Password check → mint a signed JWT (sub = customer id).
[Collection("identity")]
[Trait("Category", "Integration")]
public class LogInTests
{
    private readonly IdentityAppFixture _fixture;

    public LogInTests(IdentityAppFixture fixture) => _fixture = fixture;

    private const string ValidPassword = "critter1";

    private async Task ResetAsync()
    {
        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        await db.Users.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
    }

    private async Task<string> RegisterAsync(string email, string displayName)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterWithCredentials(email, displayName, ValidPassword)).ToUrl("/register");
            s.StatusCodeShouldBe(201);
        });
        return result.ReadAsJson<RegisterWithCredentialsResponse>()!.Id;
    }

    // Happy path: a correct password returns a JWT whose sub is the customer id, and whose signature +
    // issuer + audience + lifetime validate offline against Identity's PUBLIC key — the exact check a
    // resource server performs (slice 5.10), done here against the dev public key.
    [Fact]
    public async Task logging_in_issues_a_jwt_whose_sub_is_the_customer_id()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LogIn("ada@example.com", ValidPassword)).ToUrl("/login");
            s.StatusCodeShouldBe(200);
        });

        var body = result.ReadAsJson<LogInResponse>();
        body.ShouldNotBeNull();
        body.CustomerId.ShouldBe(id);
        body.Token.ShouldNotBeNullOrWhiteSpace();

        // Validate the token exactly as a resource server would — offline, against the public key.
        using var rsa = RSA.Create();
        rsa.ImportFromPem(DevJwtDefaults.DevPublicKeyPem);
        var handler = new JsonWebTokenHandler();
        var validation = await handler.ValidateTokenAsync(body.Token, new TokenValidationParameters
        {
            ValidIssuer = DevJwtDefaults.Issuer,
            ValidAudience = DevJwtDefaults.Audience,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true
        });

        validation.IsValid.ShouldBeTrue();
        validation.Claims["sub"].ShouldBe(id);
    }

    // Failure path — wrong password → 401, no token, no enumeration.
    [Fact]
    public async Task logging_in_with_a_wrong_password_returns_401()
    {
        await ResetAsync();
        await RegisterAsync("ada@example.com", "Ada Lovelace");

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LogIn("ada@example.com", "wrong-password")).ToUrl("/login");
            s.StatusCodeShouldBe(401);
        });
    }

    // Failure path — unknown email → the SAME 401 as a wrong password (no user enumeration).
    [Fact]
    public async Task logging_in_with_an_unknown_email_returns_401()
    {
        await ResetAsync();

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LogIn("nobody@example.com", ValidPassword)).ToUrl("/login");
            s.StatusCodeShouldBe(401);
        });
    }
}
