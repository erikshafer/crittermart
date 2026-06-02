using CritterMart.Orders.Cart;
using CritterMart.Orders.Order;
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

// The payment deadline (slice 4.7): how long a placed order may sit non-terminal before the
// scheduled OrderPaymentTimeout cancels it. Config-driven so the Aspire demo can shorten it
// (e.g. 30s) without recompiling; one value feeds both the PlaceOrder schedule and the
// OrdersAwaitingPayment projection's visible deadline.
var paymentTimeout = builder.Configuration.GetValue<TimeSpan?>("Orders:PaymentTimeout")
    ?? PaymentDeadline.Default;
builder.Services.AddSingleton(new PaymentDeadline(paymentTimeout));

// The cart inactivity window (slice 3.4): how long a cart may sit without activity before the
// scheduled CartActivityTimeout abandons it. Config-driven like the payment deadline above; one
// value feeds the AddToCart schedule, the abandonment handler's fire-and-check decision, and the
// CartsAwaitingActivity projection's visible deadline.
var cartActivityTimeout = builder.Configuration.GetValue<TimeSpan?>("Orders:CartActivityTimeout")
    ?? CartActivityDeadline.Default;
builder.Services.AddSingleton(new CartActivityDeadline(cartActivityTimeout));

// Time as a dependency (slice 3.4): the abandonment handler compares event timestamps against
// "now" — injected as TimeProvider so tests can drive the clock instead of waiting real time.
builder.Services.AddSingleton(TimeProvider.System);

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

        // The Bruun todo-list (slice 4.7): a second inline projection over the same Order stream.
        // Instance-registered (not generic) because the projection carries the configured payment
        // timeout — the row's visible deadline must match the schedule PlaceOrder actually sets.
        opts.Projections.Add(new OrdersAwaitingPaymentProjection(paymentTimeout), ProjectionLifecycle.Inline);

        // The cart-side Bruun todo-list (slice 3.4): the same shape over the Cart stream —
        // instance-registered with the configured inactivity window.
        opts.Projections.Add(new CartsAwaitingActivityProjection(cartActivityTimeout), ProjectionLifecycle.Inline);

        // The round-one ASYNC projection teaser (ADR 008, slice 3.4): registered with the async
        // lifecycle but NO AddAsyncDaemon anywhere — the daily abandonment report stays empty
        // until an on-demand rebuild materializes it from the events. That emptiness is the
        // talk's teaching beat, not a bug.
        opts.Projections.Add<CartAbandonmentReportProjection>(ProjectionLifecycle.Async);

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
    // Pin handler/endpoint discovery to this service's assembly. Explicit (not auto-detected)
    // so discovery is deterministic when another service's assembly is loaded in the same
    // process — e.g. the cross-BC smoke test boots Orders and Inventory side by side.
    opts.ApplicationAssembly = typeof(Program).Assembly;

    // Cross-BC messaging over RabbitMQ (ADR 003, slice 4.2). Aspire injects the "rabbitmq"
    // connection string via WithReference; conventional routing derives exchanges/queues from
    // message types, so there's no explicit topology to maintain (design.md decision 6).
    // Orders cascades ReserveStock (no local handler → routed to the broker) and handles the
    // StockReserved / StockReservationFailed replies (auto-listened by convention).
    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
        .AutoProvision()
        .UseConventionalRouting();

    opts.Policies.AutoApplyTransactions();

    // Durable local queues (slice 4.7): scheduled OrderPaymentTimeout self-messages are persisted
    // through the Marten-backed message store, so an order's payment deadline survives a service
    // restart — the timer fires (or no-ops against a settled stream) when the service comes back.
    opts.Policies.UseDurableLocalQueues();
});

builder.Services.AddWolverineHttp();

// The stubbed payment provider (slice 4.3). Round one stubs payment (vision.md non-goal), so the
// default always approves; integration tests swap a declining IPaymentProvider to exercise the
// failure branch. A real gateway integration would replace only this registration.
builder.Services.AddSingleton<CritterMart.Orders.Order.IPaymentProvider,
    CritterMart.Orders.Order.StubPaymentProvider>();

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
