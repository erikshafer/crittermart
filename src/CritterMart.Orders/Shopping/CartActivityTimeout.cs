namespace CritterMart.Orders.Shopping;

// The scheduled self-message that gives every open cart an inactivity deadline (Workshop 001
// slice 3.4, the second Bruun temporal automation; mirrors Order/OrderPaymentTimeout). AddToCart
// cascades this message delayed by the configured window when it CREATES a cart; the
// CartAbandonmentHandler re-cascades it when it fires against a cart whose activity intervened
// (fire-and-check, design.md Decision 1). This is an Orders-local message, not a published-language
// contract — it never crosses a service boundary, and its local handler keeps it in-process.
public record CartActivityTimeout(string CartId);

// The configured inactivity window (slice 3.4, design.md Decision 6): how long a cart may sit
// without activity before the fired timeout abandons it. Bound from Orders:CartActivityTimeout
// in Program.cs (default 2 hours — Workshop § 7's ">2h as abandoned" rebuild story) and registered
// as a singleton, so the AddToCart schedule, the abandonment handler's decision, and the
// CartsAwaitingActivity projection's visible deadline all read the same value.
public record CartActivityDeadline(TimeSpan Duration)
{
    public static readonly TimeSpan Default = TimeSpan.FromHours(2);
}
