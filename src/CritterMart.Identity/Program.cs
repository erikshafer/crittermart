using CritterMart.Identity.Auth;
using CritterMart.Identity.Customers;
using CritterMart.ServiceDefaults;
using JasperFx.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.EntityFrameworkCore;
using Wolverine.HealthChecks;
using Wolverine.Http;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, health checks, service discovery (ADR 004/005).
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("crittermart")
    ?? "Host=localhost;Port=5432;Database=crittermart;Username=postgres;Password=postgres";

// DEMO AFFORDANCE (Identity:EmailChangeTimeout): the EmailChange saga's deadline (Workshop 002 slices
// 5.5-5.7). Config-driven so the confirm/drop paths are demoable at speaking pace (a short default) yet
// realistic in production (the modeled duration is 24h). Mirrors Inventory's ReplenishDeadline binding.
var emailChangeTimeout = builder.Configuration.GetValue<TimeSpan?>("Identity:EmailChangeTimeout")
    ?? EmailChangeDeadline.Default;
builder.Services.AddSingleton(new EmailChangeDeadline(emailChangeTimeout));

builder.Host.UseWolverine(opts =>
{
    // Pin handler/endpoint discovery to this service's assembly (deterministic when another service's
    // assembly is loaded in the same process, e.g. an Alba host).
    opts.ApplicationAssembly = typeof(Program).Assembly;

    // How this service identifies itself on the CritterWatch dashboard.
    opts.ServiceName = "Identity";

    // ── The whole point of the spike (ADR 009) ──────────────────────────────────────────────────
    // Identity is the ONE service that is NOT event-sourced. It is a deliberately boring EF Core CRUD
    // registry on the SAME shared Postgres as the three Marten services — proof that Wolverine's
    // handler model is persistence-agnostic: the same static endpoint/Handle shape and the same
    // transactional outbox, over a DbContext instead of an IDocumentSession. Per ADR 023 it is NOW
    // ALSO the system's auth ISSUER (ASP.NET Core Identity user store + a self-validated JWT — wired on
    // builder.Services after this block) — still relational CRUD, still no event sourcing: auth EXTENDS
    // the boring-CRUD foil, it does not reverse it.

    // Wolverine's durable inbox/outbox lives in Postgres under Identity's OWN schema — schema-per-
    // service (ADR 002), the EF-Core mirror of the Marten services' opts.DatabaseSchemaName.
    opts.PersistMessagesWithPostgresql(connectionString, "identity");

    // Idiomatic one-call EF Core integration: registers CustomerDbContext, maps the Wolverine envelope
    // tables into it (so an entity write and its outgoing messages share one transaction), pins the
    // options lifetime, and activates the transactional middleware.
    opts.Services.AddDbContextWithWolverineIntegration<CustomerDbContext>(
        x => x.UseNpgsql(connectionString));

    // Weasel diffs the DbContext (customers + envelope tables) against the live DB and applies missing
    // DDL — the EF-Core analogue of the Marten services' ApplyAllDatabaseChangesOnStartup(). Declares
    // the EF schema as a Wolverine-managed resource; UseResourceSetupOnStartup() below applies it.
    opts.UseEntityFrameworkCoreWolverineManagedMigrations();

    // Cross-BC messaging over RabbitMQ (ADR 003), wired exactly like the Marten services. Register-
    // Customer cascades CustomerRegistered to the outbox; with no local handler, conventional routing
    // publishes it to its own exchange — but NOTHING consumes it yet, so the spike stays fully
    // isolated (a consumer would make Identity a kept bounded context needing a workshop first).
    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
        .AutoProvision()
        .UseConventionalRouting();

    // Metrics/health flow to the shared `critterwatch` queue; the console sends control commands back
    // on this service's private queue. Identity shows up as a 4th monitored node on the dashboard.
    opts.AddCritterWatchMonitoring(
        new Uri("rabbitmq://queue/critterwatch"),
        new Uri("rabbitmq://queue/identity-control"));

    opts.Policies.AutoApplyTransactions();
});

