# Prompt: Specs 002 — Slice 1.2 Browse Products OpenSpec Proposal

**Kind**: pre-code design (OpenSpec proposal)
**Files touched**: `docs/prompts/specs/002-slice-1-2-browse-products.md` (new, this file); `openspec/changes/slice-1-2-browse-products/{.openspec.yaml, proposal.md, specs/product-catalog/spec.md}` (new); `docs/retrospectives/specs/002-slice-1-2-browse-products.md` (forthcoming, authored at session close)
**Mode**: solo authoring with tool-backed scaffolding (openspec CLI 1.3.1)
**Commit subject**: `docs: add slice 1.2 browse-products OpenSpec proposal`

## Framing

This is the **second edge of the slice 1.2 triangle** (narrative → **OpenSpec proposal** → implementation prompt). Narrative 002 (the Customer's catalog-browsing journey) landed in PR #10; this session authors its machine-readable sibling.

Slice 1.2 (Browse and view products) is a **read-only query slice** — Workshop 001 § 5 marks it `*(query)*` with no command and no events; § 6.1 gives it a single happy-path GWT and **no failure path**. The proposal therefore adds a **browse requirement to the existing `product-catalog` capability** — it does **not** introduce a new capability. Slice 1.1 established the one-capability-per-bounded-context convention (`product-catalog` accumulates requirements as slices land); slice 1.2 is the first test of that accumulation.

Per **ADR 011**, drive the openspec CLI **manually** (`openspec new change` + `openspec instructions`), not `/opsx:propose`, and author **only** `proposal.md` + the `specs/product-catalog/spec.md` delta this session. `design.md` and `tasks.md` are deferred to the slice 1.2 implementation session (one-artifact-class-per-session).

## Goal

Produce a valid openspec change at `openspec/changes/slice-1-2-browse-products/` containing:

- `proposal.md` — Why / What Changes / Capabilities (Modified: `product-catalog`) / Impact, focused on WHY not HOW.
- `specs/product-catalog/spec.md` — an `## ADDED Requirements` delta with the browse requirement and a `#### Scenario:` block translating Workshop 001 § 6.1's slice 1.2 GWT (two products published; `GET /products` returns both with name, description, and current price; no failure path).

The change must pass `openspec validate slice-1-2-browse-products --strict`.

## Spec delta

The `slice-1-2-browse-products` openspec change is created. The `product-catalog` capability gains a **browse** requirement (its second requirement, after slice 1.1's publish + SKU-uniqueness). Narrative 002 gains its machine-readable sibling; the two must agree. This is the contract the slice 1.2 implementation prompt and the `GET /products` endpoint will satisfy.

## Orientation

Read these in this order:

1. **`docs/narratives/002-customer-browse-catalog.md`** — the sibling artifact the proposal must agree with. Moment 1 (browse) maps to the spec scenario; the non-events section maps to the proposal's Impact/scope.
2. **`docs/workshops/001-crittermart-event-model.md`** § 5 (slice 1.2 row: `*(query)*`, reads-from `ProductCatalogView`, writes-to —) and § 6.1 (the slice 1.2 GWT — the authoritative scenario; no failure path).
3. **`openspec/changes/slice-1-1-publish-product/`** — `proposal.md` + `specs/product-catalog/spec.md` as the format model and the source of the one-`product-catalog`-capability decision. The 1.2 delta ADDs to the same capability.
4. **`openspec/changes/slice-1-1-publish-product/design.md`** Decision 1 — `ProductCatalogView` is a query over `Product` documents, not a Marten projection. The proposal's prose should reflect "exposes the published products through `ProductCatalogView`," consistent with that.
5. **openspec CLI instructions** — `openspec instructions proposal --change slice-1-2-browse-products` and `... specs ...` supply the template and validation contract; follow them.

## Working pattern

1. `openspec new change slice-1-2-browse-products` to scaffold the change directory + `.openspec.yaml`.
2. Author `proposal.md` per `openspec instructions proposal`. Capabilities → **Modified Capabilities: `product-catalog`** (adding the browse requirement); no new capability.
3. Author `specs/product-catalog/spec.md` per `openspec instructions specs`: `## ADDED Requirements` with one **Browse** requirement and one `#### Scenario:` (4 hashtags) lifted from Workshop 001 § 6.1. Quote-identical anchor data: `crit-001` "Cosmic Critter Plush" `24.99` and `crit-002` "Nebula Newt" `18.00` (the second product introduced in Narrative 002). No failure scenario — it is a query slice.
4. `openspec validate slice-1-2-browse-products --strict` — must pass.
5. Author the retrospective at session close: spec-delta closure; the capability-granularity check (does ADDing a browse requirement to `product-catalog` hold cleanly, validating the slice 1.1 decision?); confirm `archive slice-1-1` remains a separate deferred step.

## Out of scope

- **No `design.md` or `tasks.md`** — they belong to the slice 1.2 implementation session.
- **No `/opsx:propose` or `/opsx:apply`** — drive openspec manually per ADR 011.
- **No `openspec archive slice-1-1-publish-product`** — archiving is a separate deliberate step (still deferred); this session authors the 1.2 proposal against the change-folder deltas, not the archived main spec.
- **No new capability** — slice 1.2 ADDs to the existing `product-catalog` capability.
- **No `src/` code** — the `GET /products` endpoint is the implementation session's deliverable.
- **No slice 1.2 implementation prompt** — that is the third triangle edge.
- **No edits to Workshop 001 or Narrative 002.** If a contradiction surfaces, stop and raise it — the workshop is the source of truth.
- **No fix of the `docs/specs/` path drift** in `docs/narratives/README.md` (surfaced in the Narrative 002 retro) — that is a `tidy: docs` concern, not this session's.
