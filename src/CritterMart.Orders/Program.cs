using CritterMart.Orders.Cart;
using CritterMart.Orders.Order;
using JasperFx.Events;
using JasperFx.Events.Projections;
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

        // Schema-per-service (ADR 002): Orders owns the `orders` schema.
        opts.DatabaseSchemaName = "orders";
        opts.Events.DatabaseSchemaName = "orders";

        // Cart streams are keyed by a generated cartId (a string).
        opts.Events.StreamIdentity = StreamIdentity.AsString;

        // Inline single-stream projections — the readable cart and order (ADR 008; no daemon).
        opts.Projections.Add<CartViewProjection>(ProjectionLifecycle.Inline);
        opts.Projections.Add<OrderStatusViewProjection>(ProjectionLifecycle.Inline);

        // One computed index on CartView.CustomerId that serves the open-cart resolution
        // query AND, scoped to open carts, enforces "one open cart per customer" at the DB
        // (design.md decision 2). The predicate is trivial today (every cart is open) but
        // survives unchanged once checkout (4.1) / abandon (3.4) flip IsOpen.
        opts.Schema.For<CartView>().Index(x => x.CustomerId, idx =>
        {
            idx.IsUnique = true;
            idx.Predicate = "(data ->> 'IsOpen')::boolean = true";
        });
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
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
