using CritterMart.Identity.Customers;
using Shouldly;
using Xunit;

namespace CritterMart.Identity.Tests;

// Pure unit tests over the EmailChange saga's StartOrHandle/Handle(EmailChangeTimeout) methods — no
// database, no host. Mirrors ReplenishmentSagaTests.cs. Handle(ConfirmEmailChange, IdentityDbContext) needs
// a real DbContext (it reads/writes the Customer row), so its behavior is covered by
// EmailChangeSagaIntegrationTests instead — this class covers only what is genuinely pure.
[Trait("Category", "Unit")]
public class EmailChangeSagaTests
{
    private static readonly EmailChangeDeadline Deadline = new(TimeSpan.FromMinutes(2));

    // Slice 5.5: a request with no open saga opens one (blank instance), normalizes the email, and
    // schedules the deadline. StartOrHandle detects "new" via PendingEmail being empty.
    [Fact]
    public void start_or_handle_opens_the_saga_and_schedules_the_timeout()
    {
        var saga = new EmailChange();

        var outgoing = saga.StartOrHandle(new RequestEmailChange("c-1", "Ada.New@Example.com"), Deadline);

        saga.Id.ShouldBe("c-1");
        saga.PendingEmail.ShouldBe("ada.new@example.com");
        saga.IsCompleted().ShouldBeFalse();

        var timeout = outgoing.OfType<EmailChangeTimeout>().Single();
        timeout.CustomerId.ShouldBe("c-1");
        timeout.Delay.ShouldBe(Deadline.Duration);
    }

    // Slice 5.5, the corrected behavior (Workshop 002 v1.1 § 8 item 7): a re-request on an already-open
    // saga updates PendingEmail but does NOT cascade a second EmailChangeTimeout — the original deadline
    // keeps governing the window, since Wolverine cannot cancel the first one.
    [Fact]
    public void a_re_request_updates_pending_email_without_rescheduling_the_timeout()
    {
        var saga = new EmailChange { Id = "c-1", PendingEmail = "ada.new@example.com" };

        var outgoing = saga.StartOrHandle(new RequestEmailChange("c-1", "ada.newer@example.com"), Deadline);

        saga.PendingEmail.ShouldBe("ada.newer@example.com");
        outgoing.ShouldBeEmpty();
    }

    // Slice 5.7: a timeout while still open drops the pending change (no row write happens here — the
    // saga's own state is all this method touches) and completes.
    [Fact]
    public void a_timeout_while_open_completes_the_saga()
    {
        var saga = new EmailChange { Id = "c-1", PendingEmail = "ada.new@example.com" };

        saga.Handle(new EmailChangeTimeout("c-1", Deadline.Duration));

        saga.IsCompleted().ShouldBeTrue();
    }
}
