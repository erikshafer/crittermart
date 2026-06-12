using CritterWatch.Services.Hosting;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// User secrets normally load only in Development. Load them explicitly so the trial license
// (JasperFx:LicenseKey) is still found when this host runs as Production locally — the
// console's dev-environment fallback ("Development" tier, expires never) otherwise masks
// whether the real key validates. No-op when no secrets store exists.
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

// CritterWatch keeps its own event store and projections in a dedicated database on the
// shared Postgres container — its tables stay out of the crittermart demo database
// (schema-per-service in ADR 002 governs CritterMart's services, not third-party tooling).
var postgresConnection = builder.Configuration.GetConnectionString("critterwatch")
    ?? "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

// Registers Wolverine, Marten, SignalR, and all CritterWatch services. The 30-day trial
// license is read from JasperFx:LicenseKey (user secrets locally); without it the console
// silently falls back to the read-only Free tier.
builder.AddCritterWatch(postgresConnection, opts =>
    {
        // Same broker the services publish telemetry to. Dead-letter queueing is disabled for
        // the console's own traffic — telemetry is fire-and-forget, not worth DLQ ceremony.
        opts.UseRabbitMq(new Uri(builder.Configuration.GetConnectionString("rabbitmq") ?? "amqp://localhost"))
            .DisableDeadLetterQueueing()
            .AutoProvision();

        // The well-known queue every monitored service sends metrics/events to. Sequential —
        // telemetry from one service must apply in order against its event stream.
        opts.ListenToRabbitQueue("critterwatch").Sequential();
    },
    // Single local console node: clustered mode (the 0.9.1 default) requires a sharded queue
    // topology (UseShardedRabbitQueues) and refuses to start without one. The Sequential
    // listener above already gives one node the same per-service ordering guarantee.
    enableClusterPartitioning: false);

var app = builder.Build();

// Maps the Wolverine HTTP endpoints, the SignalR hub at /api/messages, and the embedded SPA.
app.UseCritterWatch();

await app.RunAsync();
