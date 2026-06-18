using Alba;
using CritterMart.Identity.Customers;
using CritterMart.Identity.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Identity.Tests;

[Collection("identity")]
[Trait("Category", "Integration")]
public class RegisterCustomerTests
{
    private readonly IdentityAppFixture _fixture;

    public RegisterCustomerTests(IdentityAppFixture fixture) => _fixture = fixture;

    // EF Core reset between tests — the row-store analogue of Inventory's DeleteAllEventDataAsync.
    // ExecuteDeleteAsync is a bulk DELETE against the identity-schema customers table.
    private async Task ResetAsync()
    {
        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Customers.ExecuteDeleteAsync();
    }

    private async Task<string> RegisterAsync(string email, string displayName)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterCustomer(email, displayName)).ToUrl("/customers");
            s.StatusCodeShouldBe(201);
        });

        var body = result.ReadAsJson<RegisterCustomerResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    // Happy path: a plain EF Core insert, read back over HTTP. The same shape as Inventory's
    // "receive then read", but the write is a row, not an event stream.
    [Fact]
    public async Task registering_a_customer_creates_a_readable_row()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/customers/{id}");
            s.StatusCodeShouldBe(200);
        });

        var customer = result.ReadAsJson<CustomerResponse>();
        customer.ShouldNotBeNull();
        customer.Id.ShouldBe(id);
        customer.Email.ShouldBe("ada@example.com");
        customer.DisplayName.ShouldBe("Ada Lovelace");
        customer.RegisteredAt.ShouldNotBe(default);
    }

    // AutoApplyTransactions actually committed to Postgres — assert against a FRESH DbContext scope,
    // not the HTTP read, so we know the row is on disk and not merely in a request's unit of work.
    [Fact]
    public async Task registering_persists_a_row_readable_from_a_fresh_scope()
    {
        await ResetAsync();
        var id = await RegisterAsync("grace@example.com", "Grace Hopper");

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var row = await db.Customers.SingleAsync(c => c.Id == id);
        row.Email.ShouldBe("grace@example.com");
        row.DisplayName.ShouldBe("Grace Hopper");
    }

    [Fact]
    public async Task getting_an_unknown_customer_returns_404()
    {
        await ResetAsync();
        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/customers/does-not-exist");
            s.StatusCodeShouldBe(404);
        });
    }

    // Failure path — the kept-service guard the spike skipped (Workshop 002 § 6 slice 5.1). A duplicate
    // email is rejected with CustomerAlreadyRegistered (409), case-insensitively and idempotently. The
    // second POST uses a different casing AND surrounding whitespace, which the normalized guard must
    // still catch — and the registry must hold exactly one row for that email afterward (no shadow row,
    // the row-store analogue of Catalog's "no shadow event" assertion on a duplicate SKU).
    [Fact]
    public async Task registering_a_duplicate_email_is_rejected_case_insensitively()
    {
        await ResetAsync();
        await RegisterAsync("ada@example.com", "Ada Lovelace");

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterCustomer("  Ada@Example.com  ", "Ada L.")).ToUrl("/customers");
            s.StatusCodeShouldBe(409);
        });

        var problem = result.ReadAsJson<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("CustomerAlreadyRegistered");

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var count = await db.Customers.CountAsync(c => c.Email == "ada@example.com");
        count.ShouldBe(1);
    }

    // The transactional-outbox half: RegisterCustomer cascades CustomerRegistered, which Wolverine
    // enrolls in the EF Core outbox inside the SAME transaction as the insert. A tracked session
    // captures the published envelope even with external transports stubbed — proof the message left
    // the handler through the outbox (its cross-process delivery to RabbitMQ is verified live).
    [Fact]
    public async Task registering_publishes_customer_registered_through_the_outbox()
    {
        await ResetAsync();

        var tracked = await _fixture.Host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(_ => _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new RegisterCustomer("alan@example.com", "Alan Turing")).ToUrl("/customers");
                s.StatusCodeShouldBe(201);
            }));

        var published = tracked.Sent.SingleMessage<CustomerRegistered>();
        published.Email.ShouldBe("alan@example.com");
        published.DisplayName.ShouldBe("Alan Turing");
        published.CustomerId.ShouldNotBeNullOrWhiteSpace();
    }
}
