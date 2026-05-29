using CritterMart.Orders.Cart;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// The project's FIRST pure-function unit tests. The CartView projection's Apply methods are
// a pure fold over a Cart stream — no database, no mocks, no container. These are untagged,
// so they run in the CI `Category!=Integration` job that has selected zero tests since PR #19.
public class CartViewProjectionTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);

    private readonly CartViewProjection _projection = new();

    // CartCreated initializes the view: the cart belongs to the customer and is open, no lines yet.
    [Fact]
    public void cart_created_opens_an_empty_cart_for_the_customer()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);

        view.CustomerId.ShouldBe("customer-X");
        view.IsOpen.ShouldBeTrue();
        view.Lines.ShouldBeEmpty();
    }

    // Folding CartCreated + one CartItemAdded yields a single line at the snapshot price.
    [Fact]
    public void adding_the_first_item_folds_into_a_single_line()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);

        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(1);
        line.Name.ShouldBe("Cosmic Critter Plush");
        line.Price.ShouldBe(24.99m);
    }

    // A second CartItemAdded folds into a second line on the same view (one line per add in 3.1).
    [Fact]
    public void adding_a_second_item_folds_into_a_second_line()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);
        _projection.Apply(new CartItemAdded("crit-002", 3, NebulaNewt), view);

        view.Lines.Count.ShouldBe(2);
        view.Lines[0].ShouldBe(new CartLine("crit-001", 1, "Cosmic Critter Plush", 24.99m));
        view.Lines[1].ShouldBe(new CartLine("crit-002", 3, "Nebula Newt", 18.00m));
    }
}
