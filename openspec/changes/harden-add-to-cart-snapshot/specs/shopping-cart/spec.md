# shopping-cart Delta — Harden AddToCart against a malformed product snapshot

## ADDED Requirements

### Requirement: Reject an add-to-cart command with no usable product snapshot

The system SHALL reject an `AddToCart` command that carries no usable product snapshot with `400` (a malformed command) and SHALL append no event. The cart never reads the Catalog — the product snapshot (name and price) the storefront composed is a cart line's only source of product truth — so a command with no usable snapshot has nothing from which to build a line. A product snapshot is unusable when it is **absent** (no `productSnapshot` on the command), its **name is blank**, or its **price is negative**. This guard SHALL run before the Customer's open cart is resolved or created, so a malformed command never starts a new `Cart` stream and never appends a `CartItemAdded` event — the malformed command is stopped at the boundary, never becoming cart history.

This is a malformed-*input* rejection, distinct from the domain-state rejections on the cart's edit path (`CartItemNotPresent`, `NoOpenCart`): those refuse a well-formed command that does not fit the cart's current state; this refuses a command that is not well-formed at all.

#### Scenario: Reject an add with no product snapshot

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1 }` with no `productSnapshot`
- **THEN** the command is rejected with `400`
- **AND** no `Cart` stream is created for `customer-X` and no event is appended

#### Scenario: Reject an add whose snapshot has a blank name

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { name: "", price: 24.99 } }`
- **THEN** the command is rejected with `400`
- **AND** no event is appended to any `Cart` stream

#### Scenario: Reject an add whose snapshot has a negative price

- **GIVEN** the Customer `customer-X` has no open cart
- **WHEN** the Customer issues `AddToCart { sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: -1.00 } }`
- **THEN** the command is rejected with `400`
- **AND** no event is appended to any `Cart` stream
