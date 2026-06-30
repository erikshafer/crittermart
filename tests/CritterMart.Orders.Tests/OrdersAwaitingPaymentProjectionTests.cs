using CritterMart.Orders.Ordering;
using JasperFx.Events;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the OrdersAwaitingPayment todo-list fold (slice 4.7) — no database,
// no mocks, no container. Untagged, so they run in the CI `Category!=Integration` job alongside the
// other projection fold tests. The Apply takes an IEvent<OrderPlaced> wrapper (Marten's using-metadata
// convention) so it can record the event's append timestamp; the test constructs the wrapper directly
// with Event<T>. The projection is STATELESS — the visible deadline (PlacedAt + timeout) is applied on
// read in the endpoint, not here (chore/004; see OrdersAwaitingPayment remarks).
public class OrdersAwaitingPaymentProjectionTests
{
    private static readonly OrderLine Plush = new("crit-001", 2, "Cosmic Critter Plush", 24.99m);

    private readonly OrdersAwaitingPaymentProjection _projection = new();

    // OrderPlaced creates the row: the order joins the todo-list recording its placement timestamp
    // (the read endpoint adds the configured timeout to produce the visible deadline).
    [Fact]
    public void order_placed_creates_a_row_recording_placed_at()
    {
        var view = new OrderAwaitingPayment();
        var placedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var placed = new Event<OrderPlaced>(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m))
        {
            Timestamp = placedAt
        };

        _projection.Apply(placed, view);

        view.CustomerId.ShouldBe("customer-X");
        view.Total.ShouldBe(49.98m);
        view.PlacedAt.ShouldBe(placedAt);
    }

    // The conditional delete (Marten ShouldDelete convention): a confirmed order leaves the list.
    [Fact]
    public void order_confirmed_removes_the_row()
    {
        _projection.ShouldDelete(new OrderConfirmed("order-1")).ShouldBeTrue();
    }

    // A cancelled order leaves the list no matter which path cancelled it — stock failure (4.5),
    // payment decline (4.6), or the timeout itself (4.7). The todo-list stays accurate for all.
    [Fact]
    public void order_cancelled_removes_the_row_for_any_reason()
    {
        _projection.ShouldDelete(new OrderCancelled("order-1", CancelReason.StockUnavailable)).ShouldBeTrue();
        _projection.ShouldDelete(new OrderCancelled("order-1", CancelReason.PaymentDeclined)).ShouldBeTrue();
        _projection.ShouldDelete(new OrderCancelled("order-1", CancelReason.PaymentTimeout)).ShouldBeTrue();
    }
}
