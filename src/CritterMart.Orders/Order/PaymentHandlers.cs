using Marten;

namespace CritterMart.Orders.Order;

// The payment gate, slices 4.3 (authorize) + 4.4 (confirm). This mirrors slice 4.2's reserve
// flow, but entirely in-process: a request hop that calls the provider boundary, then a
// translation hop that records the decision as a Klefter local commit on the Order stream. The
// chain is kicked off by StockReservedHandler cascading AuthorizePayment once the stock gate
// clears (see StockReservationOutcomeHandlers).

// Hop 1 of the payment gate: call the stubbed provider and cascade its decision. No stream
// access here — this handler is purely the "reach the provider" boundary (the in-process
// counterpart of slice 4.2's cross-BC Inventory hop). The returned PaymentDecision is a
// cascading message; it has a local handler below, so it is processed in-process.
public static class AuthorizePaymentHandler
{
    public static async Task<PaymentDecision> Handle(AuthorizePayment message, IPaymentProvider provider) =>
        await provider.AuthorizeAsync(message);
}

// Hop 2 of the payment gate: translate the provider's transient decision into a durable Klefter
// local commit on the Order stream, and — on approval — make the slice 4.4 aggregate decision to
// confirm. Idempotent and order-sensitive via a stream-state guard (mirrors the 4.2 outcome
// handlers): it acts only while the order sits at the payment gate (stock_reserved). A duplicate
// decision, or one for an order already confirmed / terminal / unknown, is a silent no-op.
public static class PaymentDecisionHandler
{
    public static async Task Handle(PaymentDecision message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<OrderStatusView>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.StockReserved)
        {
            return; // not at the payment gate (already decided, terminal, or unknown) — ignore
        }

        if (message.Approved)
        {
            // Slice 4.3 Klefter grant: the authorized amount is the order's own total (the
            // provider response is transient and not re-read — the stream is the source of truth).
            stream.AppendOne(new PaymentAuthorized(message.OrderId, message.AuthCode!, stream.Aggregate.Total));

            // Slice 4.4 aggregate decision: the stock gate was already cleared (the guard above
            // proved it) and payment is now authorized — both gates are closed, so the order
            // confirms. Payment is always the second gate to close (it only starts after stock is
            // reserved), so confirmation deterministically follows the grant in this same commit.
            stream.AppendOne(new OrderConfirmed(message.OrderId));
        }
        else
        {
            // Slice 4.3 failure branch: record the refusal as a Klefter commit. The cancellation
            // that turns this terminal — OrderCancelled { reason: "payment_declined" } plus the
            // cross-BC release of the reserved stock — is slice 4.6, deliberately deferred. Until
            // then the order's visible status stays stock_reserved with this failure on record.
            stream.AppendOne(new PaymentAuthFailed(message.OrderId, message.Reason ?? "declined"));
        }
    }
}
