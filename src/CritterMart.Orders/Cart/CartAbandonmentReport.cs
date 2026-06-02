using JasperFx.Events;
using Marten.Events.Projections;

namespace CritterMart.Orders.Cart;

// The round-one async projection teaser (ADR 008; Workshop 001 § 7): an analytics-grade daily
// rollup of cart abandonment, fed by CartAbandoned events from EVERY Cart stream — one document
// per UTC calendar day. Nothing on the demo's hot path reads this; it exists to be REBUILT on
// demand ("the events were always there; the read model is just a function of them").
public class CartAbandonmentDailyReport
{
    public string Id { get; set; } = string.Empty;          // the UTC day, "yyyy-MM-dd"
    public int AbandonedCartCount { get; set; }
    public decimal TotalValueAbandoned { get; set; }
    public Dictionary<string, int> AbandonedSkus { get; set; } = [];
}

// The codebase's first MULTI-stream projection: where CartView and CartsAwaitingActivity fold one
// stream into one document, this folds events from many Cart streams into one document per day —
// the events' append-timestamp date is the document identity (Identity<IEvent<T>>, Marten's
// metadata-routing convention; ctx7-verified first-class support, no custom grouper needed).
//
// Registered with ProjectionLifecycle.Async and NO AddAsyncDaemon anywhere (ADR 008: no daemon
// for round one): the report stays empty until an on-demand rebuild materializes it. That
// emptiness is not a bug — it is the talk's teaching beat.
//
// `partial` is load-bearing (Marten 9 convention): conventional Apply methods are dispatched by
// the compile-time JasperFx source generator, which needs a partial class to extend. Without it
// the host refuses to boot with InvalidProjectionException — there is no runtime fallback.
public partial class CartAbandonmentReportProjection : MultiStreamProjection<CartAbandonmentDailyReport, string>
{
    public CartAbandonmentReportProjection()
    {
        // Route every CartAbandoned to the report document for its abandonment day (UTC).
        Identity<IEvent<CartAbandoned>>(e => e.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd"));
    }

    // The fold: count the cart, accumulate its abandoned value, and tally its SKUs — all read
    // from the fat CartAbandoned event (design.md Decision 3), never from the Cart stream itself.
    public void Apply(IEvent<CartAbandoned> e, CartAbandonmentDailyReport report)
    {
        report.AbandonedCartCount++;
        report.TotalValueAbandoned += e.Data.TotalValue;

        foreach (var line in e.Data.Lines)
        {
            report.AbandonedSkus[line.Sku] = report.AbandonedSkus.GetValueOrDefault(line.Sku) + line.Quantity;
        }
    }
}
