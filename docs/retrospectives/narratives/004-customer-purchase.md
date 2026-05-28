---
retrospective: 004
kind: narratives
prompt: docs/prompts/narratives/004-customer-purchase.md
deliverable: docs/narratives/004-customer-purchase.md (new); docs/prompts/narratives/004-customer-purchase.md (new); docs/narratives/README.md (population line); docs/retrospectives/narratives/004-customer-purchase.md (this file)
date: 2026-05-28
mode: solo authoring; collaborative working style (forks decided with the user)
session-runner: Claude (Opus 4.7)
---

# Retrospective — Narratives 004: The Customer Buys From the Catalog

## Outcome summary

Authored `docs/narratives/004-customer-purchase.md` (v1.0) — the **first edge of the slice 3.1 triangle** and the opening of the **Orders bounded context**. It's the Customer's purchasing journey (continuing from Narrative 002's browsing), with **Moment 1** covering slice 3.1 (Add item to cart): the frontend snapshots product fields into `AddToCart`, which creates the Cart stream (`CartCreated` + `CartItemAdded`) and surfaces it through `CartView`. Cart edits (3.2/3.3), place-order (4.1), fulfillment (4.2–4.7), and abandonment (3.4) are forward-looked. Also brought `docs/narratives/README.md`'s population section current — it had drifted to "Two narratives" and was missing Narrative 003 (authored inside a consolidated implementation PR); it now lists all four.

This is the **first session back in collaborative mode** — the three shaping decisions were taken *with the user* before drafting.

## What worked

- **Deciding the forks with the user up front.** New-narrative-vs-extend-002, per-edge-vs-bundled, and Bruun-in-3.1-vs-defer were all settled before a word of narrative was drafted — so the scope and shape were certain, not guessed. This is the collaborative default reasserting itself after the demo-era autonomy, and it made the draft a clean, single pass.
- **New narrative (004), not an extension of 002.** Keeps the Customer's two journeys focused — *browsing* (002) and *buying* (004) — rather than one sprawling narrative spanning Catalog and Orders. Narrative 002 had explicitly left this open.
- **The snapshot-at-add-time principle threaded cleanly** — the Customer-side mirror of Narrative 001's price-snapshot non-event. The "cart snapshot wins until checkout" idea is now consistent across three narratives.
- **Deferring Bruun to 3.4 kept Moment 1 honest** — no scheduled self-message described without a handler to receive it.

## What was harder than expected

- **The narratives README population section was stale.** It said "Two narratives" while three existed (003 was missed when it landed in a consolidated impl PR), carried outdated forward-looks, and referenced the pre-ADR-011 `docs/specs/1.1/proposal.md` path. I brought the *population list* current (all four, accurate scope) and dropped the stale narrative-first commentary paragraph — but left the broader `docs/specs` path drift elsewhere in the README for the deferred `tidy: docs` sweep (scope discipline). Lesson reinforced: **consolidated one-PR slices skip the per-kind README updates that per-edge PRs do** — that's the source of the population drift.
- **Re-adopting per-edge after the one-PR habit.** Under "decide per slice," slice 3.1 is per-edge (narrative/proposal/impl as three PRs) because it's the BC kickoff (new service skeleton + first aggregate + new capability). A deliberate slowdown traded for review checkpoints at each design stage.

## Methodology refinements that emerged

1. **"Decide per slice" PR-granularity, first applied.** Per-edge for novel/design-heavy slices (3.1), bundled for mechanical ones (3.2/3.3 to come). A pragmatic middle between the rigid triangle and one-PR-per-slice.
2. **Collaborative default restored** ([[feedback-collaborate-on-decisions]]) — surface forks, recommend, let the user choose. The demo-era act-on-leans autonomy is lifted.
3. **The "What the [actor] does not yet see" section is now a de facto convention** (4th narrative running). The `docs/narratives/README.md` format extension flagged after Narrative 002's retro is overdue — fold into a `tidy: docs`.
4. **README population drift is a known cost of consolidated PRs.** When a slice bundles its narrative into one PR, the narratives-README population line tends to get skipped. Either update it in the bundled PR, or sweep periodically.

## Outstanding items / next-session inputs

1. **Slice 3.1 OpenSpec proposal (next edge).** A **new Cart capability** in the Orders BC (e.g., `shopping-cart`) with an `AddToCart` requirement + the Workshop § 6.1 3.1 scenarios (first item creates the Cart stream; a second appends). Anchor data `crit-001`/`crit-002`. Drive openspec manually (ADR 011).
2. **Slice 3.1 implementation (third edge).** The `CritterMart.Orders` service skeleton (3rd event-sourced service — `orders` schema, distinct port `:5103`, ServiceDefaults, Swagger, RuntimeCompilation; add to AppHost + slnx) + the Cart aggregate + `AddToCart` + `CartView` + tests. **First pure-function unit tests** (cart decision logic) can begin here → activates the CI unit job (`CritterMart.Orders.UnitTests`, untagged).
3. **Narrative home recorded:** the Customer's purchasing journey is its own narrative (004), separate from browse (002).
4. **`tidy: docs` debt** still includes the narratives README's `docs/specs`-path references (other sections) + the "does-not-yet-see" format extension.
5. No new ADR or skill triggered.

## Spec-delta — landed?

**Yes.** `docs/narratives/004-customer-purchase.md` created at v1.0 (slice 3.1, Moment 1). `docs/narratives/README.md` population section brought current (four narratives, accurate scopes — fixing the missed 003 along the way). The forthcoming slice 3.1 OpenSpec proposal gains its human-readable sibling.

## Process notes

- Per-edge for the Orders kickoff: this narrative is its own PR. Branch `docs/customer-purchase-narrative`; commit `docs: add customer-purchase narrative covering slice 3.1`.
- The narratives-prompt number (004) matches the narrative number; there is no `narratives/003` prompt/retro because Narrative 003 (Operator) was authored inside the consolidated slice-2.1 implementation PR.
- Collaborative working style (act-on-leans lifted); the three shaping decisions were the user's.
