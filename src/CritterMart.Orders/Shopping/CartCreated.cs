namespace CritterMart.Orders.Shopping;

// Genesis event of a Cart stream — appended on the customer's first add when they have
// no open cart. The stream is keyed by a generated cartId (a new stream per cart
// lifecycle, parallel to Order's orderId; see design.md decision 1), not by customerId.
// (Workshop 001 § 2, § 4, slice 3.1.)
public record CartCreated(string CartId, string CustomerId);
