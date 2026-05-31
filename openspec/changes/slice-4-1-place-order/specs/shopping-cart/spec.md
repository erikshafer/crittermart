## ADDED Requirements

### Requirement: Check out the cart on order placement

The system SHALL terminate the Customer's open cart when an order is placed from it. In the same transaction that records `OrderPlaced` on the new Order stream, the system SHALL append a `CartCheckedOut` event carrying the new `orderId` to the cart's stream, and the inline `CartView` SHALL set `IsOpen` to false. A checked-out cart SHALL no longer be resolved as the customer's open cart, so the customer is free to start a new cart, and a repeat placement against the same cart SHALL find no open cart. The checked-out cart's line items SHALL be retained as readable history.

#### Scenario: Placing an order checks out the cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream with one or more line items
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** the `Cart` stream appends `CartCheckedOut { orderId }` in the same transaction as `OrderPlaced`
- **AND** the `CartView` for that cart has `IsOpen` set to false while its line items are retained
- **AND** the customer no longer has an open cart
