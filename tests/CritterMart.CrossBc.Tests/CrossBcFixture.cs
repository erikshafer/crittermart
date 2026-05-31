extern alias InventoryApp;

using Alba;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CritterMart.CrossBc.Tests;

// Boots BOTH services against ONE real RabbitMQ broker and ONE shared Postgres (Orders writes
// the `orders` schema, Inventory the `inventory` schema, same database — schema-per-service,
// ADR 002). Unlike the per-service fixtures, transports are NOT stubbed here: this is the real
// cross-BC round-trip over the broker (slice 4.2, the OTel-trace centerpiece, proven in a test).
public class CrossBcFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4").Build();

    public IAlbaHost OrdersHost { get; private set; } = null!;
    public IAlbaHost InventoryHost { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        // Both hosts read the same connections via the env-var config provider.
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", _rabbit.GetConnectionString());

        // Inventory first so its ReserveStock listener exists before Orders sends one.
        InventoryHost = await AlbaHost.For<InventoryApp::Program>();
        OrdersHost = await AlbaHost.For<Program>();
    }

    public async Task DisposeAsync()
    {
        if (OrdersHost is not null)
        {
            await OrdersHost.DisposeAsync();
        }

        if (InventoryHost is not null)
        {
            await InventoryHost.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", null);
    }
}

[CollectionDefinition("crossbc")]
public class CrossBcCollection : ICollectionFixture<CrossBcFixture>;
