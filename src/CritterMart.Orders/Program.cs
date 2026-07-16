using System.Security.Cryptography;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Shopping;
using CritterMart.ServiceDefaults;
using JasperFx.Events;
using JasperFx.Events.Projections;
using JasperFx.OpenTelemetry;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
// awaiting-payment endpoint's visible deadline (computed on read — the projection is stateless).
var paymentTimeout = builder.Configuration.GetValue<TimeSpan?>("Orders:PaymentTimeout")
    ?? PaymentDeadline.Default;
builder.Services.AddSingleton(new PaymentDeadline(paymentTimeout));

// The cart inactivity window (slice 3.4): how long a cart may sit without activity before the
// scheduled CartActivityTimeout abandons it. Config-driven like the payment deadline above; one
// value feeds the AddToCart schedule, the abandonment handler's fire-and-check decision, and the
// awaiting-activity endpoint's visible deadline (computed on read — the projection is stateless).
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
        // STATELESS + generic-registered: it records the placement timestamp only; the awaiting-payment
        // endpoint adds the configured timeout on read. (Marten 9.x re-materializes instance-registered
        // projections, dropping constructor-injected state — chore/004; see OrdersAwaitingPayment remarks.)
        opts.Projections.Add<OrdersAwaitingPaymentProjection>(ProjectionLifecycle.Inline);

        // The cart-side Bruun todo-list (slice 3.4): the same stateless shape over the Cart stream —
        // records the latest activity timestamp; the awaiting-activity endpoint adds the window on read.
        opts.Projections.Add<CartsAwaitingActivityProjection>(ProjectionLifecycle.Inline);

        // ── Promotions / DCB (Workshop 003 slices 6.1/6.3/6.4; ADR 024) ─────────────────────────────
        // CouponView — the coupon DEFINITION read model (ADR 020, slice 6.1): a DEDICATED inline snapshot
        // over each coupon stream, resolving a carried couponCode to its discount + cap at checkout.
        // Configuration-as-events realized: the seeder appends CouponDefined via POST /coupons.
        opts.Projections.Snapshot<CritterMart.Orders.Promotions.CouponView>(SnapshotLifecycle.Inline);

        // The DCB registration (slice 6.3): the strong-typed CouponId tag, associated to the CouponUsage
        // boundary aggregate. This single call is the ENTIRE DCB opt-in — it triggers the tag schema on
        // orders.mt_events (ApplyAllDatabaseChangesOnStartup creates it) and lets FetchForWritingByTags<CouponUsage>
        // aggregate the net redemption count across every order stream carrying the tag. Spike-confirmed against
        // Marten 9.15.1 (no separate EnableDcb()). NOT the Polecat-flavored skill surface (DEBT row 3).
        opts.Events.RegisterTagType<CritterMart.Orders.Promotions.CouponId>("coupon")
            .ForAggregate<CritterMart.Orders.Promotions.CouponUsage>();

        // CouponUsageView — the ADVISORY per-coupon usage read model (slice 6.3): a MULTI-stream inline
        // projection folding CouponRedeemed (+1) / CouponRedemptionReleased (−1) across all order streams,
        // keyed by couponId. Inline (no async daemon runs — an async advisory view would sit empty). Distinct
        // from the never-persisted CouponUsage DCB boundary state, which is the write-time cap authority.
        opts.Projections.Add<CritterMart.Orders.Promotions.CouponUsageViewProjection>(ProjectionLifecycle.Inline);

        // The round-one ASYNC projection teaser (ADR 008, slice 3.4): registered with the async
        // lifecycle but NO AddAsyncDaemon anywhere — the daily abandonment report stays empty
        // until an on-demand rebuild materializes it from the events. That emptiness is the
        // talk's teaching beat, not a bug.
        opts.Projections.Add<CartAbandonmentReportProjection>(ProjectionLifecycle.Async);

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

        // Coupon codes are unique (Workshop 003 slice 6.1): a unique index on CouponView.Code backstops
        // the DefineCoupon pre-check under a race — the open-cart partial-unique-index precedent, minus the
        // predicate (a coupon code has no open/closed state; it is simply unique). Codes never change, so
        // uniqueness holds across the append-only definition streams.
        opts.Schema.For<CritterMart.Orders.Promotions.CouponView>().Index(x => x.Code, idx =>
        {
            idx.IsUnique = true;
        });

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

// ── Resource-server token verification (ADR 023, slice 5.10) ──────────────────────────────────────
// Orders trusts a caller's identity by validating Identity's self-signed JWT ENTIRELY OFFLINE — signature,
// issuer, audience, lifetime — against Identity's PUBLIC key, distributed as CONFIGURATION. There is NO
// Authority/MetadataAddress set, which is precisely what keeps validation offline: no JWKS fetch, no HTTP
// into Identity, per-request or at startup (the load-bearing demonstration that ADR 001's no-sync-HTTP rule
// survives real auth). The public key comes from Jwt:PublicKey (dev fallback = the shared dev key, so the
// demo works with zero wiring); the private half stays with Identity alone — Orders can verify, never mint.
var jwtPublicKey = RSA.Create();
jwtPublicKey.ImportFromPem(builder.Configuration["Jwt:PublicKey"] ?? DevJwtDefaults.DevPublicKeyPem);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // MapInboundClaims = false: keep `sub` readable AS `sub` (the default handler otherwise remaps it to
        // ClaimTypes.NameIdentifier). Paired with Identity minting via JsonWebTokenHandler, `sub` stays `sub`
        // end to end, so CustomerIdentity.CustomerId() can read user.FindFirst("sub").
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(jwtPublicKey),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? DevJwtDefaults.Issuer,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? DevJwtDefaults.Audience,
            ValidateLifetime = true
        };
    });

// The six customer-keyed endpoints are [Authorize]'d (ADR 023 hard cutover — the dev-only X-Customer-Id
// fallback is retired): JwtBearer rejects a missing/invalid/expired token with 401 before any handler runs.
// The id-keyed and automation reads (/carts/{cartId}, /orders/{orderId}, the awaiting-* todo lists) stay
// anonymous — they carry no customer identity to trust.
builder.Services.AddAuthorization();

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

// Token verification (ADR 023): populate the request's ClaimsPrincipal from a valid Bearer token so the
// [Authorize]'d customer-keyed endpoints can source the customer id from the `sub` claim
// (CustomerIdentity.CustomerId()). Placed after CORS and before endpoint mapping, per the standard
// ASP.NET middleware order.
app.UseAuthentication();
app.UseAuthorization();

app.MapWolverineEndpoints();

// Map /health (all checks) and /alive (liveness-tagged). ServiceDefaults defines these but no
// service called them before (ADR 019); dev-only by the standard Aspire posture. The Wolverine
// checks registered above reach CritterWatch over its telemetry channel regardless of this HTTP map.
app.MapDefaultEndpoints();

app.Run();

// Exposed for Alba integration tests.
public partial class Program;
