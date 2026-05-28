---
narrative: 001
title: The Seller Manages the Catalog
actor: Seller
status: draft
version: v1.1
slices: [1.1, 1.3]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 2 Catalog BC, § 4 Catalog event vocabulary, § 5 slice 1.1, § 6.1 GWT scenarios)
  - docs/vision.md (single-seller framing)
  - docs/context-map/README.md (Catalog BC has no round-one cross-BC integration)
---

# Narrative 001 — The Seller Manages the Catalog

The Seller in CritterMart is a one-person operation. They source critter-themed merchandise — plush, vinyl, and the occasional oddity — and run the storefront alone, without a team and without an approval workflow. Their back-office surface talks directly to the Catalog service over HTTP; the commands they issue take effect immediately and are visible to customers on the storefront within the same request.

Workshop 001 refers to this actor as the *operator* at the GWT level. In this narrative the same person is called the *Seller* because that's how they would describe themselves to a customer or a tax authority. The two terms are interchangeable; the system shape is identical.

## Journey scope

The Seller's catalog-management journey threads two Workshop 001 slices in round one:

- **Slice 1.1 — Publish a product.** Authored in this narrative version. Two Moments below cover the happy path and the duplicate-SKU failure.
- **Slice 1.3 — Change a product's price.** Authored in this narrative version (v1.1). Moment 3 below covers the Seller dropping a product's price and the audit trail that records the change.

Slice 1.2 (Browse and view products) belongs to the *Customer's* journey and is excluded here; it is the subject of a separate, future narrative.

Long-road extensions — product discontinuation, promotion-bound pricing, multi-channel listings — are noted in Workshop 001 § 8 and `docs/vision.md` and do not appear in this narrative until their slices are authored.

## Moment 1 — Publishing the first product onto the storefront

**Context.** The Seller has just finished sourcing and packaging a new product: a *Cosmic Critter Plush* with SKU `crit-001`, retailing at `$24.99`. They open the back-office Catalog page and see an empty list — no products have been published yet. The storefront, today, has nothing for customers to browse.

**Interaction.** The Seller opens the *Publish a product* form. They enter the SKU `crit-001`, the name `Cosmic Critter Plush`, a short description, and the price `24.99`. They click *Publish*.

**System response.** The Catalog service receives the `PublishProduct` command, validates that the SKU is not already in use, persists a new `Product` document, and appends a `ProductPublished` lifecycle moment to the product's audit trail. The back-office Catalog page reloads to show the new product in the published-products list. From this moment forward, the storefront's customer-facing listing — backed by the `ProductCatalogView` — includes the Cosmic Critter Plush; any customer browsing the catalog (slice 1.2) will see it.

The `ProductPublished` event recorded here is not state-reconstruction material. Catalog is a document store, not an event-sourced aggregate; the `Product` document is the source of truth. The lifecycle moment is captured as a durable, append-only audit fact — *this product became visible on this date, with these initial attributes* — so that future questions about the catalog's history can be answered without scraping change logs out of the document store. This is the "even CRUD wants events for audit" framing the workshop calls out in Catalog's BC summary.

## Moment 2 — Catching a duplicate SKU

**Context.** Months after Moment 1, the Seller is preparing to relist a product they originally published as `crit-001`. They want a fresh start: new photos, new description, perhaps a re-pricing for a new season. In the meantime they have forgotten that the original `crit-001` document still exists in the Catalog. They open the *Publish a product* form again, intending to begin from scratch.

**Interaction.** The Seller enters the SKU `crit-001`, fills in the updated details, and clicks *Publish*.

**System response.** The Catalog service receives the `PublishProduct` command, detects that a product with SKU `crit-001` already exists, and rejects the command with `ProductAlreadyPublished`. No new `Product` document is created; no `ProductPublished` lifecycle moment is appended; the existing `crit-001` document is untouched. The back-office page surfaces a clear rejection: the SKU is already in use, the existing product is named here, and the Seller is offered two paths forward — choose a different SKU for a genuinely new product, or edit the existing one through its own page (slice 1.3 and beyond).

