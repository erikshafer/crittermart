using Contracts = CritterMart.Contracts;
using Marten;

namespace CritterMart.Orders.Order;

// The payment gate, slices 4.3 (authorize) + 4.4 (confirm) + 4.6 (cancel on decline). This mirrors
// slice 4.2's reserve flow, but the provider call is in-process: a request hop that calls the
// provider boundary, then a translation hop that records the decision as a Klefter local commit on
// the Order stream and makes the follow-on aggregate decision (confirm or cancel-and-release). The
// chain is kicked off by StockReservedHandler cascading AuthorizePayment once the stock gate clears
// (see StockReservationOutcomeHandlers). The cancel path is the one hop that leaves the process:
// it publishes ReleaseStock back to Inventory over the broker.

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
// local commit on the Order stream, and make the follow-on aggregate decision — confirm on
// approval (slice 4.4) or cancel-and-release on decline (slice 4.6). Idempotent and order-sensitive
// via a stream-state guard (mirrors the 4.2 outcome handlers): it acts only while the order sits at
// the payment gate (stock_reserved). A duplicate decision, or one for an order already confirmed /
// terminal / unknown, is a silent no-op (returns null, so nothing is appended and nothing cascades).
//
// Returns ReleaseStock? as a cascading message: the decline path returns the cross-BC release to
// publish, while the approve and no-op paths return null (a null return suppresses the cascade —
// verified against the Wolverine docs). ReleaseStock has no Orders-local handler, so conventional
// routing carries it to Inventory over RabbitMQ (the same precedence that routes ReserveStock).
public static class PaymentDecisionHandler
{
    public static async Task<Contracts.ReleaseStock?> Handle(PaymentDecision message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<OrderStatusView>(message.OrderId);
        if (stream.Aggregate?.Status != OrderStatus.StockReserved)
        {
            return null; // not at the payment gate (already decided, terminal, or unknown) — ignore
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
            return null; // success path cascades nothing
        }

        // Slice 4.6 decline branch (the 4.5 shape: append the failure commit, then cancel). Record
        // the refusal, then make the order terminal with OrderCancelled { payment_declined } in the
        // same commit. Unlike the 4.5 stock-failure cancel, stock WAS reserved before the payment
        // gate was reached, so this path must release it: cascade ReleaseStock carrying the order's
        // lines (read from the Order stream — the source of truth) to Inventory (slice 2.3). The
        // guard above proved we are at the payment gate, so the reservation is guaranteed to exist.
        stream.AppendOne(new PaymentAuthFailed(message.OrderId, message.Reason ?? "declined"));
        stream.AppendOne(new OrderCancelled(message.OrderId, CancelReason.PaymentDeclined));

        var lines = stream.Aggregate.Lines
            .Select(l => new Contracts.ReleaseStockLine(l.Sku, l.Quantity))
            .ToList();
        return new Contracts.ReleaseStock(message.OrderId, lines);
    }
}
