# shopping-cart Delta — Slice 3.5 View My Open Cart

## ADDED Requirements

### Requirement: Read the Customer's open cart

The system SHALL expose the Customer's single open cart as a read over the existing `CartView` read model, resolved by the Customer's identity rather than by `cartId`. The Customer's identity SHALL arrive ambiently on the request — the `X-Customer-Id` header, the round-one stubbed customer id behind the ADR 009 `useCurrentCustomer` seam (the stand-in for an authenticated claim) — not in the route or the request body.

- When the identity resolves to a Customer who has exactly one open `CartView` (the partial-unique open-cart index guarantees at most one), the system SHALL return that `CartView` with its SKU-keyed line items at their snapshotted names and prices.
- When the identity resolves to a Customer who has no open cart — none was ever created, or the most recent cart is `CartCheckedOut` (placed an order) or `CartAbandoned` (inactivity) — the system SHALL respond `404`. This is a "no open cart" signal the storefront renders as an empty cart, not an error condition.
- When no customer identity is supplied (a missing or blank `X-Customer-Id` header), the system SHALL reject the request with `400` — the request carries no identity against which to resolve a cart.

This read appends no event and reads no read model other than `CartView`. It is the customer-keyed read counterpart to the customer-keyed *write* side every cart command already uses (slices 3.1–3.3 resolve the open cart by customer; this exposes the same resolution as a read), closing the pre-frontend audit's blocking Gap #1: without it the cart-review screen (wireframe W2) cannot render on a cold load, when the storefront holds only the stubbed customer id and no `cartId`.

#### Scenario: Return the Customer's open cart

- **GIVEN** the Customer `customer-X` has an open `Cart` stream recording `CartCreated`, `CartItemAdded { sku: "crit-001" }`, and `CartItemAdded { sku: "crit-002" }` (no `CartCheckedOut` or `CartAbandoned`)
- **WHEN** the storefront requests `GET /carts/mine` with header `X-Customer-Id: customer-X`
- **THEN** the single open `CartView` for `customer-X` is returned, with two SKU-keyed lines (`crit-001`, `crit-002`) at their snapshot prices
- **AND** no event is appended to the `Cart` stream

#### Scenario: A Customer with no open cart gets a 404

- **GIVEN** the Customer `customer-X` has no open cart — either none was ever created, or the most recent cart is `CartCheckedOut` or `CartAbandoned`
- **WHEN** the storefront requests `GET /carts/mine` with header `X-Customer-Id: customer-X`
- **THEN** the response is `404` ("no open cart"), not an error
- **AND** no event is appended to the `Cart` stream

#### Scenario: A request with no customer identity is rejected

- **GIVEN** any state of the Customer's carts
- **WHEN** the storefront requests `GET /carts/mine` with a missing or blank `X-Customer-Id` header
- **THEN** the response is `400` — the request carries no identity to resolve a cart
- **AND** no event is appended to any `Cart` stream
