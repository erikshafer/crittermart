using Alba;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolverine;
using Xunit;

namespace CritterMart.Orders.Tests;

// Boots the Orders service against a throwaway Postgres container, shared across the test
// collection. Connection string injected via the env-var config provider. Mirrors
// InventoryAppFixture — the established event-sourced-service integration-test pattern.
// Slice 4.2 added the RabbitMQ transport; tests stub all external transports so the host
// boots with no broker — Wolverine still records cascaded messages in tracked sessions.
public class OrdersAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    // The storefront origin the frontend-bootstrap PR pins (Vite dev server on 5273). Injected into
    // config below via the exact key the AppHost uses in production (Cors__AllowedOrigins__0) so the
    // CORS preflight test asserts the real config-driven allowlist, not the Development fallback.
    public const string SpaOrigin = "http://localhost:5273";

    // Slice 3.4: the abandonment handler reads "now" from an injected TimeProvider so tests can
    // drive the clock across the inactivity window instead of waiting real time. This settable
    // provider overrides Program.cs's TimeProvider.System registration (last registration wins);
    // only CartAbandonmentHandler consumes it, so other tests are unaffected. Cart-abandonment
    // tests reset it to real "now" at their start.
    public TestTimeProvider Time { get; } = new();

    // The container connection string, kept for the boundary-aggregate-safe reset below.
    private string _connectionString = string.Empty;

    // Clears every document + event + DCB-tag table in the `orders` schema via raw SQL. Used instead of
    // Marten's Clean.DeleteAllDocumentsAsync because that enumerates ALL active document features to build
    // their schemas — and the DCB boundary aggregate CouponUsage (a `[BoundaryAggregate]`, intentionally
    // id-less per Marten's own guidance) has no Id, so once it has been materialized by a redemption the
    // enumeration throws InvalidDocumentException store-wide (a Marten 9.15.1 DCB/Clean rough edge). TRUNCATEing
    // the tables directly sidesteps the feature enumeration entirely. Also clears mt_event_tag_coupon, which
    // DeleteAllEventDataAsync leaves behind (its stale rows would collide with the next test's tagged appends).
    public async Task ResetAllDataAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN
                SELECT tablename FROM pg_tables
                WHERE schemaname = 'orders'
                  AND (tablename LIKE 'mt_doc_%' OR tablename LIKE 'mt_events%'
                       OR tablename = 'mt_streams' OR tablename = 'mt_event_tag_coupon')
              LOOP
                EXECUTE 'TRUNCATE TABLE orders.' || quote_ident(r.tablename) || ' CASCADE';
              END LOOP;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", _postgres.GetConnectionString());
        // A dummy connection so UseRabbitMqUsingNamedConnection("rabbitmq") resolves at config
        // time; the transport is stubbed below, so nothing actually connects.
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", "amqp://guest:guest@localhost:5672");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", SpaOrigin);
        Host = await AlbaHost.For<Program>(x =>
            x.ConfigureServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                services.AddSingleton<TimeProvider>(Time);
            }));
    }

    public async Task DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", null);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", null);
    }
}

// A clock the tests own: GetUtcNow() returns whatever Now was last set to. Plain BCL subclass —
// no package, no mock framework.
public class TestTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}

[CollectionDefinition("orders")]
public class OrdersCollection : ICollectionFixture<OrdersAppFixture>;
