# docs/retrospectives/

Per-session outcome records, authored before the session's PR opens. See [CLAUDE.md § 6](../../CLAUDE.md) for the routing-layer treatment of retrospectives in the design pipeline.

Each retrospective pairs with a prompt at `docs/prompts/{kind}/NNN-{slug}.md`. The prompt records *intent at session start*; the retro records *what actually shipped, what was harder than expected, what the methodology refined to, and whether the spec delta closed*. The retro is the feedback edge — it surfaces methodology refinements before they evaporate and explicitly closes (or honestly fails to close) the spec delta the prompt named.

## Subdirectory structure

Mirrors [`docs/prompts/`](../prompts/). Each kind subdir of prompts has a matching kind subdir under retrospectives. New kinds appear in retrospectives when they appear in prompts.

## File naming

Same as the paired prompt: `NNN-{slug}.md`. The number and slug match the prompt's filename so the pairing is one-glance-obvious.

## Frontmatter

Each retrospective carries YAML frontmatter with:

- `retrospective` — the retrospective number (matches the paired prompt's number).
- `kind` — the kind subdirectory (e.g., `rules`, `workshops`, `docs`).
- `prompt` — the path to the paired prompt (e.g., `docs/prompts/rules/001-round-one-structural-constraints.md`).
- `deliverable` — the path to the primary artifact (or comma-separated list of artifacts) the session produced.
- `date` — the session's date (ISO 8601).
- `mode` — the working mode (solo synthesis, multi-persona facilitation, etc.).
- `session-runner` — identifier for the agent or contributor who ran the session.

## Required sections

Per CLAUDE.md § 6:

- **Outcome summary** — what the session produced, named concretely.
- **What worked** — choices, disciplines, or passes that paid off.
- **What was harder than expected** — friction points, judgment calls, decisions that took longer than anticipated.
- **Methodology refinements that emerged** — observations about the *process* that should carry forward to future sessions.
- **Outstanding items / next-session inputs** — what surfaced but was honestly deferred; what the next session in this area should know.
- **Spec-delta — landed?** — explicit confirmation (or honest negative) of whether the prompt's named spec delta closed.

## In-repo format precedent

The format observed in [`rules/001-round-one-structural-constraints.md`](rules/001-round-one-structural-constraints.md) is the project convention:

- YAML frontmatter (see above).
- Top-line title `# Retrospective — {Kind} {NNN}: {Title}`.
- Level-2 (`##`) headings for each required section.
- Optional `## Process notes` section at the bottom for one-shot observations that don't fit the standard six.

## Operating discipline

- **Authored before the PR opens.** The retro is not a post-PR write-up; it is part of the session's deliverables and lands in the same PR as the artifact.
- **Spec-delta closure is mandatory.** Every retro names whether the prompt's spec delta landed. A retro that finishes without addressing the spec delta is incomplete.
- **Methodology refinements carry forward.** A future session reading this folder should be able to find the prior session's methodology learnings without rerunning them.

## Cross-references

- [CLAUDE.md § 6](../../CLAUDE.md) — retrospective routing-layer treatment.
- [CLAUDE.md § Operating Disciplines](../../CLAUDE.md) — the spec-delta closure loop and the one-prompt-one-PR rule.
- [`../prompts/`](../prompts/) — the paired layer of session intent records.

## Current population

Kinds populated for round one: `rules/` (2 — round-one structural-constraints synthesis, encode bundle), `workshops/` (1), `research/` (1 — frontend stack landscape; the ecommerce-lessons research prompt has no paired retro), `docs/` (10 — folder-READMEs, README overhaul, housekeeping sweep, slice 3.1/4.1/4.2/4.6/4.7/3.4 and slices 3.2+3.3 doc follow-ups), `narratives/` (3 — Seller catalog-management, Customer browse, Customer purchase), `specs/` (2 — slice 1.1 + 1.2 OpenSpec proposals), `chore/` (2 — Critter Stack 2026 upgrade, infra bundle), `implementations/` (13 — slices 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 4.1, 4.2, 4.3, 4.6, 4.7, 3.2+3.3, 3.4 — the full round-one modeled set). Additional kinds (`skills/`, `decisions/`) appear as their first session of that kind lands.
