using Marten;
using Wolverine;

namespace CritterMart.Orders.Shopping;

// The fired inactivity deadline (Workshop 001 slice 3.4, the second Bruun temporal automation;
// mirrors Order/PaymentTimeoutHandler). Scheduled by AddToCart when the cart was created;
// delivered here when the deadline passes. Fire-and-check (design.md Decision 1): the handler
// decides against the cart's own fold — never against the CartsAwaitingActivity todo-list —
// and takes one of three paths:
//
//   closed or unknown cart → silent no-op (losing the race to checkout is the timer's normal fate)
//   activity intervened    → reschedule from the last activity; no event appended
//   genuinely inactive     → append CartAbandoned, the stream's second terminal event
//
// The reschedule path exists because Wolverine has no API to cancel a scheduled message (verified
// via ctx7; Workshop § 8 item 1 overstates this) — cart activity never moves the pending timeout,
// it just makes the fired timeout re-aim itself at the true deadline. Exactly one timeout is in
// flight per open cart at any moment.
public static class CartAbandonmentHandler
{
    public static async Task<DeliveryMessage<CartActivityTimeout>?> Handle(
        CartActivityTimeout message,
        IDocumentSession session,
        CartActivityDeadline deadline,
        TimeProvider time)
    {
        var stream = await session.Events.FetchForWriting<Cart>(message.CartId);

        // Terminal-state guard: checked out, already abandoned (including by a duplicate of this
        // very timeout), or unknown — append nothing, schedule nothing.
        if (stream.Aggregate is null || !stream.Aggregate.IsOpen)
        {
            return null;
        }

        // Fire-and-check: the cart's true deadline is its newest activity plus the window.
        var dueAt = stream.Aggregate.LastActivityAt.Add(deadline.Duration);
        var now = time.GetUtcNow();

        if (dueAt > now)
        {
            // Activity intervened since this timeout was scheduled — re-aim at the true deadline.
            return new CartActivityTimeout(message.CartId).DelayedFor(dueAt - now);
        }

        // Genuinely inactive: abandon. The event snapshots the folded cart's lines + total
        // (design.md Decision 3) so the async CartAbandonmentReport can fold value and SKU
        // counts without reaching back into this stream.
        var lines = stream.Aggregate.Lines.ToList();
        var totalValue = lines.Sum(l => l.Quantity * l.Price);
        stream.AppendOne(new CartAbandoned(CartAbandonReason.InactivityTimeout, lines, totalValue));

        return null;
    }
}
