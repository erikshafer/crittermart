using Alba;
using Testcontainers.PostgreSql;
using Xunit;

namespace CritterMart.Inventory.Tests;

// Boots the Inventory service against a throwaway Postgres container, shared across
// the test collection. Connection string injected via the env-var config provider.
public class InventoryAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
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

[CollectionDefinition("inventory")]
public class InventoryCollection : ICollectionFixture<InventoryAppFixture>;
