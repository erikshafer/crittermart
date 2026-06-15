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

    // The storefront origin the frontend-bootstrap PR pins (Vite dev server on 5173). Injected into
    // config below via the exact key the AppHost uses in production (Cors__AllowedOrigins__0) so the
    // CORS preflight test asserts the real config-driven allowlist, not the Development fallback.
    public const string SpaOrigin = "http://localhost:5173";

    // Slice 3.4: the abandonment handler reads "now" from an injected TimeProvider so tests can
    // drive the clock across the inactivity window instead of waiting real time. This settable
    // provider overrides Program.cs's TimeProvider.System registration (last registration wins);
    // only CartAbandonmentHandler consumes it, so other tests are unaffected. Cart-abandonment
    // tests reset it to real "now" at their start.
    public TestTimeProvider Time { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
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
