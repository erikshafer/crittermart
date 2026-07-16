using CritterMart.Contracts;
using CritterMart.Orders.Customers;
using CritterMart.Orders.Features;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 5.4: Orders consumes CustomerRegistered (the Published-Language integration event from Identity)
// and upserts a LocalCustomerView — the consumer-local read model that slice 5.3 enriches order responses with.
// CustomerRegistered arrives as a Wolverine message (NOT from Marten's event store / async daemon), so the
// handler is a plain static Handle method with IDocumentSession, invoked via InvokeMessageAndWaitAsync.
//
// Slice 5.3: GET /orders/{orderId} and GET /orders/mine enrich the response with CustomerName from the
// LocalCustomerView loaded at read time. CustomerName is null when the local model is absent (eventually-
// consistent gap — the PL event may not have arrived yet). No synchronous call to Identity (ADR 001).
[Collection("orders")]
[Trait("Category", "Integration")]
public class CustomerRegisteredHandlerTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);

    private readonly OrdersAppFixture _fixture;

    public CustomerRegisteredHandlerTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetAsync()
    {
        await _fixture.ResetAllDataAsync();
    }

    private async Task<string> PlaceAnOrderAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url("/orders");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<PlaceOrderResponse>()!.OrderId;
    }

    // ── Slice 5.4: handler upserts LocalCustomerView ──────────────────────────────────────────────

    // Happy path: receiving a CustomerRegistered message upserts a LocalCustomerView document keyed
    // by customerId. InvokeMessageAndWaitAsync drives the full Wolverine handler pipeline (including
    // AutoApplyTransactions), so we verify the committed document — not just the in-memory upsert.
    [Fact]
    public async Task handling_CustomerRegistered_upserts_a_local_customer_view()
    {
        await ResetAsync();

        await _fixture.Host.InvokeMessageAndWaitAsync(
            new CustomerRegistered("cust-1", "alice@example.com", "Alice Wonderland"));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession();
        var view = await session.LoadAsync<LocalCustomerView>("cust-1");

        view.ShouldNotBeNull();
        view.Id.ShouldBe("cust-1");
        view.DisplayName.ShouldBe("Alice Wonderland");
    }

    // Idempotency: receiving the same CustomerRegistered message twice must not produce two documents
    // or throw. session.Store() is Marten's upsert by document-id convention — a second call with the
    // same id overwrites the first, leaving exactly one document. The handler is safe to replay.
    [Fact]
    public async Task handling_CustomerRegistered_twice_is_idempotent()
    {
        await ResetAsync();

        await _fixture.Host.InvokeMessageAndWaitAsync(
            new CustomerRegistered("cust-idm", "bob@example.com", "Bob First"));
        await _fixture.Host.InvokeMessageAndWaitAsync(
            new CustomerRegistered("cust-idm", "bob@example.com", "Bob Updated"));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession();

        var allViews = await session.Query<LocalCustomerView>()
            .Where(v => v.Id == "cust-idm")
            .ToListAsync();

        allViews.Count.ShouldBe(1);
        allViews[0].DisplayName.ShouldBe("Bob Updated");
    }

    // ── Slice 5.3: enrichment at read time ────────────────────────────────────────────────────────

    // When the LocalCustomerView is present, GET /orders/{orderId} returns CustomerName populated.
    // The order and the customer view are seeded before the GET; the endpoint does two primary-key loads
    // and assembles EnrichedOrderView.From(view, customer?.DisplayName).
    [Fact]
    public async Task GET_orders_orderId_returns_customer_name_when_local_model_is_present()
    {
        await ResetAsync();

        await _fixture.Host.InvokeMessageAndWaitAsync(
            new CustomerRegistered("customer-enrich", "carol@example.com", "Carol Demo"));

        var orderId = await PlaceAnOrderAsync("customer-enrich", "crit-001", 1, CosmicCritterPlush);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/orders/{orderId}");
            _.StatusCodeShouldBe(200);
        });

        var enriched = result.ReadAsJson<EnrichedOrderView>();
        enriched.ShouldNotBeNull();
        enriched.CustomerName.ShouldBe("Carol Demo");
        enriched.Id.ShouldBe(orderId);
        enriched.CustomerId.ShouldBe("customer-enrich");
    }

    // Eventual-consistency degradation: when no LocalCustomerView exists for the order's customerId,
    // the endpoint degrades gracefully — CustomerName is null, the rest of the order is intact. No
    // call to Identity, no 404, no exception. The PL event simply hasn't arrived yet.
    [Fact]
    public async Task GET_orders_orderId_returns_null_customer_name_when_local_model_is_absent()
    {
        await ResetAsync();

        var orderId = await PlaceAnOrderAsync("customer-no-view", "crit-001", 1, CosmicCritterPlush);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/orders/{orderId}");
            _.StatusCodeShouldBe(200);
        });

        var enriched = result.ReadAsJson<EnrichedOrderView>();
        enriched.ShouldNotBeNull();
        enriched.CustomerName.ShouldBeNull();
        enriched.Id.ShouldBe(orderId);
    }

    // GET /orders/mine enriches every row with the same CustomerName (one LocalCustomerView load for
    // the whole list — all orders share customerId from the token's `sub` claim).
    [Fact]
    public async Task GET_orders_mine_returns_customer_name_when_local_model_is_present()
    {
        await ResetAsync();

        await _fixture.Host.InvokeMessageAndWaitAsync(
            new CustomerRegistered("customer-mine", "dave@example.com", "Dave Mine"));

        await PlaceAnOrderAsync("customer-mine", "crit-001", 1, CosmicCritterPlush);
        await PlaceAnOrderAsync("customer-mine", "crit-001", 2, CosmicCritterPlush);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/orders/mine");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer("customer-mine"));
            _.StatusCodeShouldBe(200);
        });

        var enriched = result.ReadAsJson<List<EnrichedOrderView>>();
        enriched.ShouldNotBeNull();
        enriched.Count.ShouldBe(2);
        enriched.ShouldAllBe(o => o.CustomerName == "Dave Mine");
    }
}
