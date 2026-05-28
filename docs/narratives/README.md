# docs/narratives/

NDD-informed journey specs, one per actor journey. See [CLAUDE.md § 4b](../../CLAUDE.md) for the routing-layer treatment of where narratives sit in the pipeline.

A narrative is a journey-scoped spec, written from one actor's perspective, threading multiple workshop slices into one coherent experience. Narratives are NDD-informed (Narrative-Driven Development, Sam Hatoum / Xolvio — principles, not the commercial tool). The narrative is the human-readable companion to the per-slice OpenSpec proposal at `docs/specs/{slice}/proposal.md`; both must agree, but they address different audiences (narrative for humans, proposal for machines).

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

For each workshop slice the narrative threads, a sibling **OpenSpec proposal** at `docs/specs/{slice}/proposal.md` *(forthcoming)* carries the precise, machine-readable SHALL statements for the same slice. The two artifacts share a source (the workshop slice) and a scope (one vertical slice) but address two audiences — and **must agree**. If the narrative and proposal contradict, the next session that touches either pairs an update to the other.

## Cross-references

- [CLAUDE.md § 4b](../../CLAUDE.md) — narrative routing-layer treatment.
- [`../specs/`](../specs/) *(forthcoming)* — sibling OpenSpec proposal layer; per-slice machine-readable SHALL specs.
- [`../workshops/`](../workshops/) — the source slices a narrative threads.
- [`../prompts/narratives/`](../prompts/narratives/) *(forthcoming)* and [`../retrospectives/narratives/`](../retrospectives/narratives/) *(forthcoming)* — per-narrative authoring session records.

## Current population

**One narrative.** [`001-seller-manage-catalog.md`](001-seller-manage-catalog.md) — the Seller's catalog-management journey, covering Workshop 001 slice 1.1 (Publish a product). The narrative is scoped wider than slice 1.1; slice 1.3 (Change a product's price) is forward-looked and will extend the narrative when its OpenSpec proposal is authored. Customer-facing slice 1.2 (Browse and view products) is excluded; it will live in a separate Customer narrative.

This first narrative was authored before its sibling OpenSpec proposal at `docs/specs/1.1/proposal.md` (forthcoming). The narrative-first ordering for this triangle is a per-session choice, not yet a pipeline convention; see the paired retrospective at `docs/retrospectives/narratives/001-seller-manage-catalog.md` (forthcoming) for the rationale and whether the choice earns its keep across subsequent triangles.
