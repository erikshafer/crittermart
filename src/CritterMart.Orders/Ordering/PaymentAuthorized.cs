namespace CritterMart.Orders.Ordering;

// Klefter local commit on the Order stream (Workshop 001 § 4, slice 4.3): Orders records the
// stubbed payment provider's authorization as a first-class fact on the order's own stream.
// Carries the provider's authCode and the authorized amount (the order total). The provider's
// transient response is never read again — this event is the source of truth for the decision
// (the Klefter audit-trail principle). Parallels the stock gate's Order-stream StockReserved.
public record PaymentAuthorized(string OrderId, string AuthCode, decimal Amount);
