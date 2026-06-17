using CritterMart.Inventory.Stock;
using JasperFx.Events;
using JasperFx.Events.Projections;
using JasperFx.OpenTelemetry;
using Marten;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.HealthChecks;
using Wolverine.Http;
using Wolverine.Marten;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, health checks, service discovery (ADR 004/005).
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("crittermart")
    ?? "Host=localhost;Port=5432;Database=crittermart;Username=postgres;Password=postgres";

builder.Services.AddMarten(opts =>
    {
        opts.Connection(connectionString);

        // Schema-per-service (ADR 002): Inventory owns the `inventory` schema.
        opts.DatabaseSchemaName = "inventory";
        opts.Events.DatabaseSchemaName = "inventory";

        // Stock streams are keyed per SKU.
        opts.Events.StreamIdentity = StreamIdentity.AsString;

        // The StockLevel aggregate — a SKU's domain WRITE model (ADR 020), a self-aggregating immutable
        // record materialized as an inline snapshot (ADR 008; no async daemon). It is the FetchForWriting
        // target on the four stock write paths and is never served over HTTP.
        opts.Projections.Snapshot<StockLevel>(SnapshotLifecycle.Inline);

        // StockLevelView — a SKU's READ model (ADR 020): a DEDICATED inline projection served over
        // GET /stock/{sku}, decoupled from the StockLevel aggregate so the read path never touches the
        // write model. Wire shape preserved (the ADR 020 Stock rollout — this replaced the former
        // StockLevelViewProjection : SingleStreamProjection class with a self-aggregating record snapshot).
        opts.Projections.Snapshot<StockLevelView>(SnapshotLifecycle.Inline);

        // OpenTelemetry (ADR 005, completing chore/002's deferred half): verbose connection
        // tracking emits a `marten.connection` span per connection AND tags every write op (the
        // event appends) after a successful commit, so the appends show up inside the trace next
        // to the HTTP/Wolverine spans. TrackEventCounters() exports the `marten.event.append`
        // metric (tagged event_type). ServiceDefaults registers the matching "Marten" meter so the
        // counter actually reaches the dashboard. Verbose is the teaching level — the demo wants
        // the writes visible (a production setup would likely use TrackLevel.Normal).
        opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose;
        opts.OpenTelemetry.TrackEventCounters();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Pin handler/endpoint discovery to this service's assembly. Explicit (not auto-detected)
    // so discovery is deterministic when another service's assembly is loaded in the same
    // process — e.g. the cross-BC smoke test boots Orders and Inventory side by side.
    opts.ApplicationAssembly = typeof(Program).Assembly;

    // How this service identifies itself on the CritterWatch dashboard (and the key of its
    // event stream in the console's store) — must be unique across monitored services.
    opts.ServiceName = "Inventory";

    // Cross-BC messaging over RabbitMQ (ADR 003, slice 4.2). Aspire injects the "rabbitmq"
    // connection string via WithReference; conventional routing derives exchanges/queues from
    // message types (design.md decision 6). Inventory handles ReserveStock (auto-listened by
    // convention) and cascades the StockReserved / StockReservationFailed reply back to Orders.
    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
        .AutoProvision()
        .UseConventionalRouting();

    // Metrics/health flow to the shared `critterwatch` queue; the console sends control
    // commands (pause projections, DLQ replay, …) back on this service's private queue.
    opts.AddCritterWatchMonitoring(
        new Uri("rabbitmq://queue/critterwatch"),
        new Uri("rabbitmq://queue/inventory-control"));

    opts.Policies.AutoApplyTransactions();
});

builder.Services.AddWolverineHttp();

// Expose the Wolverine runtime + listener health to ASP.NET health checks (ADR 019). The bus check
// ("wolverine") reports whether the runtime started and is uncancelled; the listener check
// ("wolverine-listeners") reflects listener state (accepting / too-busy / latched / stopped). This
// is what CritterWatch's per-service "Health checks" panel reads — and it makes the console's
// chaos-monkey listener latching surface as a health change. Registered after UseWolverine per the
// WolverineFx.HealthChecks guidance.
builder.Services.AddHealthChecks()
    .AddWolverine()
    .AddWolverineListeners();

// Swagger UI over the (OpenAPI-described) Wolverine.Http endpoints — a demo/devex affordance.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Send the root to the Swagger UI (302) so localhost:<port>/ lands on the docs.
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

// Apply the default CORS policy (origins from config; ServiceDefaults). Lets the round-two
// SPA call this service's endpoints cross-origin (ADR 006/015). No-op for same-origin and
// Origin-less requests (e.g. Alba integration tests), so it is safe in every host.
app.UseCors();

app.MapWolverineEndpoints();

// Map /health (all checks) and /alive (liveness-tagged). ServiceDefaults defines these but no
// service called them before (ADR 019); dev-only by the standard Aspire posture. The Wolverine
// checks registered above reach CritterWatch over its telemetry channel regardless of this HTTP map.
app.MapDefaultEndpoints();

app.Run();

// Exposed for Alba integration tests.
public partial class Program;
