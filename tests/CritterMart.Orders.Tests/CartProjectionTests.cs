using CritterMart.Orders.Cart;
using JasperFx.Events;
using Shouldly;
using Xunit;

// Disambiguate the Cart aggregate TYPE from its same-named namespace (CS0234) — local alias only (ADR 020).
using CartAggregate = CritterMart.Orders.Cart.Cart;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the Cart aggregate fold (ADR 020 — the domain WRITE model). Cart is a
// sealed, immutable record whose static Create/Apply methods ARE its self-aggregating snapshot projection;
// each Apply returns a NEW Cart via `with`, so the fold is verified without a database, mocks, or a
// container. Untagged, so they run in the CI `Category!=Integration` job. Activity events arrive as
// IEvent<T> wrappers (constructed with Event<T>) so their append timestamps fold into LastActivityAt.
//
// CartView (the read model, ADR 020) folds the same events through the shared CartLines helper; a couple of
// read-fold cases at the bottom prove it stays consistent, and the integration tests exercise it over HTTP.
public class CartProjectionTests
{
    private static readonly ProductSnapshot CosmicCritterPlush = new("Cosmic Critter Plush", 24.99m);
    private static readonly ProductSnapshot NebulaNewt = new("Nebula Newt", 18.00m);
    private static readonly DateTimeOffset T0 = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    // Wraps an event the way Marten hands it to the fold: with its metadata (timestamp).
    private static Event<T> At<T>(T data, DateTimeOffset timestamp) where T : notnull =>
        new(data) { Timestamp = timestamp };

    // CartCreated starts the aggregate: it belongs to the customer and is open, no lines yet.
    [Fact]
    public void cart_created_opens_an_empty_cart_for_the_customer()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));

        cart.Id.ShouldBe("cart-1");
        cart.CustomerId.ShouldBe("customer-X");
        cart.IsOpen.ShouldBeTrue();
        cart.Lines.ShouldBeEmpty();
        cart.LastActivityAt.ShouldBe(T0);
    }

    // Folding CartCreated + one CartItemAdded yields a single line at the snapshot price.
    [Fact]
    public void adding_the_first_item_folds_into_a_single_line()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);

        var line = cart.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(1);
        line.Name.ShouldBe("Cosmic Critter Plush");
        line.Price.ShouldBe(24.99m);
    }

    // A second CartItemAdded for a DIFFERENT SKU folds into a second line (lines are SKU-keyed).
    [Fact]
    public void adding_a_second_item_folds_into_a_second_line()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-002", 3, NebulaNewt), T0), cart);

        cart.Lines.Count.ShouldBe(2);
        cart.Lines[0].ShouldBe(new CartLine("crit-001", 1, "Cosmic Critter Plush", 24.99m));
        cart.Lines[1].ShouldBe(new CartLine("crit-002", 3, "Nebula Newt", 18.00m));
    }

    // Adding the SAME SKU again merges quantities into the existing line; the first add's snapshot price wins.
    [Fact]
    public void adding_the_same_sku_again_merges_quantities_into_one_line()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 2, new ProductSnapshot("Cosmic Critter Plush", 29.99m)), T0), cart);

        var line = cart.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(3);
        line.Price.ShouldBe(24.99m); // the FIRST add's snapshot wins; the 29.99 re-add is ignored
    }

    // CartItemRemoved drops the SKU's line; other lines are untouched.
    [Fact]
    public void removing_an_item_drops_its_line()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-002", 3, NebulaNewt), T0), cart);
        cart = CartAggregate.Apply(At(new CartItemRemoved("crit-001"), T0), cart);

        cart.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-002");
    }

    // Removing the last line leaves the cart open and empty — a legitimate state (slice 3.2).
    [Fact]
    public void removing_the_last_item_leaves_the_cart_open_and_empty()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(At(new CartItemRemoved("crit-001"), T0), cart);

        cart.Lines.ShouldBeEmpty();
        cart.IsOpen.ShouldBeTrue();
    }

    // CartItemQuantityChanged rewrites the line's quantity in place (slice 3.3); name/price are untouched.
    [Fact]
    public void changing_quantity_updates_the_line_in_place()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(At(new CartItemQuantityChanged("crit-001", 3), T0), cart);

        var line = cart.Lines.ShouldHaveSingleItem();
        line.Quantity.ShouldBe(3);
        line.Name.ShouldBe("Cosmic Critter Plush");
        line.Price.ShouldBe(24.99m);
    }

    // Every activity event advances LastActivityAt to its own append timestamp — the fold IS the cart's
    // activity clock, which the fire-and-check abandonment decision reads.
    [Fact]
    public void the_fold_tracks_the_newest_activity_timestamp()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0.AddMinutes(5)), cart);
        cart = CartAggregate.Apply(At(new CartItemQuantityChanged("crit-001", 3), T0.AddMinutes(20)), cart);
        cart = CartAggregate.Apply(At(new CartItemRemoved("crit-001"), T0.AddMinutes(45)), cart);

        cart.LastActivityAt.ShouldBe(T0.AddMinutes(45));
    }

    // CartCheckedOut closes the cart (slice 4.1): IsOpen flips false, freeing the customer to start a fresh
    // cart. Lines are retained — the checked-out cart is still readable history.
    [Fact]
    public void checking_out_closes_the_cart_but_keeps_its_lines()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(new CartCheckedOut("order-1"), cart);

        cart.IsOpen.ShouldBeFalse();
        cart.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-001");
    }

    // CartAbandoned closes the cart (slice 3.4) — the stream's second terminal event, same shape as checkout.
    [Fact]
    public void abandonment_closes_the_cart_but_keeps_its_lines()
    {
        var cart = CartAggregate.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        cart = CartAggregate.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), cart);
        cart = CartAggregate.Apply(
            new CartAbandoned(CartAbandonReason.InactivityTimeout, [new CartLine("crit-001", 1, "Cosmic Critter Plush", 24.99m)], 24.99m),
            cart);

        cart.IsOpen.ShouldBeFalse();
        cart.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-001");
    }

    // The CartView READ model folds the same events through the shared CartLines helper, so it stays
    // consistent with the aggregate (decoupled type, same line semantics). A representative add + merge.
    [Fact]
    public void cartview_read_model_folds_lines_consistently_with_the_aggregate()
    {
        var view = CartView.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        view = CartView.Apply(At(new CartItemAdded("crit-001", 2, CosmicCritterPlush), T0), view);
        view = CartView.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), view);

        view.CustomerId.ShouldBe("customer-X");
        view.IsOpen.ShouldBeTrue();
        var line = view.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("crit-001");
        line.Quantity.ShouldBe(3); // merged by SKU, same as the aggregate
    }

    [Fact]
    public void cartview_read_model_closes_on_checkout()
    {
        var view = CartView.Create(At(new CartCreated("cart-1", "customer-X"), T0));
        view = CartView.Apply(At(new CartItemAdded("crit-001", 1, CosmicCritterPlush), T0), view);
        view = CartView.Apply(new CartCheckedOut("order-1"), view);

        view.IsOpen.ShouldBeFalse();
        view.Lines.ShouldHaveSingleItem().Sku.ShouldBe("crit-001");
    }
}
