using JasperFx.Events;
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

        // Schema-per-service (ADR 002): Catalog owns the `catalog` schema for both
        // its documents and its audit event streams on the shared database.
        opts.DatabaseSchemaName = "catalog";
        opts.Events.DatabaseSchemaName = "catalog";

        // SKU is the natural identity; the per-product audit stream is keyed by SKU.
        opts.Events.StreamIdentity = StreamIdentity.AsString;

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
    // How this service identifies itself on the CritterWatch dashboard (and the key of its
    // event stream in the console's store) — must be unique across monitored services.
    opts.ServiceName = "Catalog";

    // RabbitMQ as the CritterWatch telemetry channel. On `main` Catalog has no cross-BC message
    // flows (no UseConventionalRouting on purpose). Aspire injects the "rabbitmq" connection string
    // via WithReference.
    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
        .AutoProvision()
        // CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline: conventional
        // routing so Catalog can subscribe to the OrderPlacedSignal broadcast (fan-out target #2),
        // giving it its first inbound Topology edge in the console. See docs/research/.
        .UseConventionalRouting();

    // Metrics/health flow to the shared `critterwatch` queue; the console sends control
    // commands (pause listeners, chaos monkey, …) back on this service's private queue.
    opts.AddCritterWatchMonitoring(
        new Uri("rabbitmq://queue/critterwatch"),
        new Uri("rabbitmq://queue/catalog-control"));

    // Document write + audit-event append commit in one transaction.
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
