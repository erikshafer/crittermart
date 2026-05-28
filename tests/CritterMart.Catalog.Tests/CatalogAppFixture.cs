using Alba;
using Testcontainers.PostgreSql;
using Xunit;

namespace CritterMart.Catalog.Tests;

// Boots the Catalog service against a throwaway Postgres container, shared across
// the test collection. The connection string is injected via the environment-variable
// config provider (WebApplicationBuilder adds it after appsettings.json, so it wins).
public class CatalogAppFixture : IAsyncLifetime
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

[CollectionDefinition("catalog")]
public class CatalogCollection : ICollectionFixture<CatalogAppFixture>;
