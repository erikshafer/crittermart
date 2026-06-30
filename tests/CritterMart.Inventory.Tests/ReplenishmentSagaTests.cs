using CritterMart.Inventory.Stock;
using Shouldly;
using Xunit;

namespace CritterMart.Inventory.Tests;

// Pure unit tests over the Replenishment saga's Start/Handle methods — no database, no host. This is the
// teaching point of a convention Wolverine.Saga: the decision logic is plain methods on a POCO, testable in
// isolation, in contrast to the Order's PMvH where the state lives on an event stream (ADR 007). End-to-end
// completion (MarkCompleted deleting the saga document) is asserted in ReplenishmentSagaIntegrationTests;
// here we assert the saga's own state, the messages Start returns, and the IsCompleted() flag.
[Trait("Category", "Unit")]
public class ReplenishmentSagaTests
{
    private static readonly ReplenishDeadline Deadline = new(TimeSpan.FromMinutes(2));

    // Slice 2.5: a shortfall with no open saga opens one (blank instance), requests a restock, and schedules
    // the deadline. StartOrHandle detects "new" via Outstanding == 0.
    [Fact]
    public void start_or_handle_opens_the_saga_requests_restock_and_schedules_the_timeout()
    {
        var saga = new Replenishment();

        var outgoing = saga.StartOrHandle(new BackorderDetected("crit-001", 1), Deadline);

        saga.Id.ShouldBe("crit-001");
        saga.Outstanding.ShouldBe(1);
        saga.IsCompleted().ShouldBeFalse();

        outgoing.OfType<RequestRestock>().Single().ShouldBe(new RequestRestock("crit-001", 1));
        var timeout = outgoing.OfType<ReplenishTimeout>().Single();
        timeout.Sku.ShouldBe("crit-001");
        timeout.Delay.ShouldBe(Deadline.Duration);
    }

    // Slice 2.5: a re-detected shortfall on an open saga takes the GREATER value (idempotent, never summed)
    // and cascades nothing — no second RequestRestock, no second timeout.
    [Fact]
    public void re_detected_shortfall_raises_outstanding_to_the_greater_value_without_re_cascading()
    {
        var saga = new Replenishment { Id = "crit-001", Outstanding = 1 };

        var outgoing = saga.StartOrHandle(new BackorderDetected("crit-001", 3), Deadline);
        saga.Outstanding.ShouldBe(3);
        outgoing.ShouldBeEmpty();

        // A smaller (or duplicate) shortfall does not lower it — max, never sum.
        saga.StartOrHandle(new BackorderDetected("crit-001", 2), Deadline);
        saga.Outstanding.ShouldBe(3);
    }

    // Slice 2.6: a receipt that covers the outstanding shortfall completes the saga.
    [Fact]
    public void a_covering_restock_completes_the_saga()
    {
        var saga = new Replenishment { Id = "crit-001", Outstanding = 1 };

        saga.Handle(new RestockArrived("crit-001", 100));

        saga.IsCompleted().ShouldBeTrue();
    }

    // Slice 2.6: a partial receipt reduces the shortfall and the saga stays open (no fresh RequestRestock).
    [Fact]
    public void a_partial_restock_reduces_outstanding_and_stays_open()
    {
        var saga = new Replenishment { Id = "crit-001", Outstanding = 10 };

        saga.Handle(new RestockArrived("crit-001", 4));

        saga.Outstanding.ShouldBe(6);
        saga.IsCompleted().ShouldBeFalse();
    }

    // Slice 2.7: a timeout while still open escalates (returns the operator alert) and completes.
    [Fact]
    public void a_timeout_while_open_escalates_and_completes()
    {
        var saga = new Replenishment { Id = "crit-001", Outstanding = 1 };

        var escalated = saga.Handle(new ReplenishTimeout("crit-001", Deadline.Duration));

        escalated.ShouldBe(new ReplenishmentEscalated("crit-001", 1));
        saga.IsCompleted().ShouldBeTrue();
    }
}
