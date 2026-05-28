## ADDED Requirements

### Requirement: Change a product's price

The system SHALL allow the Seller to change the listed price of a published product, identified by its SKU. On a price change the system SHALL update the product's current price to the new value and SHALL record a `ProductPriceChanged` lifecycle moment carrying both the previous price and the new price. The catalog listing SHALL reflect the new price.

#### Scenario: Change a published product's price

- **GIVEN** a product with SKU `crit-001` "Cosmic Critter Plush" is published at price `24.99`
- **WHEN** the Seller issues `ChangeProductPrice { sku: "crit-001", newPrice: 19.99 }`
- **THEN** the system records a `ProductPriceChanged` lifecycle moment carrying the old price `24.99` and the new price `19.99`
- **AND** the product's current price becomes `19.99`
- **AND** the catalog listing shows `crit-001` at `19.99`
