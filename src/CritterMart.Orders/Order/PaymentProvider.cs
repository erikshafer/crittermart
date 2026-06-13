namespace CritterMart.Orders.Order;

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

// Round-one stub: always approves, returning a synthetic "stub-…" auth code. The failure branch
// (slice 4.3's decline path → PaymentAuthFailed, precondition for slice 4.6) is exercised in
// tests by registering a declining IPaymentProvider in place of this one.
public class StubPaymentProvider : IPaymentProvider
{
    public Task<PaymentDecision> AuthorizeAsync(AuthorizePayment command) =>
        Task.FromResult(new PaymentDecision(
            command.OrderId, Approved: true, AuthCode: $"stub-{Guid.NewGuid():N}", Reason: null));
}
