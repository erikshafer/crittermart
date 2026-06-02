# docs/prompts/

Per-session intent records, frozen at session start. See [CLAUDE.md § 5](../../CLAUDE.md) for the routing-layer treatment of prompts in the design pipeline.

A prompt is a task-scoped build order for **one session**. Prompts are the durable, version-controlled record of *intent at session start*. They are not living documents — once the session runs, the prompt is frozen as a historical record; corrections happen in the *next* prompt, not via editing the frozen one. The matching retrospective at `docs/retrospectives/{kind}/NNN-{slug}.md` records *what actually shipped*.

## Subdirectory structure

One subdirectory per kind. Per CLAUDE.md § 5, kinds include `workshops`, `narratives`, `skills`, `decisions`, and `implementations`. The list is descriptive, not prescriptive — new kinds appear as the work surfaces them. The in-repo set already extends the CLAUDE.md list with `rules/` (round-one structural-constraints synthesis) and `docs/` (folder-local README maintenance).

## File naming

`NNN-{slug}.md` where `NNN` is the prompt's number within its kind (zero-padded) and `{slug}` is a short kebab-case identifier (e.g., `001-round-one-structural-constraints.md`).

## Required sections

Per CLAUDE.md § 5, every prompt carries:

- **Metadata block** — bolded inline lines naming the kind, files touched, mode (solo synthesis, multi-persona facilitation, etc.), and commit subject.
- **Framing** — context for *why this prompt exists now*, in 1–3 paragraphs.
- **Goal** — what the session must produce, in concrete terms.
- **Spec delta** — what the canonical spec (the narrative or workshop the session satisfies) will gain when this session ships. Spec-shaped terms, not process-shaped.
- **Orientation files** — the files the session-runner must read first, in order, with one-line notes on what each is consulted for.
- **Working pattern** — the passes or steps the session executes.
- **Deliverable plan** — the files the session writes, in order. (Captured in-repo as `Files touched` in the metadata block plus the implicit list in the spec delta and goal.)
- **Out of scope** — what the session explicitly will *not* touch.

## In-repo format precedent

The format observed in [`rules/001-round-one-structural-constraints.md`](rules/001-round-one-structural-constraints.md) is the project convention:

- Top-line title `# Prompt: {Kind} {NNN} — {Title}`.
- Bolded inline metadata (`**Kind**: ...`, `**Files touched**: ...`, `**Mode**: ...`, `**Commit subject**: ...`) immediately under the title.
- Level-2 (`##`) headings for each required section.
- Sustained prose where reasoning is multi-step; bullet lists where content is genuinely list-shaped.

New prompts should mirror this shape unless a session has a deliberate reason to diverge.

## Operating discipline

- **One prompt = one session = one PR.** A prompt corresponds to one working session; that session produces one PR; the PR contains exactly the prompt's named deliverables plus the retrospective.
- **Frozen at session start.** The prompt is not edited after the session begins. Corrections become the next prompt.
- **Spec-delta closure.** Every prompt names its spec delta in 2–4 lines. The paired retrospective confirms whether the delta landed.
- **Branch-per-prompt naming.** Each prompt-driven session runs on its own branch named `{type}/{slug}`, where `{type}` matches the conventional-commit prefix of the session's commit subject (`tidy`, `feat`, `docs`, `fix`, etc.) and `{slug}` is a short kebab-case identifier mirroring the commit's scope. PR #1's branch (`tidy/docs-folder-readmes`) paired with commit subject `tidy: docs — add folder READMEs for routing-layer narrowing` is the in-repo precedent. The convention operationalizes the one-prompt-one-session-one-PR rule and makes the branch ↔ commit ↔ prompt triple one-glance-obvious in `git log` and PR listings.

## Cross-references

- [CLAUDE.md § 5](../../CLAUDE.md) — prompt routing-layer treatment.
- [CLAUDE.md § Operating Disciplines](../../CLAUDE.md) — the one-prompt-one-PR rule and the spec-delta closure loop.
- [`../retrospectives/`](../retrospectives/) — the paired layer of session outcome records.

## Current population

Kinds populated for round one: `rules/` (1), `workshops/` (1), `research/` (2 — ecommerce engineering lessons survey, frontend stack landscape), `docs/` (9 — folder-READMEs, README overhaul, housekeeping sweep, slice 3.1/4.1/4.2/4.6/4.7 and slices 3.2+3.3 doc follow-ups), `narratives/` (3 — Seller catalog-management, Customer browse, Customer purchase), `specs/` (2 — slice 1.1 + 1.2 OpenSpec proposals; from slice 1.3 on, proposals ride consolidated slice PRs without a standalone spec-prompt), `chore/` (2 — Critter Stack 2026 upgrade, infra bundle), `implementations/` (12 — slices 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 4.1, 4.2, 4.3, 4.6, 4.7, 3.2+3.3). Additional kinds (`skills/`, `decisions/`) appear as their first session of that kind lands.
