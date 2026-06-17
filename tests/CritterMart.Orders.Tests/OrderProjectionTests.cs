using CritterMart.Orders.Ordering;
using JasperFx.Events;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the Order aggregate fold (ADR 020 — the domain WRITE model and the PMvH
// process-manager state) and the OrderStatusView read model that folds the same events. Both are sealed,
// immutable records whose static Create/Apply methods ARE their self-aggregating snapshot projections; each
// Apply returns a NEW instance via `with`, so the fold is verified without a database, mocks, or a container.
// Untagged, so they run in the CI `Category!=Integration` job alongside CartProjectionTests.
//
// The Order aggregate tracks no activity timestamp, so its folds take plain events. The OrderStatusView read
// model's Create, though, takes an IEvent<OrderPlaced> wrapper (constructed with Event<T>, the At<T> helper
// below) to surface the placement timestamp as PlacedAt — the read view carrying metadata the write aggregate
// omits (ADR 020), exactly as CartView/LastActivityAt does. This is the ADR 020/021 Order rollout: it
// replaced the former OrderStatusViewProjection : SingleStreamProjection class (which mutated a shared
// OrderStatusView instance) with these immutable-record folds.
public class OrderProjectionTests
{
    private static readonly OrderLine Plush = new("crit-001", 2, "Cosmic Critter Plush", 24.99m);
    private static readonly OrderLine Newt = new("crit-002", 3, "Nebula Newt", 18.00m);
    private static readonly DateTimeOffset T0 = new(2026, 6, 16, 14, 2, 0, TimeSpan.Zero);

    // Wraps an event the way Marten hands it to the view's Create: with its metadata (the append timestamp
    // OrderStatusView surfaces as PlacedAt). Mirrors CartProjectionTests' helper.
    private static Event<T> At<T>(T data, DateTimeOffset timestamp) where T : notnull =>
        new(data) { Timestamp = timestamp };

    // ── The Order aggregate (write model / PMvH state) ──

    // OrderPlaced is genesis: the order belongs to the customer, awaits confirmation, and carries the
    // snapshotted lines + computed total frozen from the cart at checkout. Id is the stream key (orderId).
    [Fact]
    public void order_placed_opens_an_order_awaiting_confirmation()
    {
        var order = Order.Create(new OrderPlaced("order-1", "customer-X", [Plush, Newt], 103.98m));

        order.Id.ShouldBe("order-1");
        order.CustomerId.ShouldBe("customer-X");
        order.Status.ShouldBe(OrderStatus.AwaitingConfirmation);
        order.Lines.Count.ShouldBe(2);
        order.Lines[0].ShouldBe(Plush);
        order.Lines[1].ShouldBe(Newt);
        order.Total.ShouldBe(103.98m);
    }

    // Slice 4.2: the Klefter StockReserved commit advances the status past the stock gate — the value the
    // StockReservedHandler's idempotency guard reads on a replay (it acts only while AwaitingConfirmation).
    [Fact]
    public void stock_reserved_advances_status_to_stock_reserved()
    {
        var order = Order.Create(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m));

        order = Order.Apply(new StockReserved("order-1"), order);

        order.Status.ShouldBe(OrderStatus.StockReserved);
    }

    // Slice 4.3: the Klefter PaymentAuthorized commit advances the status past the payment gate. Total + Lines
    // are untouched — the gate grant only moves the status, and the handlers read Total/Lines to cascade.
    [Fact]
    public void payment_authorized_advances_status_and_preserves_lines_and_total()
    {
        var order = Order.Create(new OrderPlaced("order-1", "customer-X", [Plush, Newt], 103.98m));
        order = Order.Apply(new StockReserved("order-1"), order);

        order = Order.Apply(new PaymentAuthorized("order-1", "stub-abc123", 103.98m), order);

        order.Status.ShouldBe(OrderStatus.PaymentAuthorized);
        order.Total.ShouldBe(103.98m);
        order.Lines.Count.ShouldBe(2);
    }

    // Slice 4.4: OrderConfirmed is the terminal success state — both gates closed.
    [Fact]
    public void order_confirmed_sets_status_to_confirmed()
    {
        var order = Order.Create(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m));
        order = Order.Apply(new StockReserved("order-1"), order);
        order = Order.Apply(new PaymentAuthorized("order-1", "stub-abc123", 49.98m), order);

        order = Order.Apply(new OrderConfirmed("order-1"), order);

        order.Status.ShouldBe(OrderStatus.Confirmed);
    }

    // Slices 4.5/4.6/4.7: OrderCancelled is terminal — the order reads cancelled (the terminal guard every
    // handler reads to no-op a late or duplicate cross-BC reply).
    [Fact]
    public void order_cancelled_sets_status_to_cancelled()
    {
        var order = Order.Create(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m));

        order = Order.Apply(new OrderCancelled("order-1", CancelReason.StockUnavailable), order);

        order.Status.ShouldBe(OrderStatus.Cancelled);
    }

    // ── The OrderStatusView read model (ADR 020) — folds the same events, stays consistent with the aggregate ──

    // OrderStatusView is decoupled from the Order aggregate but folds the same genesis, so its public shape
    // (the W3/W4 wire) reflects the placed order's lines, total, and awaiting_confirmation status. It also
    // surfaces PlacedAt (the OrderPlaced append timestamp, off the IEvent<T> wrapper — slice 025) and starts
    // with a null CancelReason: an active order has no cancellation to report.
    [Fact]
    public void orderstatusview_read_model_reflects_the_placed_order()
    {
        var view = OrderStatusView.Create(At(new OrderPlaced("order-1", "customer-X", [Plush, Newt], 103.98m), T0));

        view.Id.ShouldBe("order-1");
        view.CustomerId.ShouldBe("customer-X");
        view.Status.ShouldBe(OrderStatus.AwaitingConfirmation);
        view.Lines.Count.ShouldBe(2);
        view.Total.ShouldBe(103.98m);
        view.PlacedAt.ShouldBe(T0);
        view.CancelReason.ShouldBeNull();
    }

    // The read model walks the same status path as the aggregate — a representative grant + a terminal.
    [Fact]
    public void orderstatusview_read_model_walks_the_status_path_consistently()
    {
        var view = OrderStatusView.Create(At(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m), T0));

        OrderStatusView.Apply(new StockReserved("order-1"), view).Status.ShouldBe(OrderStatus.StockReserved);
        OrderStatusView.Apply(new OrderCancelled("order-1", CancelReason.PaymentTimeout), view).Status
            .ShouldBe(OrderStatus.Cancelled);
    }

    // PlacedAt is set once at genesis and carried forward unchanged by every later fold (the `with` semantics
    // touch only the named field), and CancelReason stays null all the way to a CONFIRMED order — only a
    // cancellation sets it (slice 025).
    [Fact]
    public void orderstatusview_read_model_keeps_placed_at_and_a_null_reason_through_a_confirmed_order()
    {
        var view = OrderStatusView.Create(At(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m), T0));
        view = OrderStatusView.Apply(new StockReserved("order-1"), view);
        view = OrderStatusView.Apply(new PaymentAuthorized("order-1", "stub-abc123", 49.98m), view);
        view = OrderStatusView.Apply(new OrderConfirmed("order-1"), view);

        view.Status.ShouldBe(OrderStatus.Confirmed);
        view.PlacedAt.ShouldBe(T0); // unchanged since genesis
        view.CancelReason.ShouldBeNull(); // a confirmed order was never cancelled
    }

    // Each of the three cancellation routes surfaces its OWN reason on the view (slice 025): the read model
    // folds OrderCancelled.Reason that the write aggregate ignores, so W4 can show the specific failure.
    [Theory]
    [InlineData("stock_unavailable")]
    [InlineData("payment_declined")]
    [InlineData("payment_timeout")]
    public void orderstatusview_read_model_carries_each_cancellation_reason(string reason)
    {
        var view = OrderStatusView.Create(At(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m), T0));

        view = OrderStatusView.Apply(new OrderCancelled("order-1", reason), view);

        view.Status.ShouldBe(OrderStatus.Cancelled);
        view.CancelReason.ShouldBe(reason);
        view.PlacedAt.ShouldBe(T0); // the cancellation does not disturb the placement time
    }
}
