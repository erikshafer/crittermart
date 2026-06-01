namespace CritterMart.Orders.Order;

// The scheduled self-message that gives every placed order a deadline (Workshop 001 slice 4.7,
// Bruun temporal automation; ADR 007). PlaceOrder cascades this message delayed by the configured
// payment timeout; Wolverine delivers it back to PaymentTimeoutHandler when the deadline passes.
// This is an Orders-local message, not a published-language contract — it never crosses a service
// boundary, and its local handler keeps it in-process (the same local-over-broker conventional
// routing precedence that keeps 4.3's AuthorizePayment / PaymentDecision in-process).
public record OrderPaymentTimeout(string OrderId);

// The configured payment deadline (slice 4.7, design.md Decision 4): how long a placed order may
// sit non-terminal before the timeout cancels it. Bound from Orders:PaymentTimeout in Program.cs
// (default 10 minutes) and registered as a singleton, so the PlaceOrder schedule and the
// OrdersAwaitingPayment projection's visible deadline read the same value.
public record PaymentDeadline(TimeSpan Duration)
{
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(10);
}
