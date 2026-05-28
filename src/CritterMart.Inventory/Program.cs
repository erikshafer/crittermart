using CritterMart.Inventory.Stock;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

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
    opts.Policies.AutoApplyTransactions();
});

builder.Services.AddWolverineHttp();

var app = builder.Build();

app.MapWolverineEndpoints();

app.Run();

// Exposed for Alba integration tests.
public partial class Program;
