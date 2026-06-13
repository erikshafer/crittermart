using JasperFx.Events;
using Marten;
using Wolverine;
using Wolverine.CritterWatch;
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
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // How this service identifies itself on the CritterWatch dashboard (and the key of its
    // event stream in the console's store) — must be unique across monitored services.
    opts.ServiceName = "Catalog";

    // RabbitMQ solely as the CritterWatch telemetry channel — Catalog has no cross-BC
    // message flows (no UseConventionalRouting on purpose). Aspire injects the "rabbitmq"
    // connection string via WithReference.
    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
        .AutoProvision();

    // Metrics/health flow to the shared `critterwatch` queue; the console sends control
    // commands (pause listeners, chaos monkey, …) back on this service's private queue.
    opts.AddCritterWatchMonitoring(
        new Uri("rabbitmq://queue/critterwatch"),
        new Uri("rabbitmq://queue/catalog-control"));

    // Document write + audit-event append commit in one transaction.
    opts.Policies.AutoApplyTransactions();
});

builder.Services.AddWolverineHttp();

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

app.Run();

// Exposed for Alba integration tests.
public partial class Program;
