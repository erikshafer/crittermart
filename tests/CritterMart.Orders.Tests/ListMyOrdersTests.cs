using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Shopping;
using CritterMart.TestSupport;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// "My Orders" — the customer-keyed order list (GET /orders/mine; workshop § 5.1 Gap #3, closed; OpenSpec
// change list-my-orders). The list counterpart to the single-order W4 track read: it resolves the customer's
// orders BY identity (the JWT `sub` claim, no orderId) over the existing OrderStatusView documents, ordered
// newest-first. Mirrors ViewMyCartTests — the sibling customer-keyed read in this service.
[Collection("orders")]
[Trait("Category", "Integration")]
public class ListMyOrdersTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly OrdersAppFixture _fixture;

    public ListMyOrdersTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        await _fixture.ResetAllDataAsync();
    }

    private async Task AddAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });
    }

    // Add a line then check out — leaving a placed order at awaiting_confirmation (the cross-BC reserve-stock
    // request goes nowhere in the Orders-only host, so the order does not advance) and freeing the customer to
    // start a fresh cart for the next order. Returns the new orderId so a test can assert ordering by id.
    private async Task<string> PlaceAnOrderAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        await AddAsync(customerId, sku, quantity, snapshot);

        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url("/orders");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<PlaceOrderResponse>()!.OrderId;
    }

    private Task<List<OrderStatusView>> ListMyOrdersAsync(string customerId) =>
        ListMyOrdersWithStatusAsync(customerId, 200);

    private async Task<List<OrderStatusView>> ListMyOrdersWithStatusAsync(string customerId, int expectedStatus)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/orders/mine");
            _.WithRequestHeader("Authorization", JwtTestTokens.Bearer(customerId));
            _.StatusCodeShouldBe(expectedStatus);
        });

        return result.ReadAsJson<List<OrderStatusView>>()!;
    }

    // Happy path: two orders for the same customer come back NEWEST-FIRST. The two placements are separate
    // transactions, so the second order's OrderPlaced append timestamp is later — OrderByDescending(PlacedAt)
    // puts it first.
    [Fact]
    public async Task listing_my_orders_returns_my_orders_newest_first()
    {
        await ResetOrdersAsync();

        var firstOrderId = await PlaceAnOrderAsync("customer-X", "crit-001", 2, CosmicCritterPlush);
        var secondOrderId = await PlaceAnOrderAsync("customer-X", "crit-002", 3, NebulaNewt);

        var orders = await ListMyOrdersAsync("customer-X");

        orders.Count.ShouldBe(2);
        orders[0].Id.ShouldBe(secondOrderId);
        orders[1].Id.ShouldBe(firstOrderId);
        orders.ShouldAllBe(o => o.CustomerId == "customer-X");
    }

    // The list resolves BY identity, not "return any orders": a second customer's order must not leak into the
    // first customer's list.
    [Fact]
    public async Task listing_my_orders_scopes_strictly_to_the_requesting_customer()
    {
        await ResetOrdersAsync();

        var myOrderId = await PlaceAnOrderAsync("customer-X", "crit-001", 1, CosmicCritterPlush);
        await PlaceAnOrderAsync("customer-Y", "crit-002", 1, NebulaNewt);

        var orders = await ListMyOrdersAsync("customer-X");

        orders.Count.ShouldBe(1);
        orders[0].Id.ShouldBe(myOrderId);
        orders[0].CustomerId.ShouldBe("customer-X");
    }

    // A customer who never placed an order gets an empty list (200 []), a domain-empty state — NOT a 404 (the
    // contrast with GET /carts/mine, where 404 means "no open cart").
    [Fact]
    public async Task listing_my_orders_with_no_orders_returns_an_empty_list()
    {
        await ResetOrdersAsync();

        var orders = await ListMyOrdersAsync("customer-Z");

        orders.ShouldBeEmpty();
    }

    // Terminal orders are included (no status filter), each carrying its OrderStatusView shape — a confirmed
    // order with a null cancelReason and a cancelled order carrying its reason. Seeded directly on the streams
    // because the terminal states are not reachable through the Orders-only host (the cross-BC saga is absent):
    // the inline OrderStatusView projection materializes the views on append, and the list query reads them.
    [Fact]
    public async Task listing_my_orders_includes_terminal_orders_with_their_cancel_reason()
    {
        await ResetOrdersAsync();

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using (var session = store.LightweightSession())
        {
            var confirmedId = Guid.NewGuid().ToString();
            session.Events.StartStream<Order>(confirmedId,
                new OrderPlaced(confirmedId, "customer-X",
                    [new OrderLine("crit-001", 1, "Cosmic Critter Plush", 24.99m)], 24.99m),
                new StockReserved(confirmedId),
                new PaymentAuthorized(confirmedId, "stub-auth", 24.99m),
                new OrderConfirmed(confirmedId));

            var cancelledId = Guid.NewGuid().ToString();
            session.Events.StartStream<Order>(cancelledId,
                new OrderPlaced(cancelledId, "customer-X",
                    [new OrderLine("crit-002", 2, "Nebula Newt", 18.00m)], 36.00m),
                new OrderCancelled(cancelledId, CancelReason.StockUnavailable));

            await session.SaveChangesAsync();
        }

        var orders = await ListMyOrdersAsync("customer-X");

        orders.Count.ShouldBe(2);

        var confirmed = orders.Single(o => o.Status == OrderStatus.Confirmed);
        confirmed.CancelReason.ShouldBeNull();

        var cancelled = orders.Single(o => o.Status == OrderStatus.Cancelled);
        cancelled.CancelReason.ShouldBe(CancelReason.StockUnavailable);
    }

    // Hard cutover (ADR 023): a token-less request is 401'd by [Authorize] — unauthenticated, kept
    // distinct from the empty-list case above. Mirrors ViewMyCart's no-token rejection.
    [Fact]
    public async Task listing_my_orders_without_a_token_returns_401()
    {
        await ResetOrdersAsync();

        await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/orders/mine");
            _.StatusCodeShouldBe(401);
        });
    }
}
