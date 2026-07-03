using Alba;
using CritterMart.Identity.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Identity.Tests;

// Slices 5.5–5.7 wiring, end to end against the real Identity host (Alba + Testcontainers Postgres,
// external transports stubbed). Asserts the EmailChange saga row directly (the EF-Core counterpart of
// ReplenishmentSagaIntegrationTests' Marten saga-document loads) — proving the saga rides the existing
// AddDbContextWithWolverineIntegration wiring with no extra registration, and that confirm/timeout delete
// the row (MarkCompleted). Also empirically resolves design.md's flagged open question: whether a
// NotFound-routed ConfirmEmailChange maps to HTTP 404.
[Collection("identity")]
[Trait("Category", "Integration")]
public class EmailChangeSagaIntegrationTests
{
    private readonly IdentityAppFixture _fixture;

    public EmailChangeSagaIntegrationTests(IdentityAppFixture fixture) => _fixture = fixture;

    private async Task ResetAsync()
    {
        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.EmailChanges.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
    }

    private async Task<string> RegisterAsync(string email, string displayName)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CritterMart.Identity.Features.RegisterCustomer(email, displayName)).ToUrl("/customers");
            s.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<CritterMart.Identity.Features.RegisterCustomerResponse>()!.Id;
    }

    private async Task<EmailChange?> LoadSagaAsync(string customerId)
    {
        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.EmailChanges.FindAsync(customerId);
    }

    private async Task<string> ReadEmailAsync(string customerId)
    {
        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return (await db.Customers.SingleAsync(c => c.Id == customerId)).Email;
    }

    // Spec scenario "Open an email-change saga": the saga opens with the normalized pending email; the
    // Customer row is untouched until confirmation.
    [Fact]
    public async Task requesting_an_email_change_opens_a_saga_and_leaves_the_row_unchanged()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "Ada.New@Example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });

        var saga = await LoadSagaAsync(id);
        saga.ShouldNotBeNull();
        saga.PendingEmail.ShouldBe("ada.new@example.com");

        (await ReadEmailAsync(id)).ShouldBe("ada@example.com");
    }

    // Spec scenario "Reject a request for an unknown customer": no saga opens.
    [Fact]
    public async Task requesting_an_email_change_for_an_unknown_customer_is_rejected()
    {
        await ResetAsync();

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange("ghost-1", "new@example.com")).ToUrl("/customers/ghost-1/email-change");
            s.StatusCodeShouldBe(404);
        });

        result.ReadAsJson<ProblemDetails>()!.Title.ShouldBe("CustomerNotFound");
        (await LoadSagaAsync("ghost-1")).ShouldBeNull();
    }

    // Spec scenario "Reject a request for an email already registered to another customer": no saga opens.
    [Fact]
    public async Task requesting_an_email_already_registered_to_another_customer_is_rejected()
    {
        await ResetAsync();
        await RegisterAsync("taken@example.com", "Someone Else");
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "taken@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(409);
        });

        result.ReadAsJson<ProblemDetails>()!.Title.ShouldBe("EmailAlreadyRegistered");
        (await LoadSagaAsync(id)).ShouldBeNull();
    }

    // Spec scenario "Re-request ... updates the pending email, not the deadline": end-to-end through HTTP,
    // asserting the row-level effect (the timeout-not-rescheduled behavior itself is covered at the unit
    // level; this proves the HTTP path reaches the same StartOrHandle instance on the second call).
    [Fact]
    public async Task a_second_request_updates_the_pending_email_on_the_same_saga()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "first@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "second@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });

        (await LoadSagaAsync(id))!.PendingEmail.ShouldBe("second@example.com");
    }

    // Spec scenario "Confirm within the window applies the change": the Customer row updates and the
    // saga row is gone (MarkCompleted).
    [Fact]
    public async Task confirming_within_the_window_applies_the_change_and_completes_the_saga()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "ada.new@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailChange(id)).ToUrl($"/customers/{id}/confirm-email-change");
            s.StatusCodeShouldBe(204);
        });

        (await ReadEmailAsync(id)).ShouldBe("ada.new@example.com");
        (await LoadSagaAsync(id)).ShouldBeNull();
    }

    // Spec scenario "Confirm conflicts with an email claimed during the window": the saga stays OPEN
    // (not completed) and the row is unchanged.
    [Fact]
    public async Task confirming_a_conflicting_email_is_rejected_and_the_saga_stays_open()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "ada.new@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });
        await RegisterAsync("ada.new@example.com", "Someone Faster");

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailChange(id)).ToUrl($"/customers/{id}/confirm-email-change");
            s.StatusCodeShouldBe(409);
        });

        result.ReadAsJson<ProblemDetails>()!.Title.ShouldBe("EmailChangeConflict");
        (await ReadEmailAsync(id)).ShouldBe("ada@example.com");
        (await LoadSagaAsync(id)).ShouldNotBeNull();
    }

    // Spec scenario "Confirm after the window expired is a no-op" — driven by delivering EmailChangeTimeout
    // directly (deterministic, no real wall-clock wait), then confirming against the now-absent saga. This
    // empirically resolves design.md's flagged question: does a NotFound-routed HTTP request return 404?
    [Fact]
    public async Task confirming_after_the_saga_already_timed_out_is_a_no_op()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "ada.new@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });

        await _fixture.Host.InvokeMessageAndWaitAsync(new EmailChangeTimeout(id, TimeSpan.FromMinutes(2)));
        (await LoadSagaAsync(id)).ShouldBeNull();

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailChange(id)).ToUrl($"/customers/{id}/confirm-email-change");
            // Empirically confirmed: NotFound doesn't surface as 404 through InvokeAsync — a confirm
            // against an absent saga is indistinguishable at the HTTP layer from a successful confirm
            // (both 204), which is the more faithful "silent no-op" (see design.md decision 3).
            s.StatusCodeShouldBe(204);
        });

        (await ReadEmailAsync(id)).ShouldBe("ada@example.com");
    }

    // Spec scenario "Timeout with no confirmation drops the pending change": driven directly for
    // determinism (mirrors Replenishment's timeout tests).
    [Fact]
    public async Task a_timeout_with_no_confirmation_drops_the_pending_change()
    {
        await ResetAsync();
        var id = await RegisterAsync("ada@example.com", "Ada Lovelace");
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RequestEmailChange(id, "ada.new@example.com")).ToUrl($"/customers/{id}/email-change");
            s.StatusCodeShouldBe(204);
        });

        await _fixture.Host.InvokeMessageAndWaitAsync(new EmailChangeTimeout(id, TimeSpan.FromMinutes(2)));

        (await ReadEmailAsync(id)).ShouldBe("ada@example.com");
        (await LoadSagaAsync(id)).ShouldBeNull();
    }

    // Spec scenario "Timeout after the saga already resolved is a no-op": the saga's
    // NotFound(EmailChangeTimeout) keeps a late-firing timeout silent.
    [Fact]
    public async Task a_timeout_for_an_absent_saga_is_a_silent_no_op()
    {
        await ResetAsync();

        await Should.NotThrowAsync(() =>
            _fixture.Host.InvokeMessageAndWaitAsync(new EmailChangeTimeout("ghost-2", TimeSpan.FromMinutes(2))));

        (await LoadSagaAsync("ghost-2")).ShouldBeNull();
    }
}
