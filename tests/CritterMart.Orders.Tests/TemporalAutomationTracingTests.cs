using System.Diagnostics;
using CritterMart.Orders.Observability;
using Shouldly;
using Wolverine;
using Xunit;

namespace CritterMart.Orders.Tests;

// The trace-context fix for the two Bruun temporal automations (Workshop 001 slices 4.7 + 3.4):
// a fired timeout must run in its OWN root trace, LINKED back to — not nested under — the request
// that scheduled it, so the placement / add-to-cart trace never inflates to the timeout window
// (10 min for an order, 2 h for a cart). These assert the seam both timeout handlers share. The
// other half of the fix — [WolverineLogging(telemetryEnabled: false)] suppressing Wolverine's own
// parented span — is a documented Wolverine feature, cross-checked live on the Aspire dashboard.
//
// Pure unit tests: no host, no Postgres. The crux is the rooting assertion — it is deliberately run
// with an AMBIENT placement Activity set as Activity.Current, because that is the live condition the
// root must survive (StartActivity must not silently re-parent onto Current).
public class TemporalAutomationTracingTests
{
    // A listener is required or ActivitySource.StartActivity returns null (no sampler) — which is
    // itself the property that keeps the tracing inert in the domain tests.
    private static ActivityListener ListenToOrders() => new()
    {
        ShouldListenTo = source => source.Name == "CritterMart.Orders",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    [Fact]
    public void a_fired_timeout_opens_a_new_root_trace_linked_back_to_the_scheduling_request()
    {
        using var listener = ListenToOrders();
        ActivitySource.AddActivityListener(listener);

        // The placement request's trace, set as Activity.Current — exactly the ambient condition a
        // fired timeout is delivered into. Its W3C id is what PlaceOrder's DelayedFor cascade stamps
        // onto the scheduled envelope's ParentId.
        using var placement = TemporalAutomationTracing.Source.StartActivity("POST /orders");
        placement.ShouldNotBeNull();
        Activity.Current.ShouldBe(placement);
        var envelope = new Envelope { ParentId = placement.Id };

        using var timeout = TemporalAutomationTracing.StartLinkedRoot("order.payment.timeout", envelope);

        timeout.ShouldNotBeNull();
        // A NEW ROOT — NOT parented to the placement trace, so it can never inflate that trace.
        timeout.Parent.ShouldBeNull();
        timeout.ParentId.ShouldBeNull();
        timeout.TraceId.ShouldNotBe(placement.TraceId);
        // But LINKED to placement, so the deferred compensation stays navigable from the request.
        timeout.Links.ShouldContain(link => link.Context.TraceId == placement.TraceId);
    }

    [Fact]
    public void a_timeout_with_no_scheduling_context_still_opens_a_clean_unlinked_root()
    {
        using var listener = ListenToOrders();
        ActivitySource.AddActivityListener(listener);

        // An envelope with no ParentId (a context-less delivery) — still a clean root, just unlinked.
        using var timeout = TemporalAutomationTracing.StartLinkedRoot("cart.activity.timeout", new Envelope());

        timeout.ShouldNotBeNull();
        timeout.Parent.ShouldBeNull();
        timeout.Links.ShouldBeEmpty();
    }
}
