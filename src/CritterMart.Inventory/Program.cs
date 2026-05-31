using CritterMart.Inventory.Stock;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Wolverine;
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

        // Inline single-stream projection — the readable stock level (ADR 008; no async daemon).
        opts.Projections.Add<StockLevelViewProjection>(ProjectionLifecycle.Inline);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Pin handler/endpoint discovery to this service's assembly. Explicit (not auto-detected)
    // so discovery is deterministic when another service's assembly is loaded in the same
    // process — e.g. the cross-BC smoke test boots Orders and Inventory side by side.
    opts.ApplicationAssembly = typeof(Program).Assembly;

    // Cross-BC messaging over RabbitMQ (ADR 003, slice 4.2). Aspire injects the "rabbitmq"
    // connection string via WithReference; conventional routing derives exchanges/queues from
    // message types (design.md decision 6). Inventory handles ReserveStock (auto-listened by
    // convention) and cascades the StockReserved / StockReservationFailed reply back to Orders.
    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
        .AutoProvision()
        .UseConventionalRouting();

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
