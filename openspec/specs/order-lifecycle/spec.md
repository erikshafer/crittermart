# order-lifecycle Specification

## Purpose

The `order-lifecycle` capability manages the Order aggregate's event-sourced stream (keyed by a generated `orderId`) from placement to its terminal state, projected into an inline `OrderStatusView` read model. Slice 4.1 covers placing an order from the cart (`OrderPlaced`, status `awaiting_confirmation`); later slices fold cross-BC stock reservation (4.2), stubbed payment authorization (4.3), confirmation when both gates close (4.4), and cancellation on stock failure / payment decline / payment timeout (4.5–4.7) onto the same stream — the Order aggregate acting as its own process manager (Process Manager via Handlers, ADR 007). The terminal state is `OrderConfirmed` or `OrderCancelled`; CritterMart models no shipping or delivery. This is one of the Orders bounded context's two capabilities; the other is `shopping-cart` (the Cart aggregate).

## Requirements
### Requirement: Place an order from the cart

The system SHALL allow the Customer to place an order from their open cart. When the Customer has an open cart with at least one line, the system SHALL create a new `Order` stream keyed by a generated `orderId` and append an `OrderPlaced` event carrying the customer id, the cart's line items (SKU, quantity, and snapshotted name and price), and a total equal to the sum of each line's quantity multiplied by its snapshotted price. The system SHALL maintain an inline `OrderStatusView` read model whose status, line items, and total reflect the `OrderPlaced` event, with status `awaiting_confirmation`. When the Customer has no open cart, the system SHALL reject the command and create no `Order` stream. When the Customer's open cart has no lines, the system SHALL reject the command and create no `Order` stream. The order's lines and total are taken from the cart's snapshot and are authoritative — the order does not read the Catalog.

#### Scenario: Place an order from an open cart

- **GIVEN** the Customer `customer-X` has an open cart with `crit-001` quantity `2` at `24.99` and `crit-002` quantity `3` at `18.00`
- **WHEN** the Customer issues `PlaceOrder { customerId: "customer-X" }`
- **THEN** a new `Order` stream keyed by a generated `orderId` records `OrderPlaced { orderId, customerId: "customer-X", items: [{ sku: "crit-001", quantity: 2, name: "Cosmic Critter Plush", price: 24.99 }, { sku: "crit-002", quantity: 3, name: "Nebula Newt", price: 18.00 }], total: 103.98 }`
- **AND** the `OrderStatusView` for that order shows status `awaiting_confirmation`, the two lines, and total `103.98`

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

