## Why

Catalog prices are not static — the Seller runs sales and adjusts pricing over a product's life. Workshop 001 slice 1.3 (`ChangeProductPrice`) is the Seller's third catalog operation and completes the round-one Seller journey (Narrative 001 v1.1, Moment 3). It also exercises the per-product audit stream as a genuinely **growing** log: `ProductPriceChanged` becomes the second moment after `ProductPublished`, recording the old and new price for audit while the `Product` document stays the source of truth. Narrative 001 (v1.1) is this proposal's human-readable sibling; the two must agree.

## What Changes

- Introduce the `ChangeProductPrice` command: the Seller changes a published product's listed price, identified by SKU.
- Update the `Product` document's current price (the source of truth) and append a `ProductPriceChanged` lifecycle moment to the product's audit stream carrying the **old** and **new** price.
- The catalog listing (`ProductCatalogView`, slice 1.2) reflects the new price with no additional change — it already returns the document's current price.
- No cross-bounded-context integration: a price change fires no message to Orders or Inventory. It does **not** ripple to live carts — the Cart snapshots price at add-to-cart time (a deliberate round-one non-event).

## Capabilities

### New Capabilities

<!-- None. Slice 1.3 extends the existing product-catalog capability. -->

### Modified Capabilities

- `product-catalog`: adds a **change-price** requirement — the third operation, after publish + SKU-uniqueness (1.1) and browse (1.2). This is the third accumulation onto the one-capability-per-bounded-context model; like browse, it ADDs a new operation rather than MODIFYing existing behavior (publish and browse are unaffected).

## Impact

- **HTTP surface:** a Wolverine.Http endpoint accepting `ChangeProductPrice`; no synchronous service-to-service calls.
- **Persistence:** loads and updates the existing `Product` document and appends `ProductPriceChanged` to its SKU-keyed event stream **in one transaction**. No new document type; a second event *kind* now appears on the per-product stream (after `ProductPublished`).
- **Identity:** the acting Seller is stubbed per ADR 009 — flows through the command as the change's author.
- **Out of scope:** no price-change notification to Orders or live carts (round-one non-event, Narrative 001); no scheduled or promotion-bound pricing (parked, Workshop 001 § 9).
- **Downstream artifacts:** `design.md` and `tasks.md` are authored in this same session/PR — slice 1.3 is the first slice shipped under the consolidated one-PR mode (narrative + proposal + implementation together), a deliberate, informally-kept divergence from ADR 011's session split (recorded in the slice retrospective).
