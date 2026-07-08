using Alba;
using CritterMart.Contracts;
using CritterMart.Identity.Customers;
using CritterMart.Identity.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Identity.Tests;

// Slice 5.8 — register with credentials (ADR 023). The credentialed evolution of RegisterCustomer: creates
// an ASP.NET Core Identity user (password hashed) AND the Customer row, same id, one transaction.
[Collection("identity")]
[Trait("Category", "Integration")]
public class RegisterWithCredentialsTests
{
    private readonly IdentityAppFixture _fixture;

    public RegisterWithCredentialsTests(IdentityAppFixture fixture) => _fixture = fixture;

    // Clear BOTH tables — the Identity user table and the registry customers table — between tests.
    private async Task ResetAsync()
    {
        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        await db.Users.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
    }

    private const string ValidPassword = "critter1"; // 8 chars, lowercase + digit — passes the demo policy

    // Happy path: a user row + a customer row with the SAME id, and CustomerRegistered on the outbox — the
    // whole all-or-nothing register committed as one transaction. Also proves Weasel created the ASP.NET
    // Identity tables (design.md decision 1's flagged risk): if it hadn't, db.Users.Add would fail here.
    [Fact]
    public async Task registering_with_credentials_creates_user_and_customer_with_same_id()
    {
        await ResetAsync();

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterWithCredentials("ada@example.com", "Ada Lovelace", ValidPassword))
                .ToUrl("/register");
            s.StatusCodeShouldBe(201);
        });

        var body = result.ReadAsJson<RegisterWithCredentialsResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldNotBeNullOrWhiteSpace();

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();

        var customer = await db.Customers.SingleAsync(c => c.Id == body.Id);
        customer.Email.ShouldBe("ada@example.com");
        customer.DisplayName.ShouldBe("Ada Lovelace");

        var user = await db.Users.SingleAsync(u => u.Id == body.Id);
        user.Email.ShouldBe("ada@example.com");
        user.PasswordHash.ShouldNotBeNullOrWhiteSpace(); // hashed, never plaintext
    }

    // The outbox half: RegisterWithCredentials cascades CustomerRegistered enrolled in the same transaction.
    [Fact]
    public async Task registering_with_credentials_publishes_customer_registered_through_the_outbox()
    {
        await ResetAsync();

        var tracked = await _fixture.Host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(_ => _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new RegisterWithCredentials("grace@example.com", "Grace Hopper", ValidPassword))
                    .ToUrl("/register");
                s.StatusCodeShouldBe(201);
            }));

        var published = tracked.Sent.SingleMessage<CustomerRegistered>();
        published.Email.ShouldBe("grace@example.com");
        published.DisplayName.ShouldBe("Grace Hopper");
    }

    // Failure path — duplicate email (case-insensitive), rejected before any write.
    [Fact]
    public async Task registering_a_duplicate_email_is_rejected()
    {
        await ResetAsync();
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterWithCredentials("ada@example.com", "Ada Lovelace", ValidPassword))
                .ToUrl("/register");
            s.StatusCodeShouldBe(201);
        });

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterWithCredentials("  Ada@Example.com  ", "Ada L.", ValidPassword))
                .ToUrl("/register");
            s.StatusCodeShouldBe(409);
        });

        var problem = result.ReadAsJson<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("CustomerAlreadyRegistered");

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        (await db.Customers.CountAsync(c => c.Email == "ada@example.com")).ShouldBe(1);
        (await db.Users.CountAsync(u => u.Email == "ada@example.com")).ShouldBe(1);
    }

    // Failure path — a password that fails policy creates NOTHING (no user, no row, no event).
    [Fact]
    public async Task registering_with_a_weak_password_is_rejected_and_writes_nothing()
    {
        await ResetAsync();

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterWithCredentials("weak@example.com", "Weak Password", "weak"))
                .ToUrl("/register");
            s.StatusCodeShouldBe(400);
        });

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == "weak@example.com")).ShouldBeFalse();
        (await db.Users.AnyAsync(u => u.Email == "weak@example.com")).ShouldBeFalse();
    }
}