// ── Real authentication (ADR 023) ────────────────────────────────────────────────────────────────
// Identity as the auth ISSUER. ASP.NET Core Identity gives the user store + credential handling
// (UserManager for hashing/validation, SignInManager for the password check) over the SAME
// CustomerDbContext / `identity` schema — so the Identity user, the Customer row, and the outbox all
// share one transaction.
//
// AddIdentityCore (NOT AddIdentity): AddIdentity drags in cookie authentication + its UI handlers,
// exactly what ADR 023 rejects (bearer, not cookie). AddIdentityCore gives just UserManager;
// AddSignInManager adds SignInManager (its CheckPasswordSignInAsync is slice 5.9's password check);
// AddEntityFrameworkStores<CustomerDbContext> backs both on EF Core. No roles are added — authZ is out
// of scope (Workshop 002 § 8 Q16) — so the store is user-only, matching CustomerDbContext deriving
// IdentityUserContext (not IdentityDbContext).
builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        // A demo-workable password policy: 8+ chars with a lowercase letter and a digit, no required
        // symbol or uppercase (so a memorable demo password like "critter1" passes while "weak" fails
        // slice 5.8's weak-password path). Tighten via config in prod.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        // Emails are the login key; require them unique at the Identity layer too (the DB backstop is
        // ux_customers_email on the customers table — see EnsureEmailUniqueIndex below).
        options.User.RequireUniqueEmail = true;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<CustomerDbContext>();

// SignInManager's constructor depends on IAuthenticationSchemeProvider; AddAuthentication() registers it.
// Identity issues tokens but does NOT gate ITSELF with bearer auth (no default scheme, no [Authorize]) —
// registering the scheme provider is purely to satisfy SignInManager's DI, not to protect endpoints.
builder.Services.AddAuthentication();

// The issued-token settings (issuer/audience/lifetime), bound from the `Jwt` config section with dev
// fallbacks, and the JwtTokenIssuer that mints with Identity's private RSA key (Jwt:PrivateKey ?? dev key).
builder.Services.AddSingleton(new JwtSettings
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? DevJwtDefaults.Issuer,
    Audience = builder.Configuration["Jwt:Audience"] ?? DevJwtDefaults.Audience,
    AccessTokenLifetime = builder.Configuration.GetValue<TimeSpan?>("Jwt:AccessTokenLifetime")
        ?? new JwtSettings().AccessTokenLifetime
});
builder.Services.AddSingleton<JwtTokenIssuer>();

// Build all Wolverine-managed resources on startup — the identity-schema message-storage tables AND
// the EF managed-migration schema declared above. The EF-Core counterpart of the Marten services'
// ApplyAllDatabaseChangesOnStartup(); Aspire just runs the app, so the schema must self-apply here.
builder.Host.UseResourceSetupOnStartup();

builder.Services.AddWolverineHttp();

// Expose the Wolverine runtime + listener health to ASP.NET health checks (ADR 019), as the Marten
// services do — this is what CritterWatch's per-service "Health checks" panel reads.
builder.Services.AddHealthChecks()
    .AddWolverine()
    .AddWolverineListeners();

// Swagger UI over the (OpenAPI-described) Wolverine.Http endpoints — a demo/devex affordance.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// The duplicate-email guard's DATABASE backstop. RegisterCustomer.ValidateAsync returns the friendly
// 409, but a unique index on `email` is what closes the check-then-insert race the app-level check can't
// (two concurrent registrations both passing the guard before either commits). Weasel's EF-managed
// migrations create the `customers` table but NOT secondary indexes (they migrate tables/columns/PKs/FKs
// only — confirmed against the Wolverine EF-Core docs + a live schema check), so an EF `HasIndex` would be
// silently dropped. The index is therefore applied here as idempotent DDL. ApplicationStarted fires AFTER
// every hosted service's StartAsync — including Weasel's resource setup that creates the table — and runs
// synchronously, so the index is in place before the host reports started (and before any Alba scenario
// runs). The stored email is already normalized (trim + lowercase), so the index is case-insensitive.
app.Lifetime.ApplicationStarted.Register(EnsureEmailUniqueIndex);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

// Apply the default CORS policy (origins from config; ServiceDefaults). No-op for same-origin and
// Origin-less requests (e.g. Alba integration tests), so it is safe in every host.
app.UseCors();

app.MapWolverineEndpoints();

// Map /health (all checks) and /alive (liveness-tagged).
app.MapDefaultEndpoints();

app.Run();

// Applies the email unique index out-of-band (Weasel migrates the EF table but not its indexes — see the
// comment at the ApplicationStarted registration above and in CustomerDbContext). Idempotent so a restart
// against an existing schema is a no-op; the `identity`-schema `customers` table exists by the time this
// runs because ApplicationStarted fires after Weasel's resource-setup hosted service.
void EnsureEmailUniqueIndex()
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_customers_email ON identity.customers (email)");
}

// Exposed for Alba integration tests.
public partial class Program;
