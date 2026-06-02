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

    // A second CartItemAdded for a DIFFERENT SKU folds into a second line (lines are SKU-keyed).
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

    // Adding the SAME SKU again merges quantities into the existing line (slices 3.2/3.3 resolved
    // 3.1's deferred merge-by-SKU question). The first add's snapshot price stays authoritative.
    [Fact]
    public void adding_the_same_sku_again_merges_quantities_into_one_line()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);
        _projection.Apply(new CartItemAdded("crit-001", 2, new ProductSnapshot("Cosmic Critter Plush", 29.99m)), view);

        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(3);
        line.Price.ShouldBe(24.99m); // the FIRST add's snapshot wins; the 29.99 re-add is ignored
    }

    // CartItemRemoved drops the SKU's line (slice 3.2). Other lines are untouched.
    [Fact]
    public void removing_an_item_drops_its_line()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);
        _projection.Apply(new CartItemAdded("crit-002", 3, NebulaNewt), view);
        _projection.Apply(new CartItemRemoved("crit-001"), view);

        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-002");
    }

    // Removing the last line leaves the cart open and empty — a legitimate state (slice 3.2).
    // PlaceOrder's CartEmpty guard protects checkout; the cart itself never auto-closes.
    [Fact]
    public void removing_the_last_item_leaves_the_cart_open_and_empty()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);
        _projection.Apply(new CartItemRemoved("crit-001"), view);

        view.Lines.ShouldBeEmpty();
        view.IsOpen.ShouldBeTrue();
    }

    // CartItemQuantityChanged rewrites the line's quantity in place (slice 3.3); the snapshotted
    // name and price are untouched.
    [Fact]
    public void changing_quantity_updates_the_line_in_place()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);
        _projection.Apply(new CartItemQuantityChanged("crit-001", 3), view);

        var line = view.Lines.ShouldHaveSingleItem();
        line.Quantity.ShouldBe(3);
        line.Name.ShouldBe("Cosmic Critter Plush");
        line.Price.ShouldBe(24.99m);
    }

    // CartCheckedOut closes the cart (slice 4.1): IsOpen flips false, which frees the customer
    // to start a fresh cart. Lines are retained — the checked-out cart is still readable history.
    [Fact]
    public void checking_out_closes_the_cart_but_keeps_its_lines()
    {
        var view = new CartView();

        _projection.Apply(new CartCreated("cart-1", "customer-X"), view);
        _projection.Apply(new CartItemAdded("crit-001", 1, CosmicCritterPlush), view);
        _projection.Apply(new CartCheckedOut("order-1"), view);

        view.IsOpen.ShouldBeFalse();
        view.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-001");
    }
}
