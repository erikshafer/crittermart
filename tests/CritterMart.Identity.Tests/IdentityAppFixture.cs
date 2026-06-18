using Alba;
using Testcontainers.PostgreSql;
using Wolverine;
using Xunit;

namespace CritterMart.Identity.Tests;

// Boots the Identity service against a throwaway Postgres container, shared across the test
// collection. Connection string injected via the env-var config provider; external transports are
// stubbed so the host boots with no broker (Wolverine still records cascaded messages in tracked
// sessions). Deliberately a near-identical twin of InventoryAppFixture — the test harness is
// persistence-agnostic too, even though the service under it is EF Core rather than Marten.
public class IdentityAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    // The storefront origin the AppHost pins (Vite dev server on 5273), injected via the exact key
    // the AppHost uses (Cors__AllowedOrigins__0) so the host boots its real config-driven allowlist.
    public const string SpaOrigin = "http://localhost:5273";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__crittermart", _postgres.GetConnectionString());
        // A dummy connection so UseRabbitMqUsingNamedConnection("rabbitmq") resolves at config time;
        // the transport is stubbed below, so nothing actually connects.
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

[CollectionDefinition("identity")]
public class IdentityCollection : ICollectionFixture<IdentityAppFixture>;
