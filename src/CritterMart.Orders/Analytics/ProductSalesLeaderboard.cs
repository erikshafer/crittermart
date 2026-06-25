using CritterMart.Orders.Ordering;
using JasperFx.Events;
using Marten.Events.Projections;

namespace CritterMart.Orders.Analytics;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline. ADR 008 (no async
//  daemon) still holds on `main`; this projection only MOVES when the daemon is flipped on behind
//  the Cw:Telemetry flag. See docs/research/cw-telemetry-fodder.md.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// A per-SKU sales leaderboard. Where CartAbandonmentReport folds MANY Cart streams into ONE document
// (many-streams → one-doc), this is the mirror topology — ONE OrderPlaced event fans OUT to MANY
// documents (one-event → many-docs), one ProductSalesLeaderboard per SKU on the order. That fan-out
// is exactly the multi-stream shape CritterWatch's Projection Stepper "Stream Slice" / "Tag Query"
// source modes exist for but that the baseline (inline-only, no daemon) never produces.
public class ProductSalesLeaderboard
{
    public string Id { get; set; } = string.Empty;   // the SKU — the document identity
    public string ProductName { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal GrossRevenue { get; set; }
    public int OrderCount { get; set; }               // orders that included this SKU
}

// `partial` is load-bearing (Marten 9 source-gen convention, same as CartAbandonmentReportProjection):
// conventional Apply methods are dispatched by the compile-time JasperFx generator, which extends a
// partial class. Without it the host refuses to boot with InvalidProjectionException.
public partial class ProductSalesLeaderboardProjection
    : MultiStreamProjection<ProductSalesLeaderboard, string>
{
    public ProductSalesLeaderboardProjection()
    {
        // Fan-out routing: each OrderPlaced is routed to one document per DISTINCT SKU it contains.
        // Identities<T> (plural) is Marten's one-event-updates-many-documents primitive.
        Identities<IEvent<OrderPlaced>>(e => e.Data.Items.Select(i => i.Sku).Distinct().ToList());
    }

    // view.Id is the SKU this particular document tracks (Marten assigns the routed identity to the
    // document key), so accumulate only the order line(s) matching it.
    public void Apply(IEvent<OrderPlaced> e, ProductSalesLeaderboard view)
    {
        var lines = e.Data.Items.Where(i => i.Sku == view.Id).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(view.ProductName))
            {
                view.ProductName = line.Name;
            }

            view.UnitsSold += line.Quantity;
            view.GrossRevenue += line.Quantity * line.Price;
        }

        view.OrderCount++;
    }
}
