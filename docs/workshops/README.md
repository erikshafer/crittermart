# docs/workshops/

Event Modeling outputs and (when needed) Domain Storytelling captures. See [CLAUDE.md §§ 2–3](../../CLAUDE.md) for the routing-layer treatment of how workshops fit into the design pipeline.

The folder holds two workshop kinds per CLAUDE.md:

- **Event Modeling workshops** — the primary artifact kind for round one. Each workshop produces a timeline of events, commands, views/read models, swim lanes, vertical slices, and GWT scenarios.
- **Domain Storytelling captures** — story-shaped captures of an actor walking a domain scenario. **Explicitly skipped for round one** per CLAUDE.md (CritterMart's single-seller language is unambiguous across all four BCs). The folder is the home for these when a future BC introduces actors with divergent vocabularies.

## File naming

- **Event Modeling:** `NNN-{bc}-event-model.md` where `NNN` is the workshop's session number (zero-padded) and `{bc}` is the bounded-context slug, or `crittermart` for a cross-BC pass. See [`001-crittermart-event-model.md`](001-crittermart-event-model.md) as the in-repo precedent.
- **Domain Storytelling:** convention to be confirmed when the first capture lands. Anticipated shape: `NNN-{actor}-{scenario}.md`.

## Output discipline (Event Modeling)

Per CLAUDE.md § 3 *Output discipline*:

- Each slice has a number (e.g., 4.1, 4.2) inside the workshop's slice section.
- Each slice has a *reads-from* list (which prior events the slice consumes) and a *writes-to* list (which new events the slice produces).
- Each slice has at least one **GWT happy path** scenario.
- **Failure paths are explicit, not implied.** A slice that handles a failure mode names it as its own GWT scenario or as a sibling slice.

## How a workshop is conducted

The methodology for facilitating, simulating, or planning an Event Modeling workshop lives in [`../skills/event-modeling/SKILL.md`](../skills/event-modeling/SKILL.md). That skill covers the four-phase brain-dump → storytelling → storyboarding → slicing → scenarios flow, the multi-persona facilitation pattern, and the adjunct event-modeling patterns (Klefter translation-decision events, Bruun temporal-automation slices, configuration-as-events) that may appear in a CritterMart workshop. This README is for the *output* shape; the skill is for the *process* that produces it.

## Cross-references

- [CLAUDE.md §§ 2–3](../../CLAUDE.md) — Domain Storytelling and Event Modeling Workshop routing-layer sections.
- [`../skills/event-modeling/SKILL.md`](../skills/event-modeling/SKILL.md) — *how* a workshop is conducted.
- [`../prompts/workshops/`](../prompts/workshops/) and [`../retrospectives/workshops/`](../retrospectives/workshops/) — per-workshop session intent and outcome records.
- Workshop slices flow into per-slice OpenSpec proposals at `docs/specs/` *(forthcoming)* and sibling narratives at `docs/narratives/` *(forthcoming)*.

## Current population

One workshop has landed for round one: `001-crittermart-event-model.md` (cross-BC event model covering all four BCs).
