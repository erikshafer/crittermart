# Prompt: Narrative 004 — The Customer Buys From the Catalog

**Kind**: pre-code design (narrative)
**Files touched**: `docs/prompts/narratives/004-customer-purchase.md` (new, this file); `docs/narratives/004-customer-purchase.md` (new); `docs/narratives/README.md` (population line); `docs/retrospectives/narratives/004-customer-purchase.md` (forthcoming, session close)
**Mode**: solo authoring — collaborative working style (per-fork decisions taken with the user)
**Commit subject**: `docs: add customer-purchase narrative covering slice 3.1`

## Framing

This is the **first edge of the slice 3.1 triangle** and the opening of the **Orders bounded context** — the keystone BC (two event-sourced aggregates; the Order is its own process manager via PMvH, ADR 007). Orders is big and design-heavy, so for slice 3.1 (which also brings up the Orders service skeleton and the first Cart aggregate) we are working **per-edge**: this narrative is its own PR, the OpenSpec proposal its own PR, the implementation its own PR — giving review checkpoints at each design stage. (The PR-granularity policy is "decide per slice"; mechanical follow-ons like 3.2/3.3 will bundle.)

Three decisions were taken with the user before this prompt was frozen:

1. **A new narrative (004), not an extension of Narrative 002.** Narrative 002 was the Customer *browsing* the catalog; this is a distinct, longer journey — the Customer *buying*: filling a cart, placing an order, and watching it get fulfilled. Narrative 002 explicitly left the cart/order continuation open to "a separate Customer-purchasing narrative."
2. **Slice 3.1 scope is cart-create + add-item only.** The cart-inactivity timeout (Bruun temporal automation) is **deferred to slice 3.4** — no scheduled self-message is set up until its abandonment handler exists. So Moment 1 here does not mention scheduling a timeout.
3. **Per-edge for slice 3.1** (above).

The actor is the same **Customer** as Narrative 002, now past browsing and deciding to buy. Their identity is stubbed (ADR 009).

## Goal

Produce `docs/narratives/004-customer-purchase.md` (v1.0) covering the Customer's purchasing journey, scoped in this version to **Workshop 001 slice 3.1 (Add item to cart)** as Moment 1. The journey is framed wide enough to grow — cart edits (3.2/3.3), placing the order (4.1), fulfillment (reserve/payment/confirm-or-cancel, 4.2–4.7), and cart abandonment (3.4) — without restructuring, but v1.0 authors only the Moment slice 3.1 needs.

Follow `docs/narratives/README.md`: frontmatter (`status`, `version`, `slices`), Moments (Context / Interaction / System response), `Document History`. Mirror the shape of Narratives 001–003.

## Spec delta

`docs/narratives/004-customer-purchase.md` is created at v1.0, threading slice 3.1. `docs/narratives/README.md`'s *Current population* line is brought current (it lags — it should list narratives 001 Seller, 002 Customer-browse, 003 Operator-stock, and now 004 Customer-purchase; the 003 entry was missed when Narrative 003 landed inside a consolidated implementation PR). The forthcoming slice 3.1 OpenSpec proposal (a new Cart capability in the Orders BC) gains its human-readable sibling.

## Orientation

Read in this order:

1. **`docs/narratives/002-customer-browse-catalog.md`** — the same Customer actor; its forthcoming-Moments note hands off to this narrative. Continue from where browsing left the Customer (a product in view, deciding to buy). Reuse the anchor product (`crit-001` "Cosmic Critter Plush" `24.99`).
2. **`docs/workshops/001-crittermart-event-model.md`** § 2 (Orders BC — Cart aggregate lifecycle, the frontend snapshots product fields into Cart commands at add-to-cart time), § 4 (Cart vocabulary: `CartCreated`, `CartItemAdded`), § 5 (slice 3.1 row), § 6.1 (the 3.1 GWT scenarios — first item creates the Cart stream; a second item appends).
3. **`docs/narratives/001-seller-manage-catalog.md`** — the price-snapshot non-event: the Cart snapshots price at add-to-cart time, so a later catalog price change does not affect the cart. Mirror that from the Customer's side.
4. **`docs/context-map/README.md`** — Catalog has no BC-level integration with Orders; product info reaches the Cart *only* via the frontend snapshotting it into `AddToCart`. This is presentation-layer composition, not a BC integration.

## Working pattern

1. Author this prompt (done).
2. Draft frontmatter (`actor: Customer`, `slices: [3.1]`, `version: v1.0`) + a journey-scope intro: who the Customer is here (a buyer, continuing from browsing), what round one covers (3.1 now; 3.2/3.3/4.x forthcoming), what is excluded (catalog management — the Seller's narrative).
3. Draft **Moment 1 — adding the first item to the cart** from Workshop 001 § 6.1: the Customer adds `crit-001` (qty 1); the frontend **snapshots** the product's fields (name, price) into `AddToCart`; the system creates a new Cart stream (`CartCreated` for the customer) and appends `CartItemAdded` with the snapshot; `CartView` shows the line. Note the second-item behavior (appends `CartItemAdded` to the existing cart) briefly. **No timeout scheduling** (deferred to 3.4).
4. Add a brief **"What the Customer does not yet see"** section: the cart's snapshot price is authoritative (later catalog price changes don't ripple — mirror of Narratives 001/002); no checkout/payment yet (4.x); no abandonment yet (3.4).
5. Note **Forthcoming Moments**: cart edits (3.2/3.3), place order (4.1), fulfillment + confirmation/cancellation (4.2–4.7), abandonment (3.4). Do not author them.
6. Stamp `Document History` v1.0 / today.
7. Bring `docs/narratives/README.md`'s *Current population* line current (list all four narratives).
8. Author the retrospective at session close (spec-delta closure; note the per-edge choice for the Orders kickoff; note the README population catch-up).

## Out of scope

- **No OpenSpec proposal** (the next edge — a new Cart capability in the Orders BC).
- **No implementation / no Orders service skeleton** (the third edge).
- **No Bruun timeout scheduling** in Moment 1 (deferred to 3.4 per the scope decision).
- **No Moments for 3.2/3.3/4.x** beyond the forward-looking note.
- **No edits to Workshop 001 or other narratives.** If a contradiction with the workshop surfaces, stop and raise it.
- **No `tidy: docs` beyond the narratives README population line** (the broader README/`docs/specs`-drift debt stays a separate sweep).
