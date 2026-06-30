using Alba;
using CritterMart.Orders.Features;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Tracking;
using Xunit;

namespace CritterMart.Orders.Tests;

// Slice 3.4 (abandon cart on inactivity): the fired CartActivityTimeout IS the deadline passing —
// these tests invoke it directly instead of waiting real time (mirroring PaymentTimeoutTests /
// 4.7 design.md Decision 5), and drive the clock the handler reads (the fixture's TestTimeProvider)
// to cross or not cross the inactivity window. Tracked sessions prove the abandon, the
// fire-and-check reschedule, and the terminal-state guard's no-ops; the Bruun todo-list and the
// async report rebuild (ADR 008) are exercised end to end.
[Collection("orders")]
[Trait("Category", "Integration")]
public class CartAbandonmentTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly OrdersAppFixture _fixture;

    public CartAbandonmentTests(OrdersAppFixture fixture) => _fixture = fixture;

    private async Task ResetOrdersAsync()
    {
        // Reset the handler's clock to real now — a prior test may have advanced it.
        _fixture.Time.Now = DateTimeOffset.UtcNow;

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    private async Task<string> AddAsync(string customerId, string sku, int quantity, ProductSnapshot snapshot)
    {
        var result = await _fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new AddToCart(sku, quantity, snapshot)).ToUrl("/carts/mine/items");
            _.WithRequestHeader("X-Customer-Id", customerId);
            _.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<AddToCartResponse>()!.CartId;
    }

    // Cross the inactivity window by advancing the clock the handler reads — never by waiting.
    private void AdvancePastWindow() =>
        _fixture.Time.Now = DateTimeOffset.UtcNow.Add(CartActivityDeadline.Default).AddMinutes(5);

    // Workshop § 6.1 slice 3.4 happy path: the cart sat inactive past the window. The fired
    // timeout abandons it: the fat terminal event lands, the cart closes, the todo-list row goes.
    [Fact]
    public async Task a_timeout_abandons_an_inactive_cart()
    {
        await ResetOrdersAsync();
        var cartId = await AddAsync("customer-X", "crit-001", 2, CosmicCritterPlush);

        AdvancePastWindow();
        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(cartId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // The abandonment is recorded: the fat terminal event with the cart's lines + total
        // (what the async report will fold).
        var events = await session.Events.FetchStreamAsync(cartId);
        events.Count.ShouldBe(3); // CartCreated + CartItemAdded + CartAbandoned
        var abandoned = events[2].Data.ShouldBeOfType<CartAbandoned>();
        abandoned.Reason.ShouldBe(CartAbandonReason.InactivityTimeout);
        abandoned.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-001");
        abandoned.TotalValue.ShouldBe(2 * 24.99m);

        // The cart is closed; its lines are retained as readable history.
        var view = await session.LoadAsync<CartView>(cartId);
        view!.IsOpen.ShouldBeFalse();
        view.Lines.ShouldHaveSingleItem();

        // The todo-list row is gone (the conditional delete on CartAbandoned).
        var row = await session.LoadAsync<CartAwaitingActivity>(cartId);
        row.ShouldBeNull();

        // Nothing rescheduled — abandonment is terminal.
        tracked.Scheduled.AllMessages().OfType<CartActivityTimeout>().ShouldBeEmpty();
    }

    // Workshop § 6.1 slice 3.4 failure path (fire-and-check): the customer kept shopping, so the
    // fired timeout finds activity within the window — it appends nothing and re-aims itself at
    // the cart's true deadline (last activity + window).
    [Fact]
    public async Task a_timeout_reschedules_when_activity_intervened()
    {
        await ResetOrdersAsync();
        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        // The clock is NOT advanced: the cart's last activity is "just now", well within the
        // window — exactly what a fired timeout finds when the customer kept shopping.
        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(cartId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // No event appended; the cart is still open; the todo-list row is still there.
        var events = await session.Events.FetchStreamAsync(cartId);
        events.Count.ShouldBe(2); // CartCreated + CartItemAdded only

        var view = await session.LoadAsync<CartView>(cartId);
        view!.IsOpen.ShouldBeTrue();

        var row = await session.LoadAsync<CartAwaitingActivity>(cartId);
        row.ShouldNotBeNull();

        // The timeout re-aimed itself: a fresh CartActivityTimeout is scheduled for this cart.
        var rescheduled = tracked.Scheduled.SingleMessage<CartActivityTimeout>();
        rescheduled.CartId.ShouldBe(cartId);
    }

    // The terminal-state guard, checkout side: the cart became an order before its timer fired.
    // Losing that race is the timer's normal, expected fate for every successful checkout.
    [Fact]
    public async Task a_timeout_is_a_no_op_on_a_checked_out_cart()
    {
        await ResetOrdersAsync();
        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        // Check the cart out through the front door.
        await _fixture.Host.Scenario(_ =>
        {
            _.Post.Url("/orders");
            _.WithRequestHeader("X-Customer-Id", "customer-X");
            _.StatusCodeShouldBe(201);
        });

        AdvancePastWindow();
        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(cartId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Nothing changed: the stream still ends at CartCheckedOut, no abandonment, no reschedule.
        var events = await session.Events.FetchStreamAsync(cartId);
        events[^1].Data.ShouldBeOfType<CartCheckedOut>();
        events.Count(e => e.Data is CartAbandoned).ShouldBe(0);
        tracked.Scheduled.AllMessages().OfType<CartActivityTimeout>().ShouldBeEmpty();
    }

    // The terminal-state guard, duplicate side: at-least-once delivery can fire the same timeout
    // twice. The second finds the cart already abandoned and does nothing.
    [Fact]
    public async Task a_duplicate_timeout_is_a_no_op()
    {
        await ResetOrdersAsync();
        var cartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        AdvancePastWindow();
        await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(cartId));
        var tracked = await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(cartId));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        // Still exactly one abandonment — the duplicate appended nothing, scheduled nothing.
        var events = await session.Events.FetchStreamAsync(cartId);
        events.Count(e => e.Data is CartAbandoned).ShouldBe(1);
        tracked.Scheduled.AllMessages().OfType<CartActivityTimeout>().ShouldBeEmpty();
    }

    // An abandoned cart no longer blocks the customer: the partial-unique open-cart index only
    // covers OPEN carts, so the next add starts a fresh stream.
    [Fact]
    public async Task an_abandoned_cart_frees_the_customer_to_start_a_new_one()
    {
        await ResetOrdersAsync();
        var firstCartId = await AddAsync("customer-X", "crit-001", 1, CosmicCritterPlush);

        AdvancePastWindow();
        await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(firstCartId));

        // Back to the present: the customer returns and shops again.
        _fixture.Time.Now = DateTimeOffset.UtcNow;
        var secondCartId = await AddAsync("customer-X", "crit-002", 1, NebulaNewt);

        secondCartId.ShouldNotBe(firstCartId);
    }

    // The Bruun todo-list over HTTP: an open cart appears with its deadline; an abandoned cart
    // disappears (the conditional delete). The cart-side mirror of /orders/awaiting-payment.
    [Fact]
    public async Task the_awaiting_activity_list_shows_open_carts_and_drops_closed_ones()
    {
        await ResetOrdersAsync();
        var cartId = await AddAsync("customer-V", "crit-001", 1, CosmicCritterPlush);

        // The cart is on the todo-list, deadline in the future.
        var listed = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/awaiting-activity");
            _.StatusCodeShouldBe(200);
        });
        var rows = listed.ReadAsJson<List<CartAwaitingActivityRow>>()!;
        var row = rows.ShouldHaveSingleItem();
        row.Id.ShouldBe(cartId);
        row.CustomerId.ShouldBe("customer-V");
        row.Deadline.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        // Abandon it — the row disappears.
        AdvancePastWindow();
        await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(cartId));

        var afterAbandon = await _fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/carts/awaiting-activity");
            _.StatusCodeShouldBe(200);
        });
        afterAbandon.ReadAsJson<List<CartAwaitingActivityRow>>()!.ShouldBeEmpty();
    }

    // ADR 008's teaching beat, proven: the async CartAbandonmentReport is EMPTY until an
    // on-demand rebuild materializes it — no daemon runs anywhere in this test or in the service.
    // Mirrors Marten's own documented async-projection testing pattern (ctx7-verified): build a
    // one-shot daemon agent, rebuild, assert against the persisted documents.
    [Fact]
    public async Task the_abandonment_report_is_empty_until_rebuilt_on_demand()
    {
        await ResetOrdersAsync();

        // Two customers abandon carts "today".
        var firstCart = await AddAsync("customer-X", "crit-001", 2, CosmicCritterPlush);
        var secondCart = await AddAsync("customer-Y", "crit-002", 1, NebulaNewt);

        AdvancePastWindow();
        await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(firstCart));
        await _fixture.Host.InvokeMessageAndWaitAsync(new CartActivityTimeout(secondCart));

        var store = _fixture.Host.Services.GetRequiredService<IDocumentStore>();

        // The CartAbandoned events are on the streams, but the report has never been
        // materialized: the projection is async and no daemon is running (ADR 008).
        await using (var session = store.LightweightSession())
        {
            (await session.Query<CartAbandonmentDailyReport>().ToListAsync()).ShouldBeEmpty();
        }

        // The on-demand rebuild: a one-shot daemon agent, built and disposed right here.
        using var daemon = await store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<CartAbandonmentDailyReport>(CancellationToken.None);

        // Now the daily rollup exists, folded from the two fat CartAbandoned events.
        await using (var session = store.LightweightSession())
        {
            var report = (await session.Query<CartAbandonmentDailyReport>().ToListAsync()).ShouldHaveSingleItem();
            report.AbandonedCartCount.ShouldBe(2);
            report.TotalValueAbandoned.ShouldBe(2 * 24.99m + 18.00m);
            report.AbandonedSkus["crit-001"].ShouldBe(2);
            report.AbandonedSkus["crit-002"].ShouldBe(1);
        }
    }
}
