namespace CritterMart.Orders.Ordering;

// The in-process "call the payment provider" seam for slice 4.3. This is the Orders-internal
// mirror of slice 4.2's cross-BC reserve hop: there, Inventory was the external party reached
// over RabbitMQ; here the party is a stubbed provider reached in-process. AuthorizePayment is the
// request Orders sends to the provider boundary; PaymentDecision is the provider's transient
// reply. Both have local handlers, so Wolverine's conventional routing keeps them in-process
// (local routing takes precedence over the RabbitMQ convention) — no broker hop, no Contracts
// project (that is published language reserved for genuine cross-BC traffic, ADR 014).

// Request to the payment provider. amount is the order total, carried from the Order stream.
public record AuthorizePayment(string OrderId, decimal Amount);

// The provider's transient decision. Approved → AuthCode is set; declined → Reason is set. This
// response is never read again outside Orders: a downstream handler translates it once into the
// durable Klefter event (PaymentAuthorized / PaymentAuthFailed), which is the source of truth.
public record PaymentDecision(string OrderId, bool Approved, string? AuthCode, string? Reason);

// The provider boundary. A real integration (out of scope for round one — vision.md stubs
// payment) would implement this with a network call to a gateway; here it is a deterministic
// stub. Injected so tests can swap a declining implementation to exercise the failure branch
// without polluting the domain payload with magic values (the chosen stub policy).
public interface IPaymentProvider
{
    public Task<PaymentDecision> AuthorizeAsync(AuthorizePayment command);
}

// DEMO AFFORDANCE — a configurable artificial delay before the stub responds, so the order's
// stock_reserved → payment_authorized → confirmed steps are visible during a live demo.
// Default is zero (no delay) everywhere except the AppHost's demo wiring. Mirrors the
// PaymentDeclinePolicy / PaymentDeadline config-singleton pattern.
public record PaymentAuthDelay(TimeSpan Duration)
{
    public static readonly TimeSpan Default = TimeSpan.Zero;
}

// DEMO AFFORDANCE — the policy that lets the stub decline on demand. `DeclineOverAmount` is null by
// default (and in tests): the stub then approves everything, exactly as round one always did. When the
// AppHost sets `Payment:DeclineOverAmount` for the live demo (src/CritterMart.AppHost/Program.cs), the
// stub declines any order whose total exceeds the threshold — so the slice-4.6 payment-DECLINE path
// (cancel + compensating ReleaseStock back to Inventory) is triggerable live, without swapping providers
// or restarting: a small order confirms, a large one cancels. This is config-gated *stub* behavior, NOT
// a domain rule and NOT a magic value in the command payload (the policy the codebase deliberately chose
// over payload magic — see PaymentAuthorizationTests' DecliningPaymentProvider). See docs/demo-runbook.md.
public record PaymentDeclinePolicy(decimal? DeclineOverAmount);

// Round-one stub: approves by default, returning a synthetic "stub-…" auth code. The failure branch
// (slice 4.3's decline path → PaymentAuthFailed, precondition for slice 4.6) is exercised in tests by
// registering a declining IPaymentProvider in place of this one, and — for the live demo — by the
// PaymentDeclinePolicy threshold above. A real gateway integration would replace this registration.
public class StubPaymentProvider(PaymentDeclinePolicy policy) : IPaymentProvider
{
    public Task<PaymentDecision> AuthorizeAsync(AuthorizePayment command)
    {
        // Demo decline: total over the configured threshold → decline (precondition for the slice-4.6
        // cancel-and-release). Unset threshold → this branch never runs → always approve.
        if (policy.DeclineOverAmount is { } threshold && command.Amount > threshold)
        {
            return Task.FromResult(new PaymentDecision(
                command.OrderId, Approved: false, AuthCode: null,
                Reason: $"declined (demo): order total {command.Amount} exceeds threshold {threshold}"));
        }

        return Task.FromResult(new PaymentDecision(
            command.OrderId, Approved: true, AuthCode: $"stub-{Guid.NewGuid():N}", Reason: null));
    }
}
