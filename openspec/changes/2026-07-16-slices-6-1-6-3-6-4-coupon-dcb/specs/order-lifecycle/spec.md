## MODIFIED Requirements

### Requirement: Place an order from the cart

The system SHALL allow the Customer to place an order from their open cart. When the Customer has an open cart with at least one line, the system SHALL create a new `Order` stream keyed by a generated `orderId` and append an `OrderPlaced` event carrying the customer id, the cart's line items (SKU, quantity, and snapshotted name and price), the order `subtotal` equal to the sum of each line's quantity multiplied by its snapshotted price, the `discount` applied (zero when no coupon is redeemed), and a `total` equal to `subtotal` minus `discount`. The system SHALL maintain an inline `OrderStatusView` read model whose status, line items, subtotal, discount, and total reflect the `OrderPlaced` event, with status `awaiting_confirmation`. When the Customer has no open cart, the system SHALL reject the command and create no `Order` stream. When the Customer's open cart has no lines, the system SHALL reject the command and create no `Order` stream. The order's lines and subtotal are taken from the cart's snapshot and are authoritative — the order does not read the Catalog.

`PlaceOrder` MAY carry an optional `couponCode`. When absent, `discount` SHALL be zero, `total` SHALL equal `subtotal`, no coupon boundary is opened, and this requirement's behavior is byte-for-byte unchanged from slice 4.1. When present, the redemption and cap enforcement are governed by the `coupon-promotion` capability: the system SHALL resolve the code, enforce the global per-coupon cap through the DCB boundary, and — on success — append a tagged `CouponRedeemed` to the same new `Order` stream in the **same transaction** as `OrderPlaced`, with the `OrderPlaced` carrying the discounted pricing; a cap-breach or an unknown code SHALL reject the placement (`409 CouponExhausted` / `409 CouponInvalid`) so that no `Order` stream is created. The `OrderStatusView` wire shape gains `subtotal`, `discount`, and `couponCode` as additive fields alongside the existing `total`; existing consumers reading the prior shape are unaffected.

#### Scenario: Place an order from an open cart

- **GIVEN** the Customer `customer-X` has an open cart with `crit-001` quantity `2` at `24.99` and `crit-002` quantity `3` at `18.00`
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** a new `Order` stream keyed by a generated `orderId` records `OrderPlaced { orderId, customerId: "customer-X", items: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }, { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.00 }], subtotal: 103.98, discount: 0, total: 103.98 }`
- **AND** the `OrderStatusView` for that order shows status `awaiting_confirmation`, the two lines, subtotal `103.98`, discount `0`, and total `103.98`

#### Scenario: Place a discounted order with a valid coupon

- **GIVEN** the Customer `customer-X` has an open cart totalling `40.00` and `CouponDefined { code: "FLASH20", discountPercent: 20, cap: 3 }` has a net redemption count below its cap
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X", couponCode: "FLASH20" }`
- **THEN** a new `Order` stream records `OrderPlaced { subtotal: 40.00, discount: 8.00, total: 32.00 }` and, in the same transaction, a `CouponRedeemed { couponId, discount: 8.00 }` tagged with the `CouponId`
- **AND** the `OrderStatusView` for that order shows subtotal `40.00`, discount `8.00`, total `32.00`, and `couponCode: "FLASH20"`

#### Scenario: Reject placement when the customer has no open cart

- **GIVEN** the Customer `customer-Y` has no open cart
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-Y" }`
- **THEN** the command is rejected with a `409` response
- **AND** no `Order` stream is created

#### Scenario: Reject a second placement after checkout

- **GIVEN** the Customer `customer-X` has already placed an order from their cart (the cart is checked out and no longer open)
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }` again
- **THEN** the command is rejected with a `409` response
- **AND** no second `Order` stream is created

### Requirement: Surface placement time and cancellation reason in the order view

The system SHALL surface, in the inline `OrderStatusView` read model, the time the order was placed, the order's pricing breakdown, and — once the order is cancelled — the reason it was cancelled, in addition to the order's status, line items, and total. The view SHALL carry a `placedAt` timestamp set at genesis to the append time of the order's `OrderPlaced` event (event metadata, not a new event field), present for every order from the moment it is placed. The view SHALL carry `subtotal`, `discount`, and `couponCode` fields sourced from the `OrderPlaced` event: `subtotal` is the pre-discount line total, `discount` is the amount taken off (zero when no coupon was redeemed), and `couponCode` is the redeemed code or null when none was applied. The view SHALL carry a `cancelReason` field that is null while the order has not been cancelled and, once an `OrderCancelled` event is recorded on the stream, carries that event's reason — one of `stock_unavailable`, `payment_declined`, or `payment_timeout`. This enrichment SHALL preserve the existing `OrderStatusView` wire shape `{ id, customerId, status, lines, total, placedAt, cancelReason }` as a superset: `subtotal`, `discount`, and `couponCode` are added alongside the existing fields, none of which is removed or renamed, so existing consumers are unaffected. The enrichment appends no event, sends no message, and reads no stream other than the order's own.

#### Scenario: A placed order's view carries its placement time and pricing

- **GIVEN** the Customer places an order `ord-A` with subtotal `103.98` and no coupon, and `OrderPlaced { orderId: "ord-A" }` is appended to its stream
- **WHEN** the `OrderStatusView` for `ord-A` is read
- **THEN** its `placedAt` equals the append time of the `OrderPlaced` event
- **AND** its `subtotal` is `103.98`, its `discount` is `0`, its `total` is `103.98`, and its `couponCode` is null
- **AND** its `cancelReason` is null (the order is not cancelled)

#### Scenario: A discounted order's view carries the coupon and discount

- **GIVEN** the Customer places an order `ord-D` redeeming `FLASH20` (20% off) on a `40.00` subtotal
- **WHEN** the `OrderStatusView` for `ord-D` is read
- **THEN** its `subtotal` is `40.00`, its `discount` is `8.00`, its `total` is `32.00`, and its `couponCode` is `FLASH20`

#### Scenario: A cancelled order's view carries the cancellation reason

- **GIVEN** the order `ord-B` stream shows `OrderPlaced` and then `OrderCancelled { orderId: "ord-B", reason: "stock_unavailable" }`
- **WHEN** the `OrderStatusView` for `ord-B` is read
- **THEN** its `status` is `cancelled`
- **AND** its `cancelReason` is `stock_unavailable`
- **AND** its `placedAt` still equals the append time of the `OrderPlaced` event
