using Alba;
using Testcontainers.PostgreSql;
using Wolverine;
using Xunit;

namespace CritterMart.Inventory.Tests;

// Boots the Inventory service against a throwaway Postgres container, shared across
// the test collection. Connection string injected via the env-var config provider.
// Slice 4.2 added the RabbitMQ transport; tests stub all external transports so the host
// boots with no broker — Wolverine still records cascaded messages in tracked sessions.
public class InventoryAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    // The storefront origin the frontend-bootstrap PR pins (Vite dev server on 5273). Injected into
    // config via the exact key the AppHost uses in production (Cors__AllowedOrigins__0) so the CORS
    // preflight test asserts the real config-driven allowlist, not the Development fallback.
    public const string SpaOrigin = "http://localhost:5273";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", _postgres.GetConnectionString());
        // A dummy connection so UseRabbitMqUsingNamedConnection("rabbitmq") resolves at config
        // time; the transport is stubbed below, so nothing actually connects.
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", "amqp://guest:guest@localhost:5672");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", SpaOrigin);
        Host = await AlbaHost.For<Program>(x =>
            x.ConfigureServices(services => services.DisableAllExternalWolverineTransports()));
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

[CollectionDefinition("inventory")]
public class InventoryCollection : ICollectionFixture<InventoryAppFixture>;
