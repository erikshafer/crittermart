---
narrative: 004
title: The Customer Buys From the Catalog
actor: Customer
status: draft
version: v1.0
slices: [3.1]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 2 Orders BC / Cart aggregate, § 4 Cart vocabulary, § 5 slice 3.1, § 6.1 GWT)
  - docs/narratives/002-customer-browse-catalog.md (the browsing step this journey continues from)
  - docs/narratives/001-seller-manage-catalog.md (the price-snapshot non-event, mirrored here from the Customer's side)
  - docs/context-map/README.md (product fields reach the Cart only via the frontend snapshot; no Catalog↔Orders integration)
---

# Narrative 004 — The Customer Buys From the Catalog

This is the same Customer as [Narrative 002](002-customer-browse-catalog.md), now past browsing and deciding to buy. Their journey here is the purchasing arc: filling a **cart**, placing an **order**, and watching it get fulfilled. Where the Catalog is a document store, the **Orders bounded context is event-sourced** — the Cart is an event-sourced aggregate (a stream of what the Customer added, removed, and changed), and the readable cart contents are projected from those events. The Customer's identity is stubbed for round one (a hardcoded customer ID flows through their commands, per ADR 009).

## Journey scope

The Customer's purchasing journey threads several Orders slices:

- **Slice 3.1 — Add item to cart.** Authored in this version. Moment 1 below covers adding the first item.

Forthcoming (not authored here): editing the cart — remove (3.2), change quantity (3.3); **placing the order** (4.1, where the cart becomes an order); the order's **fulfillment** — stock reservation (4.2), payment (4.3), and confirmation or cancellation (4.4–4.7); and **cart abandonment** (3.4) if the cart is left inactive. Each extends this narrative when its slice is authored.

Browsing the catalog is the prior step ([Narrative 002](002-customer-browse-catalog.md)); catalog *management* is the Seller's journey ([Narrative 001](001-seller-manage-catalog.md)).

## Moment 1 — Adding the first item to the cart

**Context.** The Customer has been browsing the storefront ([Narrative 002](002-customer-browse-catalog.md)) and has settled on the *Cosmic Critter Plush* (SKU `crit-001`, listed at `$24.99`). They have no cart yet — this is their first action toward a purchase.

**Interaction.** On the product listing, the Customer adds the *Cosmic Critter Plush* to their cart (quantity 1). The frontend already read this product from the Catalog when rendering the listing, so it **snapshots** the relevant fields — name and price — into the command: `AddToCart { customerId, sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`.

**System response.** The Orders service finds no Cart stream for this customer, so it **creates one**: it appends `CartCreated { customerId }` and then `CartItemAdded { sku: "crit-001", quantity: 1, snapshot }` to the new stream. The `CartView` projects from those events and shows a single line — *Cosmic Critter Plush*, quantity 1, at the snapshot price `$24.99`. If the Customer then adds a second product — say the *Nebula Newt* (`crit-002`, `$18.00`), quantity 3 — a second `CartItemAdded` is appended to the **same** cart, and `CartView` shows two lines. The cart is the Customer's working selection: a running event stream they keep shaping until they check out (slice 4.1) or it lapses (slice 3.4).

The price in the cart is the price **snapshotted at add-to-cart time**. This is the Customer-side mirror of the non-event named in [Narrative 001](001-seller-manage-catalog.md): product information reaches the cart only through the frontend's snapshot (the context map calls this presentation-layer composition, not a bounded-context integration), and the cart's snapshot is authoritative through checkout.

## What the Customer does *not* yet see

- **No re-pricing of the cart.** If the Seller changes `crit-001`'s price (slice 1.3) after it is in the cart, the cart still shows the snapshot `$24.99`. The snapshot wins until checkout — by design.
- **No stock check at add time.** Adding an item neither verifies nor reserves stock. A product can sit in the cart and still turn out to be unreservable later; stock is reserved only at checkout (slice 4.2). So the cart never says "out of stock" in this slice.
- **No checkout, payment, or order yet.** The cart is a selection, not a commitment. Turning it into an order (`PlaceOrder`, slice 4.1) and the fulfillment that follows (4.2–4.7) come later.
- **No abandonment yet.** If the Customer walks away, nothing happens to the cart in this slice; the inactivity timeout and `CartAbandoned` arrive with slice 3.4.

## Forthcoming Moments

The Customer's purchasing journey will gain Moments as the Orders slices are authored:

- **Editing the cart** — removing an item (slice 3.2) and changing a quantity (slice 3.3).
- **Placing the order** (slice 4.1) — checkout turns the cart into an order: `OrderPlaced` on a new Order stream, `CartCheckedOut` on the cart.
- **Fulfillment** (slices 4.2–4.7) — what the Customer sees as the order is processed: stock reserved cross-BC, payment authorized (stubbed), and the order confirmed — or cancelled on stock failure, payment decline, or timeout.
- **Abandonment** (slice 3.4) — the cart times out if left inactive and is marked `CartAbandoned`.

When those land, this narrative's `slices` frontmatter grows, the version bumps, and `## Document History` records the amendment.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                            |
| ------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-28 | Initial commit. Covers Workshop 001 slice 3.1 (Add item to cart) as Moment 1: the Customer adding their first item, the frontend snapshotting product fields into `AddToCart`, the new Cart stream (`CartCreated` + `CartItemAdded`), and `CartView`. Continues the Customer's journey from Narrative 002 (browse). Cart edits (3.2/3.3), place-order (4.1), fulfillment (4.2–4.7), and abandonment (3.4) noted as forthcoming. The cart-inactivity timeout (Bruun) is deferred to slice 3.4. |
