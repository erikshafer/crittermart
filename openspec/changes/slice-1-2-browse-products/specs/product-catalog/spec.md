## ADDED Requirements

### Requirement: Browse the product catalog

The system SHALL expose all published products through the `ProductCatalogView` so that a customer can browse the storefront catalog. For each product the listing SHALL return its SKU, name, description, and current price. Browsing is read-only: it SHALL NOT modify any product document and SHALL NOT record any event.

#### Scenario: Browse published products

- **GIVEN** two products are published: `crit-001` "Cosmic Critter Plush" at price `24.99`, and `crit-002` "Nebula Newt" at price `18.00`
- **WHEN** the customer requests `GET /products`
- **THEN** the `ProductCatalogView` returns both products
- **AND** each returned product shows its SKU, name, description, and current price
