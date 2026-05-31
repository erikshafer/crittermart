using CritterMart.Orders.Order;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the OrderStatusView fold — no database, no mocks, no container.
// Untagged, so they run in the CI `Category!=Integration` job alongside CartViewProjectionTests.
public class OrderStatusViewProjectionTests
{
    private static readonly OrderLine Plush = new("crit-001", 2, "Cosmic Critter Plush", 24.99m);
    private static readonly OrderLine Newt = new("crit-002", 3, "Nebula Newt", 18.00m);

    private readonly OrderStatusViewProjection _projection = new();

    // OrderPlaced initializes the view: the order belongs to the customer, awaits confirmation,
    // and carries the snapshotted lines + computed total.
    [Fact]
    public void order_placed_opens_an_order_awaiting_confirmation()
    {
        var view = new OrderStatusView();

        _projection.Apply(new OrderPlaced("order-1", "customer-X", [Plush, Newt], 103.98m), view);

        view.CustomerId.ShouldBe("customer-X");
        view.Status.ShouldBe(OrderStatus.AwaitingConfirmation);
        view.Lines.Count.ShouldBe(2);
        view.Lines[0].ShouldBe(Plush);
        view.Lines[1].ShouldBe(Newt);
        view.Total.ShouldBe(103.98m);
    }

    // Slice 4.2: the Klefter StockReserved commit advances the status past the stock gate.
    [Fact]
    public void stock_reserved_advances_status_to_stock_reserved()
    {
        var view = new OrderStatusView { Status = OrderStatus.AwaitingConfirmation };

        _projection.Apply(new StockReserved("order-1"), view);

        view.Status.ShouldBe(OrderStatus.StockReserved);
    }

    // Slice 4.3: the Klefter PaymentAuthorized commit advances the status past the payment gate.
    [Fact]
    public void payment_authorized_advances_status_to_payment_authorized()
    {
        var view = new OrderStatusView { Status = OrderStatus.StockReserved };

        _projection.Apply(new PaymentAuthorized("order-1", "stub-abc123", 103.98m), view);

        view.Status.ShouldBe(OrderStatus.PaymentAuthorized);
    }

    // Slice 4.4: OrderConfirmed is the terminal success state — both gates closed.
    [Fact]
    public void order_confirmed_sets_status_to_confirmed()
    {
        var view = new OrderStatusView { Status = OrderStatus.PaymentAuthorized };

        _projection.Apply(new OrderConfirmed("order-1"), view);

        view.Status.ShouldBe(OrderStatus.Confirmed);
    }

    // Slice 4.5: OrderCancelled is terminal — the view reads cancelled.
    [Fact]
    public void order_cancelled_sets_status_to_cancelled()
    {
        var view = new OrderStatusView { Status = OrderStatus.AwaitingConfirmation };

        _projection.Apply(new OrderCancelled("order-1", CancelReason.StockUnavailable), view);

        view.Status.ShouldBe(OrderStatus.Cancelled);
    }
}
