using JasperFx.Events;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

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

app.MapWolverineEndpoints();

app.Run();

// Exposed for Alba integration tests.
public partial class Program;
