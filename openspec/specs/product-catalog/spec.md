# product-catalog Specification

## Purpose

The `product-catalog` capability is the single-seller storefront's catalog: the Seller publishes products (each identified by a unique SKU and carrying a name, description, and price), re-prices them, and Customers browse the published set. It is the Catalog bounded context's one capability and CritterMart's only **document-store-backed** context — products are Marten documents surfaced through the inline `ProductCatalogView` read model, with a per-product `ProductPublished` event recorded purely as an audit moment (not an event-sourced aggregate, in deliberate contrast to the event-sourced Inventory and Orders contexts). The price a product carries here is the list price; once a Customer adds an item to a cart it is snapshotted into the Orders BC, so later re-pricing never alters an in-flight cart or order. Slices 1.1 (publish), 1.2 (browse), and 1.3 (change price) make up this capability; round one models no categories, search, media, or stock levels (stock is the Inventory BC's `stock-management`).

## Requirements
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

### Requirement: Browse the product catalog

The system SHALL expose all published products through the `ProductCatalogView` so that a customer can browse the storefront catalog. For each product the listing SHALL return its SKU, name, description, and current price. Browsing is read-only: it SHALL NOT modify any product document and SHALL NOT record any event.

#### Scenario: Browse published products

- **GIVEN** two products are published: `crit-001` "Cosmic Critter Plush" at price `24.99`, and `crit-002` "Nebula Newt" at price `18.00`
- **WHEN** the customer requests `GET /products`
- **THEN** the `ProductCatalogView` returns both products
- **AND** each returned product shows its SKU, name, description, and current price

### Requirement: Change a product's price

The system SHALL allow the Seller to change the listed price of a published product, identified by its SKU. On a price change the system SHALL update the product's current price to the new value and SHALL record a `ProductPriceChanged` lifecycle moment carrying both the previous price and the new price. The catalog listing SHALL reflect the new price.

#### Scenario: Change a published product's price

- **GIVEN** a product with SKU `crit-001` "Cosmic Critter Plush" is published at price `24.99`
- **WHEN** the Seller issues `ChangeProductPrice { sku: "crit-001", newPrice: 19.99 }`
- **THEN** the system records a `ProductPriceChanged` lifecycle moment carrying the old price `24.99` and the new price `19.99`
- **AND** the product's current price becomes `19.99`
- **AND** the catalog listing shows `crit-001` at `19.99`

