using CritterMart.Orders.Shopping;
using JasperFx.Events;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the CartsAwaitingActivity todo-list fold (slice 3.4) — no database,
// no mocks, no container. Untagged, so they run in the CI `Category!=Integration` job alongside the
// other projection fold tests. Mirrors OrdersAwaitingPaymentProjectionTests: Apply methods take
// IEvent<T> wrappers (Marten's using-metadata convention) so each activity event's append timestamp is
// recorded; the tests construct the wrappers with Event<T>. The projection is STATELESS — the visible
// deadline (LastActivityAt + window) is applied on read in the endpoint (chore/004).
public class CartsAwaitingActivityProjectionTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly DateTimeOffset T0 = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly CartsAwaitingActivityProjection _projection = new();

    private static Event<T> At<T>(T data, DateTimeOffset timestamp) where T : notnull =>
        new(data) { Timestamp = timestamp };

    // CartCreated creates the row: the cart joins the todo-list recording its creation timestamp
    // (the read endpoint adds the configured inactivity window to produce the visible deadline).
    [Fact]
    public void cart_created_records_the_activity_timestamp()
    {
        var view = new CartAwaitingActivity();

        _projection.Apply(At(new CartCreated("cart-1", "customer-X"), T0), view);

        view.CustomerId.ShouldBe("customer-X");
        view.LastActivityAt.ShouldBe(T0);
    }

    // Every activity event advances the recorded activity timestamp — the visible mirror of
    // fire-and-check (the read endpoint projects it forward by the window).
    [Fact]
    public void cart_activity_advances_the_activity_timestamp()
    {
        var view = new CartAwaitingActivity();

        _projection.Apply(At(new CartCreated("cart-1", "customer-X"), T0), view);
        _projection.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0.AddMinutes(10)), view);
        view.LastActivityAt.ShouldBe(T0.AddMinutes(10));

        _projection.Apply(At(new CartItemQuantityChanged("crit-001", 3), T0.AddMinutes(30)), view);
        view.LastActivityAt.ShouldBe(T0.AddMinutes(30));

        _projection.Apply(At(new CartItemRemoved("crit-001"), T0.AddMinutes(55)), view);
        view.LastActivityAt.ShouldBe(T0.AddMinutes(55));
    }

    // The conditional delete (Marten ShouldDelete convention): a checked-out cart leaves the list.
    [Fact]
    public void checkout_removes_the_row()
    {
        _projection.ShouldDelete(new CartCheckedOut("order-1")).ShouldBeTrue();
    }

    // An abandoned cart leaves the list too — either terminal event ends the automation's watch.
    [Fact]
    public void abandonment_removes_the_row()
    {
        _projection.ShouldDelete(new CartAbandoned(CartAbandonReason.InactivityTimeout, [], 0m)).ShouldBeTrue();
    }
}
