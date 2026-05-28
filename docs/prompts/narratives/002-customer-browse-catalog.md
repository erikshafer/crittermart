# Prompt: Narrative 002 — The Customer Browses the Catalog

**Kind**: pre-code design (narrative)
**Files touched**: `docs/prompts/narratives/002-customer-browse-catalog.md` (new, this file); `docs/narratives/002-customer-browse-catalog.md` (new); `docs/narratives/README.md` (population line); `docs/retrospectives/narratives/002-customer-browse-catalog.md` (forthcoming, authored at session close)
**Mode**: solo authoring — single session writes CritterMart's second narrative (first Customer-actor narrative) against Workshop 001 slice 1.2
**Commit subject**: `docs: add customer-browse narrative covering slice 1.2`

## Framing

This is the **first edge of the slice 1.2 triangle** (narrative → OpenSpec proposal → implementation prompt) and CritterMart's **second narrative** — the first written from the **Customer's** perspective. Narrative 001 (the Seller's catalog-management journey) deliberately excluded slice 1.2, naming it "the subject of a separate, future narrative" because browsing belongs to a different actor. This session authors that narrative.

Slice 1.2 (Browse and view products) is a **read-only query slice** — Workshop 001 § 5 marks it `*(query)*` with no command and no events, and § 6.1 gives it a single happy-path GWT and **no failure path**. The journey it threads is the front half of the Customer's shopping experience: discovering what the storefront sells, before any cart or order exists.

This session is **prompt-first** (the prompt is authored before the narrative), correcting the mid-session prompt authoring that Narrative 001's retro flagged as a known "frozen at session start" strain. The session also carries forward three explicit next-session inputs the Narrative 001 retro left for the Customer narrative:

1. **Trial the "What the [actor] does not yet see" non-events section** (candidate format extension). For the Customer: no live stock visibility, no recommendations/cross-sell, no real-time price updates.
2. **Decide the actor-naming bridge.** Workshop 001's GWT term is *customer* (lowercase); the prose term is *Customer*. These coincide, so — unlike Narrative 001's *Seller*/*operator* bridge — **no bridge is needed**. Record this as the second data point on the bridge pattern.
3. **Quote-identical example data.** Reuse `crit-001` "Cosmic Critter Plush" `24.99` from Workshop 001 § 6.1 and add `crit-002` (the workshop's two-product browse scenario) with a name/price chosen here and carried verbatim into the forthcoming proposal and tests.

## Goal

Produce `docs/narratives/002-customer-browse-catalog.md` (v1.0) covering the Customer's catalog-browsing journey, scoped to Workshop 001 slice 1.2. Follow the format in `docs/narratives/README.md`: YAML frontmatter (`status`, `version`, `slices`), a sequence of **Moments** (Context / Interaction / System response), and a `Document History` table. The journey is framed wide enough to note its continuation into the Orders BC (add-to-cart, place-order) without authoring those Moments.

The voice is the Customer's — a shopper visiting the single-seller critter storefront, whose identity is stubbed (a hardcoded customer ID flows through as if from a real identity system, per ADR 009 / the context map's Conformist relationship).

## Spec delta

`docs/narratives/002-customer-browse-catalog.md` is created at v1.0, threading slice 1.2. `docs/narratives/README.md`'s *Current population* moves from "One narrative" to two, naming the Customer browse narrative. The forthcoming slice 1.2 OpenSpec proposal (a new openspec change adding a **browse** requirement to the existing `product-catalog` capability) gains its human-readable companion spec — the first edge of the slice 1.2 triangle. No workshop, ADR, or code change.

## Orientation

Read these in this order:

1. **`docs/narratives/001-seller-manage-catalog.md`** — the format and voice template; note its "What the Seller does not yet see" section (the pattern this narrative trials from the Customer side) and its journey-scope framing.
2. **`docs/narratives/README.md`** — narrative format conventions. (Note: this README still references the pre-ADR-011 `docs/specs/{slice}/proposal.md` sibling path; that drift is out of scope to fix here — flag in the retro.)
3. **`docs/workshops/001-crittermart-event-model.md`** — § 2 (Catalog BC: document store; `ProductCatalogView`; no BC-level integration), § 5 (slice 1.2 row: `*(query)*`, reads-from `ProductCatalogView`, writes-to —), § 6.1 (the slice 1.2 GWT: two products `crit-001`/`crit-002`, `GET /products` returns both with price + description, no failure path). The narrative must agree; if it contradicts the workshop, stop and raise it.
4. **`docs/context-map/README.md`** — Catalog has *no* BC-level integration in round one; the frontend calls Catalog directly (ADR 006, no BFF); Identity is Conformist/stubbed. These bound what the Customer can and cannot see when browsing (no live stock from Inventory).
5. **`openspec/changes/slice-1-1-publish-product/design.md`** Decision 1 — `ProductCatalogView` is a read **query** over `Product` documents, not a Marten projection. The narrative's system-response prose should reflect "the storefront reads the published catalog," not "a projection rebuilds."

## Working pattern

1. Author this prompt first (done — prompt-first this session).
2. Draft frontmatter (`actor: Customer`, `slices: [1.2]`, `version: v1.0`) and a journey-scope intro: who the Customer is, what round one covers (browse), what continues later (cart/order in Orders BC), what is excluded (catalog management — that is the Seller's Narrative 001).
3. Draft **Moment 1 — browsing the storefront catalog** from Workshop 001 § 6.1: the Seller has published `crit-001` and `crit-002`; the Customer requests the listing; Catalog serves `ProductCatalogView` (a query over the document store) showing both products with name, price, and description. No cross-BC calls; identity flows through but does not gate the public listing.
4. Add a **"What the Customer does not yet see"** section: no live stock availability (Catalog has no Inventory integration in round one), no recommendations/cross-sell, no real-time price push (price is as-of-request; the Cart later snapshots it — the mirror of Narrative 001's price-snapshot non-event).
5. Note **forthcoming Moments**: the journey continues with add-to-cart (slice 3.1) and place-order (slice 4.1) in the Orders BC; whether those extend this narrative or get a separate Customer-purchasing narrative is decided when those slices are authored. Do not author them.
6. Stamp `Document History` v1.0 / today.
7. Update `docs/narratives/README.md`'s *Current population* line.
8. Author the retrospective at session close: spec-delta closure; the no-bridge-needed data point; whether the non-events section earned its place a second time (candidate to lift into the README); whether narrative-first felt earned for a query slice; the `docs/specs/`-path drift in the narratives README as a surfaced tidy item.

## Out of scope

- **No slice 1.2 OpenSpec proposal.** That is the next session in the triangle (a new openspec change adding a browse requirement to the `product-catalog` capability, driven manually per ADR 011).
- **No slice 1.2 implementation prompt or code.** Third edge of the triangle; this is design.
- **No Orders-BC Moments** (add-to-cart, place-order) beyond the forward-looking note.
- **No edits to Workshop 001, Narrative 001, or any ADR.** If a contradiction with the workshop surfaces, stop and raise it.
- **No fix of the `docs/specs/` → `openspec/changes/` path drift** in `docs/narratives/README.md` or elsewhere — that is a broader staleness sweep for a `tidy: docs` session; flag it in the retro, do not fix it here (no opportunistic edits).
- **No new capability** in the eventual proposal — slice 1.2 adds a requirement to the existing `product-catalog` capability (recorded here so the proposal session inherits the decision).
- **No API/UI shape commitments** beyond what Workshop 001 § 5 implies (`GET /products` reading `ProductCatalogView`); the endpoint's exact shape is the implementation prompt's call.
