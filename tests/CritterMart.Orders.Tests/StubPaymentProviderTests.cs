using CritterMart.Orders.Ordering;
using Shouldly;
using Xunit;

namespace CritterMart.Orders.Tests;

// Pure unit tests for the StubPaymentProvider's DEMO AFFORDANCE (Payment:DeclineOverAmount). No host or
// database — the provider is a config-gated stub, so it is exercised by constructing it directly. The
// full decline→cancel→ReleaseStock chain it unlocks is covered by PaymentAuthorizationTests (slice 4.6);
// these only prove the threshold decision itself: unset = approve all, at/under = approve, over = decline.
public class StubPaymentProviderTests
{
    private static AuthorizePayment Order(decimal amount) => new("ord-1", amount);

    // The default everywhere except the demo: no threshold → approve every order (round-one behavior).
    [Fact]
    public async Task with_no_threshold_configured_it_approves_every_order()
    {
        var provider = new StubPaymentProvider(new PaymentDeclinePolicy(DeclineOverAmount: null));

        var decision = await provider.AuthorizeAsync(Order(9_999m));

        decision.Approved.ShouldBeTrue();
        decision.AuthCode.ShouldStartWith("stub-");
        decision.Reason.ShouldBeNull();
    }

    // At/under the threshold approves — so the demo's small order still confirms.
    [Theory]
    [InlineData(50)]
    [InlineData(100)] // exactly the threshold is NOT "over" — approved
    public async Task it_approves_orders_at_or_under_the_threshold(decimal amount)
    {
        var provider = new StubPaymentProvider(new PaymentDeclinePolicy(DeclineOverAmount: 100m));

        var decision = await provider.AuthorizeAsync(Order(amount));

        decision.Approved.ShouldBeTrue();
        decision.AuthCode.ShouldStartWith("stub-");
    }

    // Over the threshold declines — the precondition for the slice-4.6 cancel-and-release the demo shows.
    [Fact]
    public async Task it_declines_orders_over_the_threshold()
    {
        var provider = new StubPaymentProvider(new PaymentDeclinePolicy(DeclineOverAmount: 100m));

        var decision = await provider.AuthorizeAsync(Order(100.01m));

        decision.Approved.ShouldBeFalse();
        decision.AuthCode.ShouldBeNull();
        decision.Reason.ShouldNotBeNullOrWhiteSpace();
    }
}
