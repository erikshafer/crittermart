using CritterMart.Orders.Cart;
using JasperFx.Events;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure-function unit tests for the CartAbandonmentReport fold (slice 3.4, the round-one async
// projection teaser) — no database, no daemon, no container. The multi-stream routing (which
// document a CartAbandoned lands in, keyed by its abandonment day) is configured declaratively
// via Identity<IEvent<T>> and proven by the integration rebuild test in CartAbandonmentTests;
// these tests cover the fold itself: count, value, and SKU accumulation.
public class CartAbandonmentReportProjectionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly CartAbandonmentReportProjection _projection = new();

    private static Event<CartAbandoned> At(CartAbandoned data, DateTimeOffset timestamp) =>
        new(data) { Timestamp = timestamp };

    private static CartAbandoned Abandoned(params CartLine[] lines) =>
        new(CartAbandonReason.InactivityTimeout, lines, lines.Sum(l => l.Quantity * l.Price));

    // One abandonment folds its count, value, and SKU quantities into the day's report.
    [Fact]
    public void an_abandonment_folds_count_value_and_skus()
    {
        var report = new CartAbandonmentDailyReport();
        var abandoned = Abandoned(
            new CartLine("crit-001", 2, "Cosmic Critter Plush", 24.99m),
            new CartLine("crit-002", 1, "Nebula Newt", 18.00m));

        _projection.Apply(At(abandoned, T0), report);

        report.AbandonedCartCount.ShouldBe(1);
        report.TotalValueAbandoned.ShouldBe(2 * 24.99m + 18.00m); // 67.98
        report.AbandonedSkus["crit-001"].ShouldBe(2);
        report.AbandonedSkus["crit-002"].ShouldBe(1);
    }

    // A second abandonment the same day accumulates — counts add, values add, SKU tallies merge.
    [Fact]
    public void two_abandonments_accumulate_into_the_same_daily_report()
    {
        var report = new CartAbandonmentDailyReport();

        _projection.Apply(At(Abandoned(new CartLine("crit-001", 2, "Cosmic Critter Plush", 24.99m)), T0), report);
        _projection.Apply(At(Abandoned(
            new CartLine("crit-001", 1, "Cosmic Critter Plush", 24.99m),
            new CartLine("crit-007", 3, "Quantum Quokka", 32.50m)), T0.AddHours(3)), report);

        report.AbandonedCartCount.ShouldBe(2);
        report.TotalValueAbandoned.ShouldBe(2 * 24.99m + (24.99m + 3 * 32.50m));
        report.AbandonedSkus["crit-001"].ShouldBe(3);  // 2 from the first cart + 1 from the second
        report.AbandonedSkus["crit-007"].ShouldBe(3);
    }

    // An abandoned-but-empty cart still counts as an abandonment (zero value, no SKUs) — the
    // count tracks carts walked away from, not value lost.
    [Fact]
    public void an_empty_abandoned_cart_counts_with_zero_value()
    {
        var report = new CartAbandonmentDailyReport();

        _projection.Apply(At(Abandoned(), T0), report);

        report.AbandonedCartCount.ShouldBe(1);
        report.TotalValueAbandoned.ShouldBe(0m);
        report.AbandonedSkus.ShouldBeEmpty();
    }
}
