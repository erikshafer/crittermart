using CritterMart.Orders.Ordering;
using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Weasel.Postgresql.Tables;

namespace CritterMart.Orders.Analytics;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline. Async; only runs with
//  the daemon on (Cw:Telemetry). See docs/research/cw-telemetry-fodder.md.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// A FLAT-TABLE read model — raw SQL rows, not a JSONB document: one row per (order, line) in
// orders.order_line_items, the kind of table a BI tool or EF Core report would consume.
//
// Implemented as an EventProjection (not the declarative FlatTableProjection) for two reasons the
// DSL can't satisfy: (1) we want event METADATA — placed_at (the event timestamp) and event_sequence
// — which FlatTableProjection deliberately can't reach; (2) one OrderPlaced must produce MANY rows
// (one per line), which the per-event single-row DSL doesn't fan out to. QueueSqlCommand batches the
// inserts into the projection's unit of work.
//
// Its real job here is to probe whether CritterWatch's Store Inspector / Event Store Explorer renders
// a non-document projection AT ALL — a question the baseline (JSONB documents only) can't ask.
public partial class OrderLineItemsProjection : EventProjection
{
    public OrderLineItemsProjection()
    {
        // Marten manages this table's schema (create + migrate) alongside the Orders store objects.
        var table = new Table("orders.order_line_items");
        table.AddColumn<string>("order_id").AsPrimaryKey();
        table.AddColumn<string>("sku").AsPrimaryKey();
        table.AddColumn<string>("product_name").NotNull();
        table.AddColumn<int>("quantity").NotNull();
        table.AddColumn<decimal>("unit_price").NotNull();
        table.AddColumn<decimal>("line_total").NotNull();
        table.AddColumn<string>("customer_id").NotNull();
        table.AddColumn<DateTimeOffset>("placed_at").NotNull();
        table.AddColumn<long>("event_sequence").NotNull();
        SchemaObjects.Add(table);

        // Clear the table on rebuild so a "rebuild this projection" from CritterWatch is clean.
        Options.DeleteDataInTableOnTeardown(table.Identifier.QualifiedName);
    }

    public void Project(IEvent<OrderPlaced> e, IDocumentOperations ops)
    {
        foreach (var line in e.Data.Items)
        {
            // ON CONFLICT DO NOTHING keeps the insert idempotent against at-least-once replays and
            // async-daemon rebuilds — the (order, sku) pair is the natural key.
            ops.QueueSqlCommand(
                """
                INSERT INTO orders.order_line_items
                    (order_id, sku, product_name, quantity, unit_price, line_total, customer_id, placed_at, event_sequence)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT (order_id, sku) DO NOTHING
                """,
                e.Data.OrderId,
                line.Sku,
                line.Name,
                line.Quantity,
                line.Price,
                line.Quantity * line.Price,
                e.Data.CustomerId,
                e.Timestamp,
                e.Sequence);
        }
    }
}
