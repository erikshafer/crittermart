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
}
