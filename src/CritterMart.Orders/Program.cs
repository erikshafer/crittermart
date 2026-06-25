using CritterMart.Orders.Analytics;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Shopping;
using CritterMart.Orders.Spike;
using JasperFx.Events;
using JasperFx.Events.Daemon;
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

var martenConfig = builder.Services.AddMarten(opts =>
    {
        opts.Connection(connectionString);

        // Schema-per-service (ADR 002): Orders owns the `orders` schema.
        opts.DatabaseSchemaName = "orders";
        opts.Events.DatabaseSchemaName = "orders";

        // Cart streams are keyed by a generated cartId (a string).
        opts.Events.StreamIdentity = StreamIdentity.AsString;

        // The Cart aggregate — the domain WRITE model (ADR 020), a self-aggregating immutable
        // record materialized as an inline snapshot (ADR 008). It is the FetchForWriting/StartStream target
        // and is never served over HTTP. The open-cart invariant index (below) lives on it.
        opts.Projections.Snapshot<Cart>(SnapshotLifecycle.Inline);

        // CartView — the cart's READ model (ADR 020): a DEDICATED inline projection the storefront binds,
        // decoupled from the Cart aggregate so the read path never touches the write model.
        opts.Projections.Snapshot<CartView>(SnapshotLifecycle.Inline);

        // The Order aggregate — the domain WRITE model (ADR 020) and the PMvH process-manager state, a
        // self-aggregating inline snapshot like Cart. It is the FetchForWriting/StartStream target on the
        // order's write paths (PlaceOrder + the cross-BC outcome handlers) and is never served over HTTP.
        opts.Projections.Snapshot<Order>(SnapshotLifecycle.Inline);

        // OrderStatusView — the order's READ model (ADR 020): a DEDICATED inline projection W3/W4 bind via
        // GET /orders/{orderId}, decoupled from the Order aggregate so the read path never touches the
        // write model. Wire shape preserved (the ADR 020/021 Order rollout — this replaced the former
        // OrderStatusViewProjection : SingleStreamProjection class with a self-aggregating record snapshot).
        opts.Projections.Snapshot<OrderStatusView>(SnapshotLifecycle.Inline);

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

        // ── CW-TELEMETRY SPIKE projections (research/cw-telemetry-spike) — NOT round-one baseline ──
        // Two ASYNC read models that only MOVE when the daemon is on (Cw:Telemetry, below). They give
        // CritterWatch the live async telemetry the baseline never produces: ProductSalesLeaderboard is
        // a fan-out multi-stream projection (one OrderPlaced → one doc per SKU); OrderLineItemsProjection
        // is a flat SQL table (orders.order_line_items). Registered unconditionally — exactly like the
        // CartAbandonment teaser above, they sit inert until a daemon turns. See docs/research/.
        opts.Projections.Add<ProductSalesLeaderboardProjection>(ProjectionLifecycle.Async);
        opts.Projections.Add<OrderLineItemsProjection>(ProjectionLifecycle.Async);

        // The open-cart invariant lives on the Cart AGGREGATE (ADR 020 — it is a write-side rule):
        // a partial-unique index on Cart.CustomerId, scoped to open carts, enforces "one open cart
        // per customer" at the DB. The write paths resolve the customer's open cart against this index; a
        // checked-out (4.1) or abandoned (3.4) cart has IsOpen=false and frees the customer to start a fresh one.
        opts.Schema.For<Cart>().Index(x => x.CustomerId, idx =>
        {
            idx.IsUnique = true;
            idx.Predicate = "(data ->> 'IsOpen')::boolean = true";
        });

        // The read model resolves by customer too (GET /carts/mine) — a plain (non-unique) index serves
        // that query; uniqueness is the aggregate's invariant, not the read model's.
        opts.Schema.For<CartView>().Index(x => x.CustomerId);

        // The order read model resolves by customer too (GET /orders/mine — the "My Orders" list, Gap #3).
        // The mirror of the CartView index above: a plain (non-unique) index over OrderStatusView.CustomerId
        // serves the customer-keyed list query. NOT unique — a customer has many orders (unlike the cart's
        // one-open-cart invariant, which is the write-side partial-unique index on the Cart aggregate above).
        opts.Schema.For<OrderStatusView>().Index(x => x.CustomerId);

        // The consumer-local customer read model (slice 5.4): a plain Marten document upserted by
        // CustomerRegisteredHandler when the CustomerRegistered Published-Language event arrives from
        // Identity over RabbitMQ. Registered here so Marten's schema management creates the
        // `orders.mt_doc_localcustomerview` table on startup alongside the other order documents.
        // All reads are primary-key loads (session.LoadAsync<LocalCustomerView>(customerId)) — no index.
        opts.Schema.For<CritterMart.Orders.Customers.LocalCustomerView>();

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

// ── CW-TELEMETRY SPIKE: async daemon (research/cw-telemetry-spike) — NOT round-one baseline ────────
// ADR 008 keeps the baseline daemon-free; the spike flips it ON behind Cw:Telemetry so the async
// projections above (plus the previously-inert CartAbandonment teaser) actually run and stream
// shard / lag / rebuild telemetry to CritterWatch — letting you WATCH lag climb then drain in the
// console. Solo mode = single node, right for the single-instance Aspire host. Flag OFF = exact
// baseline behaviour (the "before" CritterWatch picture). See docs/research/cw-telemetry-fodder.md.
var cwTelemetry = builder.Configuration.GetValue<bool>("Cw:Telemetry");
if (cwTelemetry)
{
    martenConfig.AddAsyncDaemon(DaemonMode.Solo);
}

// Expose the toggle so the PlaceOrder endpoint can decide whether to broadcast OrderPlacedSignal.
builder.Services.AddSingleton(new CwTelemetryFlag(cwTelemetry));

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

    // How this service identifies itself on the CritterWatch dashboard (and the key of its
    // event stream in the console's store) — must be unique across monitored services.
    opts.ServiceName = "Orders";

    // Metrics/health flow to the shared `critterwatch` queue; the console sends control
    // commands (pause projections, DLQ replay, …) back on this service's private queue.
    opts.AddCritterWatchMonitoring(
        new Uri("rabbitmq://queue/critterwatch"),
        new Uri("rabbitmq://queue/orders-control"));

    opts.Policies.AutoApplyTransactions();

    // Durable local queues (slice 4.7): scheduled OrderPaymentTimeout self-messages are persisted
    // through the Marten-backed message store, so an order's payment deadline survives a service
    // restart — the timer fires (or no-ops against a settled stream) when the service comes back.
    opts.Policies.UseDurableLocalQueues();
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

// The stubbed payment provider (slice 4.3). Round one stubs payment (vision.md non-goal), so the
// default always approves; integration tests swap a declining IPaymentProvider to exercise the
// failure branch. A real gateway integration would replace only this registration.
//
// DEMO AFFORDANCE (Payment:DeclineOverAmount): when set, the stub declines any order whose total
// exceeds the threshold, making the slice-4.6 payment-DECLINE path (cancel + compensating ReleaseStock)
// triggerable in a LIVE demo without swapping providers. UNSET (the default here and in every test) →
// approve everything, exactly as before. The demo value is injected by the AppHost
// (src/CritterMart.AppHost/Program.cs); how to change/remove it is in docs/demo-runbook.md § Payment
// decline. Mirrors the PaymentDeadline / CartActivityDeadline config-singleton pattern above.
var declineOverAmount = builder.Configuration.GetValue<decimal?>("Payment:DeclineOverAmount");
builder.Services.AddSingleton(new PaymentDeclinePolicy(declineOverAmount));

// DEMO AFFORDANCE (Payment:AuthDelay): when set, the stub sleeps this long before returning a
// decision — so the stock_reserved → payment_authorized transition is visible at speaking pace.
// Default is zero (no delay) everywhere except the AppHost's demo wiring.
var paymentAuthDelay = builder.Configuration.GetValue<TimeSpan?>("Payment:AuthDelay")
    ?? PaymentAuthDelay.Default;
builder.Services.AddSingleton(new PaymentAuthDelay(paymentAuthDelay));

builder.Services.AddSingleton<CritterMart.Orders.Ordering.IPaymentProvider,
    CritterMart.Orders.Ordering.StubPaymentProvider>();

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
