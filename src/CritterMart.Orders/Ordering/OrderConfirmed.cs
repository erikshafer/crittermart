namespace CritterMart.Orders.Ordering;

// Terminal success event on the Order stream (Workshop 001 § 4, slice 4.4, ADR 007). The
// aggregate-as-process-manager appends this once BOTH gates are closed — stock reserved AND
// payment authorized. In CritterMart the terminal of a successful order is "confirmed", not
// "shipped" or "delivered": the model carries no logistics (vision.md non-goal). Paired with
// PaymentAuthorized in the same transaction, since payment is always the second gate to close.
public record OrderConfirmed(string OrderId);
