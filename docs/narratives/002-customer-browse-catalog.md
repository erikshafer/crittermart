---
narrative: 002
title: The Customer Browses the Catalog
actor: Customer
status: draft
version: v1.0
slices: [1.2]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 2 Catalog BC, § 5 slice 1.2, § 6.1 GWT)
  - docs/vision.md (single-seller storefront, customer framing)
  - docs/context-map/README.md (Catalog has no round-one cross-BC integration; Identity Conformist/stubbed)
  - docs/narratives/001-seller-manage-catalog.md (sibling Seller journey; the products this Customer browses were published there)
---

# Narrative 002 — The Customer Browses the Catalog

The Customer in CritterMart is a shopper visiting a one-person critter-merchandise storefront. They arrive to discover what is for sale — plush, vinyl, the occasional oddity — before deciding to put anything in a cart. They have no account to manage and no relationship with the Seller beyond the storefront itself; their identity is stubbed for round one (a hardcoded customer ID rides along with their requests as if it came from a real identity system, per ADR 009 and the context map's Conformist relationship), and browsing the public catalog does not depend on it.

Workshop 001 refers to this actor as the *customer* at the GWT level. This narrative uses *Customer* — the same word, capitalized — so no terminology bridge is needed here, unlike Narrative 001's *Seller*/*operator* split. The actor's name and the system's name for them coincide.

## Journey scope

The Customer's shopping journey begins with discovery and runs through purchase. Round one authors only its first step:

- **Slice 1.2 — Browse and view products.** Authored in this narrative version. One Moment below covers the Customer arriving at a populated storefront and seeing the published catalog.

The journey continues — adding an item to a cart (slice 3.1) and placing an order (slice 4.1) — but those steps live in the **Orders** bounded context, not Catalog. They are noted under *Forthcoming Moments* below and are not authored here; whether they extend this narrative or become a separate Customer-purchasing narrative is decided when those slices are picked up.

Catalog management — publishing products, changing prices — is the **Seller's** journey and lives in [Narrative 001](001-seller-manage-catalog.md). The products this Customer browses are exactly the ones the Seller published there.

## Moment 1 — Browsing the storefront catalog

**Context.** The Seller has been at work: the storefront now lists two products — the *Cosmic Critter Plush* (SKU `crit-001`, `$24.99`) and the *Nebula Newt* (SKU `crit-002`, `$18.00`), each published through the Seller's catalog page (slice 1.1). The Customer has never visited before. They open the storefront wanting to see what a critter shop actually stocks.

**Interaction.** The Customer lands on the storefront's product listing. The frontend asks the Catalog service directly for the published catalog — there is no aggregating gateway in front of it (the frontend calls each service directly in round one), so this is a straight read of Catalog's product listing.

**System response.** Catalog answers from the `ProductCatalogView` — the read shape over its `Product` document store. The view is a query over the documents the Seller published, not a rebuilt projection: the `Product` documents are the source of truth, and the listing simply reads them. The Customer sees both products, each with its name, current price, and description — enough to browse the shelf and read what each item is without leaving the listing. The *Cosmic Critter Plush* shows at `$24.99` and the *Nebula Newt* at `$18.00`. No other service is consulted: Catalog has no cross-bounded-context integration in round one, so nothing is fetched from Inventory or Orders to render the listing. The customer ID rides along with the request but gates nothing — the catalog is public.

From here, the Customer can read each product and decide whether to pursue it. The act of pursuing — adding to a cart — is the next step of the journey and the first that leaves the Catalog context.

## What the Customer does *not* yet see

Three absences in the round-one browsing experience are deliberate design choices, not gaps, and naming them keeps the journey honest:

- **No live stock availability.** The listing shows what the Seller published — name, price, description — but not whether an item is actually in stock. Stock lives in the Inventory context, and Catalog has no integration with Inventory in round one (per the context map). A product can appear on the storefront and still turn out to be unreservable at checkout; the Customer discovers that later in the Orders flow, not here.
- **No recommendations or cross-sell.** The listing is the flat published catalog in publication order. There is no "you might also like," no personalization, no merchandising. The single-seller storefront shows everything it sells, plainly.
- **No real-time price updates.** The price the Customer sees is the price as of their request. If the Seller changes a price (slice 1.3) while the Customer is browsing, the Customer does not see it move; they would see the new price only on a fresh request. And once the Customer adds an item to a cart, the Cart snapshots the price at add-to-cart time and that snapshot is authoritative through checkout — the mirror image of the price-snapshot non-event named from the Seller's side in Narrative 001.

## Forthcoming Moments

The Customer's journey continues beyond discovery, but into a different bounded context:

- **Adding an item to the cart (slice 3.1, Orders BC).** When the Customer decides to buy, the frontend snapshots the relevant product fields (price among them) into an `AddToCart` command; the Cart aggregate's snapshot becomes authoritative from that moment. This is where the Customer's journey crosses from Catalog into Orders.
- **Placing an order (slice 4.1 and the Place Order journey, Orders BC).** Checkout turns the cart into an order and begins the stock-reservation and payment process.

When those slices are authored, this narrative is revisited: either it grows to thread them (bumping to `v1.1` and appending Moments) or a dedicated Customer-purchasing narrative is created for the Orders-context steps. That decision is deferred to the session that picks up slice 3.1.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                       |
| ------- | ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-28 | Initial commit. Covers Workshop 001 slice 1.2 (Browse and view products) as one Moment: the Customer browsing a populated storefront and seeing the published catalog (`crit-001` Cosmic Critter Plush `$24.99`, `crit-002` Nebula Newt `$18.00`) via `ProductCatalogView`. No failure path (read-only query slice). Cart/order steps (slices 3.1, 4.1) noted as forthcoming, not authored. No actor-naming bridge needed (workshop *customer* = narrative *Customer*). |
