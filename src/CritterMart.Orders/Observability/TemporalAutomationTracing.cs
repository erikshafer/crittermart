using System.Diagnostics;
using Wolverine;

namespace CritterMart.Orders.Observability;

// Observability seam for the two Bruun temporal automations (Workshop 001 slices 4.7 + 3.4).
//
// A scheduled self-message (OrderPaymentTimeout / CartActivityTimeout) is delivered by Wolverine
// minutes — or hours — after the request that scheduled it. Wolverine stamps the scheduling
// request's W3C trace context onto every envelope it sends, including delayed ones, so by default
// the fired timeout's execution span is parented INTO the original placement / add-to-cart trace.
// That keeps the original trace "open" until the deadline fires, inflating its duration to the
// entire timeout window (10 minutes for an order, 2 hours for a cart) even though the request
// itself completed in ~50 ms. A placement trace that reads ten minutes is a lie about the
// request's latency — and the trace waterfall is a centerpiece teaching visual.
//
// The fix is OpenTelemetry's idiom for asynchronous follow-up work: the fired timeout runs in its
// OWN root trace, related to the originating request by a span LINK rather than a parent-child
// edge. Links express causation without coupling latency, so the placement trace stays ~50 ms and
// the timeout stays fully observable in its own right (and navigable back to the request that
// armed it). Each handler suppresses Wolverine's own parented span with
// [WolverineLogging(telemetryEnabled: false)] so that only this clean linked root remains.
//
// The ActivitySource is named to match the service's ApplicationName, which ServiceDefaults already
// feeds to AddSource (CritterMart.ServiceDefaults/Extensions.cs) — so these spans export with no
// extra wiring.
public static class TemporalAutomationTracing
{
    public static readonly ActivitySource Source = new("CritterMart.Orders");

    // Open a new ROOT activity for a fired timeout, linked back to the trace that scheduled it.
    // The link target is read from envelope.ParentId — the W3C traceparent Wolverine stamped on the
    // delayed envelope when the request scheduled it. A missing or unparseable parent simply yields
    // an unlinked root (still correct — just not navigable back to the originating request).
    //
    // Returns null when no OpenTelemetry listener is sampling the source (e.g. in unit/integration
    // tests with no exporter wired), so callers must null-guard. `using var` and `?.SetTag` both
    // no-op on null — which is exactly why this change leaves the domain tests untouched.
    public static Activity? StartLinkedRoot(string name, Envelope envelope)
    {
        var links = ActivityContext.TryParse(envelope.ParentId, null, out var origin)
            ? new[] { new ActivityLink(origin) }
            : null;

        // Force a NEW ROOT. StartActivity silently re-parents onto Activity.Current even when an
        // empty parentContext is passed (an invalid ActivityContext is treated as "no override" and
        // it falls back to the ambient span), so the only reliable detach is to clear Current first.
        // The started span then becomes the new Current; when nothing is sampling (StartActivity
        // returns null) we restore the ambient context so the caller's trace is not stranded.
        var ambient = Activity.Current;
        Activity.Current = null;
        var activity = Source.StartActivity(name, ActivityKind.Internal, parentContext: default, links: links);
        if (activity is null)
        {
            Activity.Current = ambient;
        }

        return activity;
    }
}
