using Alba;
using Testcontainers.PostgreSql;
using Wolverine;
using Xunit;

namespace CritterMart.Catalog.Tests;

// Boots the Catalog service against a throwaway Postgres container, shared across
// the test collection. The connection string is injected via the environment-variable
// config provider (WebApplicationBuilder adds it after appsettings.json, so it wins).
// CritterWatch (commit 2b127f4) added RabbitMQ to Catalog for telemetry — the dummy
// connection string + DisableAllExternalWolverineTransports keeps the host bootable
// without a live broker, mirroring the Inventory and Orders fixtures.
public class CatalogAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", "amqp://guest:guest@localhost:5672");
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
    }
}

[CollectionDefinition("catalog")]
public class CatalogCollection : ICollectionFixture<CatalogAppFixture>;
