# docs/narratives/

NDD-informed journey specs, one per actor journey. See [CLAUDE.md § 4b](../../CLAUDE.md) for the routing-layer treatment of where narratives sit in the pipeline.

A narrative is a journey-scoped spec, written from one actor's perspective, threading multiple workshop slices into one coherent experience. Narratives are NDD-informed (Narrative-Driven Development, Sam Hatoum / Xolvio — principles, not the commercial tool). The narrative is the human-readable companion to the per-slice OpenSpec proposal at `openspec/changes/{change}/proposal.md` (with its SHALL delta at `openspec/changes/{change}/specs/{capability}/spec.md`; ADR 011); both must agree, but they address different audiences (narrative for humans, proposal for machines).

## File naming

`NNN-{actor}-{journey}.md` where `NNN` is the narrative's number (zero-padded), `{actor}` is the actor's role (e.g., `customer`, `seller`, `inventory-operator`), and `{journey}` is a short journey identifier (e.g., `place-order`, `manage-stock`).

## Frontmatter

Each narrative carries YAML frontmatter with:

- `status` — `draft`, `active`, or `superseded`.
- `version` — semver-shaped (start at `v1.0`).
- `slices` — list of workshop slice numbers the narrative threads through (e.g., `[4.1, 4.2, 4.3]`).

## Body structure

A sequence of **Moments**, each containing:

- **Context** — what state the actor and system are in at this moment.
- **Interaction** — what the actor does (input, decision, navigation).
- **System response** — what the system does and what the actor sees.

Moments compose into a complete journey; each Moment maps to a workshop slice or a UI step within a slice.

## Versioning

Each session that touches a narrative bumps its version and appends to `## Document History`. Per CLAUDE.md § 4b, narratives are durable, prose-shaped specs that prompts reference; they persist across many implementation sessions, and the version trail is the audit history of how the journey's specification has evolved.

## Sibling artifact

For each workshop slice the narrative threads, a sibling **OpenSpec proposal** at `openspec/changes/{change}/` carries the precise, machine-readable SHALL statements for the same slice. The two artifacts share a source (the workshop slice) and a scope (one vertical slice) but address two audiences — and **must agree**. If the narrative and proposal contradict, the next session that touches either pairs an update to the other.

## Cross-references

- [CLAUDE.md § 4b](../../CLAUDE.md) — narrative routing-layer treatment.
- [`openspec/changes/`](../../openspec/changes/) — sibling OpenSpec proposal layer; per-slice machine-readable SHALL specs (the openspec workspace, a peer to `docs/`; ADR 011).
- [`../workshops/`](../workshops/) — the source slices a narrative threads.
- [`../prompts/narratives/`](../prompts/narratives/) *(forthcoming)* and [`../retrospectives/narratives/`](../retrospectives/narratives/) *(forthcoming)* — per-narrative authoring session records.

## Current population

**Nine narratives.**