The failure is idempotent by design. A retry, whether intentional or the consequence of an absent-minded second submit, never produces a second document for the same SKU. The audit trail remains clean: one `ProductPublished` event per SKU, with no shadow events from failed attempts.

## Moment 3 — Adjusting a product's price

**Context.** Months after publishing, the Seller decides to run a seasonal sale on the *Cosmic Critter Plush* (`crit-001`), dropping it from `$24.99` to `$19.99`. The product has been live on the storefront at `$24.99` since Moment 1, and its audit stream holds a single `ProductPublished` moment.

**Interaction.** The Seller opens the product's pricing surface, enters the new price `19.99`, and confirms.

**System response.** The Catalog service receives `ChangeProductPrice { sku: "crit-001", newPrice: 19.99 }`, loads the existing `crit-001` `Product`, updates its price to `19.99`, and appends a `ProductPriceChanged` lifecycle moment to `crit-001`'s audit stream carrying both the **old price (`24.99`)** and the **new price (`19.99`)**. The `Product` document's current price is now `19.99` — it remains the source of truth — and the storefront listing (`ProductCatalogView`, slice 1.2) immediately shows `19.99` to any browsing customer. The audit stream now holds two moments for `crit-001`: `ProductPublished` (the original `24.99` listing) and `ProductPriceChanged` (`24.99` → `19.99`). That is a durable, append-only history of how the price evolved — answerable later without reconstructing it from document change logs. This is the same "even CRUD wants events for audit" framing as Moment 1, now exercised by a stream that genuinely grows over a product's life.

A customer who already had the Cosmic Critter Plush in their cart when the price changed still sees the snapshot price taken at add-to-cart time; the change does **not** ripple to live carts. That deliberate non-event is described below.

## Forthcoming Moments

With slices 1.1 and 1.3 both authored, the Seller's round-one catalog-management journey is complete. Longer-road extensions would thread further Moments when their slices are authored:

- **Product discontinuation.** A `ProductDiscontinued` lifecycle moment (parked in Workshop 001 § 4 / § 9) would append a third kind of event to a product's stream and remove it from the customer-facing listing. Out of scope for round one.

When such a Moment lands, the narrative's `slices` frontmatter grows, the version bumps, and `## Document History` records the amendment.

## What the Seller does *not* yet see

Two non-events are worth naming here because their absence is a deliberate round-one choice, not an oversight:

- **No cross-BC notification on publish.** When the Seller publishes a product, no event flows from Catalog to Orders or Inventory. The Catalog BC has no outbound BC-level integration in round one (per `docs/context-map/README.md`). Product information reaches a Cart only when a customer adds the item; the frontend snapshots the relevant product fields into the `AddToCart` command at that moment, and the Cart's snapshot is authoritative through checkout.
- **No price-change ripple to live carts.** If the Seller later changes a price (slice 1.3) while a customer has the product in their cart, the customer's cart still reflects the snapshot price taken at add-to-cart time. This is by design — see Workshop 001 § 8 open question on Catalog → Orders price-change notifications, deferred to a future round.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                                          |
| ------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-27 | Initial commit. Covers Workshop 001 slice 1.1 (Publish a product) as two Moments: happy path (publish a first product) and duplicate-SKU failure (`ProductAlreadyPublished`). Slice 1.3 (`ChangeProductPrice`) noted as the next planned extension; not yet authored. |
| v1.1    | 2026-05-28 | Threads Workshop 001 slice 1.3 (Change a product's price). Adds Moment 3 (Adjusting a product's price — `crit-001` `24.99` → `19.99`, `ProductPriceChanged` recording old + new price as the second moment on the audit stream). `slices` → `[1.1, 1.3]`. Authored in the consolidated one-PR slice mode (see the slice 1.3 retrospective). |
