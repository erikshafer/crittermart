namespace CritterMart.Orders.Cart;

// Terminal-failure event of a Cart stream (Workshop 001 § 4, slice 3.4): the cart sat inactive
// past the configured window and the Bruun temporal automation abandoned it. The cart is now
// closed — CartView.IsOpen flips to false, which frees the partial-unique index so the customer
// can start a fresh cart, exactly like CartCheckedOut (the stream's other terminal).
//
// The event is deliberately FATTER than the workshop's `{ reason }` sketch (design.md Decision 3):
// it snapshots the abandoned cart's lines and computed total at the moment of abandonment, so the
// async CartAbandonmentReport (a multi-stream projection) can fold value and SKU counts without
// reaching back into this stream. Record the decision with the data it was made on.
public record CartAbandoned(string Reason, IReadOnlyList<CartLine> Lines, decimal TotalValue);

// Abandonment reasons carried by CartAbandoned. Round one has exactly one: the inactivity
// timeout (slice 3.4). A future round could add e.g. customer-initiated abandonment.
public static class CartAbandonReason
{
    public const string InactivityTimeout = "inactivity_timeout";
}
