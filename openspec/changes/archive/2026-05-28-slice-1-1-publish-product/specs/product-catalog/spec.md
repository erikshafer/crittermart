## ADDED Requirements

### Requirement: Publish a product to the catalog

The system SHALL allow the Seller to publish a product to the storefront catalog, identified by a unique SKU and carrying a name, description, and price. On publication the system SHALL record a `ProductPublished` lifecycle moment for audit, and SHALL surface the product through the `ProductCatalogView` read model so customers can browse it.

#### Scenario: Publish a new product

- **GIVEN** no product exists with SKU `crit-001`
- **WHEN** the Seller issues `PublishProduct { sku: "crit-001", name: "Cosmic Critter Plush", description: "...", price: 24.99 }`
- **THEN** the system records a `ProductPublished` lifecycle moment
- **AND** the `ProductCatalogView` shows the new product with its name and price

### Requirement: Product SKUs are unique in the catalog

The system SHALL reject any attempt to publish a product whose SKU already exists in the catalog. The rejection SHALL be idempotent: no new product document is created, and no additional `ProductPublished` lifecycle moment is recorded.

#### Scenario: Reject a duplicate SKU

- **GIVEN** a product with SKU `crit-001` already exists
- **WHEN** the Seller issues `PublishProduct { sku: "crit-001", ... }` again
- **THEN** the command is rejected with `ProductAlreadyPublished`
- **AND** no new `ProductPublished` lifecycle moment is recorded
- **AND** the existing product document is unchanged
