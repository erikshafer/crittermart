extern alias InventoryApp;

using Alba;
using CritterMart.Orders.Order;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Swap the always-approve stub for a runtime-configurable one so a single shared Orders
        // host can drive both the approve (reserve smoke) and decline (release smoke) paths. The
        // crossbc collection runs serially, so the decline smoke flips ShouldApprove off and back
        // around its order without racing the reserve smoke — and without a second Orders host that
        // would compete for the reply queue or a second broker/Postgres that would race the
        // process-global connection-string env vars.
        OrdersHost = await AlbaHost.For<Program>(x => x.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<ConfigurableStubPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<ConfigurableStubPaymentProvider>());
        }));
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

// A stubbed payment provider whose decision is flipped by the test rather than by any value in the
// order payload (keeping with the 4.3 stub policy: no magic domain values). Approves by default —
// the reserve smoke relies on that; the release smoke sets ShouldApprove = false around its order.
public class ConfigurableStubPaymentProvider : IPaymentProvider
{
    public bool ShouldApprove { get; set; } = true;

    public Task<PaymentDecision> AuthorizeAsync(AuthorizePayment command) =>
        Task.FromResult(ShouldApprove
            ? new PaymentDecision(command.OrderId, Approved: true, AuthCode: $"stub-{Guid.NewGuid()}", Reason: null)
            : new PaymentDecision(command.OrderId, Approved: false, AuthCode: null, Reason: "declined"));
}
