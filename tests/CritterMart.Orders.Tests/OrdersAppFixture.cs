using Alba;
using Testcontainers.PostgreSql;
using Xunit;

namespace CritterMart.Orders.Tests;

// Boots the Orders service against a throwaway Postgres container, shared across the test
// collection. Connection string injected via the env-var config provider. Mirrors
// InventoryAppFixture — the established event-sourced-service integration-test pattern.
public class OrdersAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", _postgres.GetConnectionString());
        Host = await AlbaHost.For<Program>();
    }

    public async Task DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", null);
    }
}

[CollectionDefinition("orders")]
public class OrdersCollection : ICollectionFixture<OrdersAppFixture>;