- [`001-seller-manage-catalog.md`](001-seller-manage-catalog.md) — the Seller's catalog-management journey (v1.1, Catalog BC), covering slices 1.1 (Publish a product) and 1.3 (Change a product's price).
- [`002-customer-browse-catalog.md`](002-customer-browse-catalog.md) — the Customer's catalog-browsing journey (Catalog BC), covering slice 1.2 (Browse and view products), a read-only query slice. First Customer-actor narrative.
- [`003-operator-manage-stock.md`](003-operator-manage-stock.md) — the Operator's stock-management journey (v1.1, Inventory BC), covering slices 2.1 (Receive stock) and 2.2 (Reserve stock). First event-sourced-BC narrative.
- [`004-customer-purchase.md`](004-customer-purchase.md) — the Customer's purchasing journey (v1.8, Orders + Inventory BCs), covering slices 3.1 (Add item to cart), 3.2/3.3 (cart edits — remove item, change quantity), 3.4 (cart abandonment on inactivity), 4.1 (Place order), 4.2/4.5 (stock reservation + cancel on stock failure), 4.3/4.4 (payment authorization + confirmation), 4.6 (cancel on payment decline with cross-BC stock release), 4.7 (cancel on payment timeout — the order lifecycle's completing slice), and 2.4 (commit reserved stock on confirmation) across Moments 1–6 plus the journey-ordered inserts Moments 1A and 1B. **The journey is fully authored — every modeled slice is covered.**
- [`005-customer-storefront.md`](005-customer-storefront.md) — the Customer's storefront journey (v1.1, Orders + Catalog), the **frontend-mode entry** (ADR 016). The *screen lens* companion to 002 (browse) and 004 (purchase): where those carry the system behavior, 005 carries what is on screen, threading browse → cart → checkout → track across five Moments tied to the workshop's § 5.1 wireframes W1–W4. Covers slices 1.2, 3.1, 3.2/3.3, **3.5 (View my open cart — net-new view slice, the cold-load read that closes the audit's blocking Gap #1)**, and 4.1. **Partly built** — slice 3.5's keystone read (`GET /carts/mine`) shipped (PR #50; OpenSpec change archived); the W2 cart-review screen that consumes it, the `client/` Vite app, and the `docs/skills/frontend/` convergence are the frontend-bootstrap session.

- [`006-customer-registration.md`](006-customer-registration.md) — the Customer's registration journey (v1.0, **Identity BC** — the first narrative for CritterMart's one non-event-sourced context), covering slices 5.1 (Register a customer — happy path + duplicate-email rejection, case-insensitive) and 5.2 (Resolve a customer — the Open-Host Service read + 404). Carries the teaching contrast: the `Customer` row *is* the read model, and `CustomerRegistered` is an EF-Core **outbox notification**, not a stream event. Sibling of the `customer-registry` OpenSpec change.

- [`007-customer-data-in-orders.md`](007-customer-data-in-orders.md) — the customer-data-flows-into-orders journey (v1.0, Identity→Orders Published Language), covering slices 5.4 (Consume `CustomerRegistered` → upsert consumer-local `LocalCustomerView` in Orders) and 5.3 (Resolve `X-Customer-Id` against the local model at read time → `customerName?` on order responses). Carries the PL graduation: `CustomerRegistered` moves from Identity-internal to `CritterMart.Contracts`, and the no-sync-HTTP rule plays out in the upsert handler. Sibling of the `customer-data` OpenSpec change.

- [`008-operator-replenish-backorders.md`](008-operator-replenish-backorders.md) — the Operator's backorder-replenishment journey (v1.0, Inventory BC), covering slices 2.5 (open a replenishment on a `ReserveStock` shortfall), 2.6 (resolve on a covering or partial restock), and 2.7 (escalate on timeout). Threads CritterMart's **first convention `Wolverine.Saga`** — a SKU-keyed `Replenishment` saga whose transient state lives in saga storage and is deleted on completion — and draws the saga-vs-PMvH (ADR 007) contrast (saga storage vs. state on the Order stream, the "third way to wait"). Picks up the reorder/supplier thread Narrative 003 explicitly deferred. Sibling of the `slices-2-5-2-7-replenishment-saga` OpenSpec change.

- [`009-customer-changes-email.md`](009-customer-changes-email.md) — the Customer's email-change journey (v1.0, Identity BC), covering slices 5.5 (request — with unknown-customer and duplicate-email guards), 5.6 (confirm — with the stays-open-on-conflict rule), and 5.7 (timeout — drop, no escalation). Threads CritterMart's **second convention `Wolverine.Saga`** — an EF-Core-backed `EmailChange` saga, the direct counterpart to Narrative 008's Marten-backed `Replenishment`, proving the identical saga programming model rides on either store (ADR 022). Sibling of the `slices-5-5-5-7-email-change-saga` OpenSpec change.

**Two lenses on the Customer journey.** Narratives 002 + 004 are the *behavior* lens (streams, events, cross-BC hops); 005 is the *screen* lens (wireframes, what the actor sees and touches). They share slices and must agree, but answer different questions — useful precedent if a future actor's journey also splits behavior from UI. (Narrative 006 is a separate Customer journey — registration into the Identity registry — not another lens on the purchase journey.)
