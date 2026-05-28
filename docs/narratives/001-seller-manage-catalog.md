---
narrative: 001
title: The Seller Manages the Catalog
actor: Seller
status: draft
version: v1.0
slices: [1.1]
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
- **Slice 1.3 — Change a product's price.** Forthcoming. When that slice's OpenSpec proposal is drafted, this narrative is updated to thread its Moment(s) and the version bumps to `v1.1`.

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

## Forthcoming Moments

The Seller's catalog-management journey will gain at least one more Moment when slice 1.3 is authored:

- **Moment 3 — Adjusting a product's price.** The Seller decides to drop the Cosmic Critter Plush from `$24.99` to `$19.99` for a sale. The `ChangeProductPrice` command appends a `ProductPriceChanged` lifecycle moment carrying the old and new prices, and the `ProductCatalogView` updates to reflect the new price on the storefront. The audit trail preserves the original price for posterity; the document's current price is the new value.

When that Moment lands, the narrative's `slices` frontmatter becomes `[1.1, 1.3]`, the version bumps to `v1.1`, and `## Document History` records the amendment.

## What the Seller does *not* yet see

Two non-events are worth naming here because their absence is a deliberate round-one choice, not an oversight:

- **No cross-BC notification on publish.** When the Seller publishes a product, no event flows from Catalog to Orders or Inventory. The Catalog BC has no outbound BC-level integration in round one (per `docs/context-map/README.md`). Product information reaches a Cart only when a customer adds the item; the frontend snapshots the relevant product fields into the `AddToCart` command at that moment, and the Cart's snapshot is authoritative through checkout.
- **No price-change ripple to live carts.** If the Seller later changes a price (slice 1.3) while a customer has the product in their cart, the customer's cart still reflects the snapshot price taken at add-to-cart time. This is by design — see Workshop 001 § 8 open question on Catalog → Orders price-change notifications, deferred to a future round.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                                          |
| ------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-27 | Initial commit. Covers Workshop 001 slice 1.1 (Publish a product) as two Moments: happy path (publish a first product) and duplicate-SKU failure (`ProductAlreadyPublished`). Slice 1.3 (`ChangeProductPrice`) noted as the next planned extension; not yet authored. |
