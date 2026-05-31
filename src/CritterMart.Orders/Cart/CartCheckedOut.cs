namespace CritterMart.Orders.Cart;

// Terminal-success event of a Cart stream (Workshop 001 § 4, slice 4.1). The Customer checked
// out: the cart is now closed — CartView.IsOpen flips to false, which frees the partial-unique
// index so the customer can start a fresh cart. Carries the orderId of the Order placed in the
// same transaction; paired with OrderPlaced on the new Order stream.
public record CartCheckedOut(string OrderId);
