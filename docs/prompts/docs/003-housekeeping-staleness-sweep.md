# Prompt: Docs 003 — Housekeeping Staleness Sweep + Branch Convention

**Kind**: maintenance / docs surface (housekeeping — closing deferred items from PR #1's retro)
**Files touched**: `CLAUDE.md` (edit — *Artifact layer map* table rows + Skills section prose); `docs/skills/event-modeling/SKILL.md` (edit — one line in Pipeline Integration); `docs/prompts/README.md` (edit — add branch-per-prompt convention sub-section); `docs/retrospectives/docs/003-housekeeping-staleness-sweep.md` (new)
**Mode**: solo synthesis — narrow-scope housekeeping; identify each stale claim against the orientation material, make the targeted edit, and verify legitimate `forthcoming` references remain.
**Commit subject**: `tidy: housekeeping — close PR #1 deferred items and add branch convention`

## Framing

PR #1 (folder READMEs for routing-layer narrowing) was the first session in the project's pipeline to use a branch-and-PR workflow. Its retrospective ([`docs/retrospectives/docs/001-folder-readmes.md`](../../retrospectives/docs/001-folder-readmes.md)) explicitly named four follow-up items as *Outstanding items / next-session inputs*, the strongest of which was a `tidy: housekeeping` PR to drop stale `forthcoming` annotations from `CLAUDE.md`'s *Artifact layer map* table. This session closes that item plus two adjacent ones surfaced during PR #1 and its follow-up conversation: a single-line staleness in the event-modeling skill, and a small addition to `docs/prompts/README.md` capturing the branch-per-prompt convention validated during PR #1.

A grep sweep across the repo's markdown for `forthcoming` and `TBD` confirms the staleness scope is narrow. Most `forthcoming` references are still load-bearing-truthful — they reference genuinely-forthcoming artifacts (`docs/specs/` proposals, narrative content, per-narrative session subdirs) or sit inside frozen historical prompt/retro files that must not be edited retroactively. The actually-stale set this session closes:

- **`CLAUDE.md` *Artifact layer map* table** — 8 of 10 rows carry `*(forthcoming)*` annotations that no longer match reality (`docs/context-map/`, `docs/workshops/`, `docs/narratives/`, `docs/prompts/`, `docs/retrospectives/`, `docs/skills/`, `docs/rules/`, `docs/decisions/` all exist and are populated). The two rows that legitimately remain forthcoming are `docs/specs/` (no OpenSpec proposals yet) and `docs/research/` (no spikes yet). The Skills row additionally carries a secondary stale claim — `may stay empty in round one` — which is no longer true since the event-modeling skill is authored.
- **`CLAUDE.md` Skills section prose** — the paragraph stating "An empty `docs/skills/` during round one is intentional, not debt" no longer matches reality. The folder is populated with the event-modeling skill and a folder README. Softening to "minimally populated" or equivalent is required.
- **`docs/skills/event-modeling/SKILL.md` Pipeline Integration table** — one row marks `docs/rules/structural-constraints.md` as `*(forthcoming)*`. The rules file landed in the round-one structural-constraints synthesis session and is now an active artifact. Drop the annotation.
- **`docs/prompts/README.md`** — does not currently document the branch-per-prompt convention. PR #1 validated a working shape: branch named `{type}/{slug}` where `{type}` matches the conventional-commit prefix (`tidy`, `feat`, `docs`, `fix`, etc.) and `{slug}` is a short kebab-case identifier matching the work (e.g., `tidy/docs-folder-readmes` for PR #1). Capturing this in the prompts/ README is the right home — the convention applies to every prompt-driven session.

This is a derivative session: every edit corresponds to a claim already established in PR #1's retro or in the in-session conversation that followed it. No new conventions, no new architectural decisions, no new commitments beyond what the project has already aligned on.

## Goal

Make four targeted edits across three existing files, plus the paired retrospective. After this session, a fresh AI session-runner reading `CLAUDE.md`'s *Artifact layer map* table sees an accurate snapshot of which folders are populated and which remain forthcoming; reading the event-modeling skill's Pipeline Integration table no longer follows a "(forthcoming)" link to a file that actually exists; reading `docs/prompts/README.md` finds the branch-per-prompt convention pinned down rather than left as in-conversation tribal knowledge.

The edits are:

1. **`CLAUDE.md` *Artifact layer map* table** — for the 8 stale rows, drop the `*(forthcoming)*` annotation from the Path column. Optionally add a parenthetical README cross-link in the same cell where the folder carries a folder-local README from PR #1 (e.g., `docs/skills/` → `docs/skills/` ([README](docs/skills/README.md))). Keep `docs/specs/` and `docs/research/` rows as forthcoming. The Skills row additionally drops the secondary `may stay empty in round one` claim — its What-it-holds cell should now describe the folder accurately (component-scoped patterns local to CritterMart, with one current skill: event-modeling).

2. **`CLAUDE.md` Skills supporting-layer section prose** — find the paragraph containing "An empty `docs/skills/` during round one is intentional, not debt" and rewrite it to reflect current state. Suggested rewrite: change "empty" to "minimally populated", note that the event-modeling skill is the current local skill, and preserve the "defer to upstream, write only what diverges" framing intact (that part remains accurate).

3. **`docs/skills/event-modeling/SKILL.md` Pipeline Integration table** — find the row referencing `docs/rules/structural-constraints.md *(forthcoming)*` and drop the `*(forthcoming)*` annotation. The "When present" phrasing in the right-hand cell should also tighten — the file is no longer conditional, so the cell can read simply "service-boundary rules, transport selection" or similar.

4. **`docs/prompts/README.md` Operating discipline section** — add a new sub-section (or extend an existing one) capturing the branch-per-prompt convention. Suggested shape: a short paragraph stating that each prompt-driven session runs on its own branch named `{type}/{slug}`, where `{type}` matches the conventional-commit prefix (`tidy`, `feat`, `docs`, `fix`, etc.) and `{slug}` is a short kebab-case identifier mirroring the commit's scope. Reference PR #1's branch (`tidy/docs-folder-readmes`) as the first in-repo precedent. The convention is consistent with the existing one-prompt-one-session-one-PR rule and operationalizes it.

## Orientation

Read these in this order before beginning:

1. **`docs/retrospectives/docs/001-folder-readmes.md`** — the retro that explicitly named this session as a follow-up. Particularly the *Outstanding items / next-session inputs* section, which scopes item #1 ("`tidy: housekeeping` PR to update CLAUDE.md's *Artifact layer map* table"). This is the canonical scope-source for the session.
2. **`CLAUDE.md`** — read end-to-end. The *Routing Layer → Artifact layer map* table is the primary edit target (8 rows). The Skills supporting-layer section is the secondary edit target (one paragraph). Note that other prose in CLAUDE.md may reference the table or the Skills folder — confirm no other prose is stale before completing the session.
3. **`docs/skills/event-modeling/SKILL.md`** — particularly the *Pipeline Integration → Existing Documents to Load* table (the one-line edit target). Skim the rest to confirm no other staleness needs addressing (the file is large and may have other `(forthcoming)` references worth checking).
4. **`docs/prompts/README.md`** — particularly the *Operating discipline* section, where the branch-per-prompt convention sub-section will be added.
5. **`docs/skills/README.md`**, **`docs/workshops/README.md`**, **`docs/narratives/README.md`**, **`docs/retrospectives/README.md`** — skim to confirm each folder README still accurately describes its current population state. No edits anticipated, but verify; a folder README that itself carries stale state would be in-scope for this housekeeping session.
6. **`docs/rules/structural-constraints.md`** — confirm the file exists and is non-empty (this verifies the event-modeling skill's stale `forthcoming` reference is genuinely droppable).
7. **PR #1's commit (`294ee2b`)** — review via `git show` to recall the branch name (`tidy/docs-folder-readmes`) and the in-conversation framing of the branch convention. The convention text in `docs/prompts/README.md` should be consistent with what PR #1 actually did.

## Out of scope

- **Do not edit any frozen historical file** — no prompts, no retros, no the workshop, no ADRs. Their `forthcoming` references reflect what was true at session start and must not be revised retroactively. The session-runner who wrote them did so honestly; the historical record is sacrosanct.
- **Do not author `docs/skills/_template/SKILL.md`.** Still deferred per PR #1's retro; still no trigger to land it.
- **Do not author `docs/skills/DEBT.md`.** Still no deferred skill-file gaps to record.
- **Do not author `docs/specs/README.md`.** Still deferred until the first OpenSpec proposal lands.
- **Do not edit `README.md`.** That is the scope of the separate prompt at [`docs/prompts/docs/002-readme-overhaul.md`](002-readme-overhaul.md), which is pre-authored and uncommitted. This session is housekeeping; the README overhaul is a substantive rewrite — separate concerns, separate PRs.
- **Do not retroactively edit `Frontend | TBD` in `CLAUDE.md`'s tech-stack table.** The frontend stack is genuinely TBD for round one; this is not stale.
- **Do not edit any "still genuinely forthcoming" reference.** Specifically: `docs/specs/` references in `docs/narratives/README.md` and `docs/workshops/README.md`, narrative-content references in the event-modeling skill, per-narrative session subdir references in the narratives README. All of these are accurate.
- **Do not invent new conventions.** The branch-per-prompt convention is what PR #1 already did, captured. Do not extend it with new rules (e.g., "branches must be deleted after merge" or "branches must rebase before PR") that PR #1 did not actually establish.
- **Do not commit any code.** This is documentation.
- **Do not edit `CLAUDE.md`'s "**CritterMart round one** defers to the upstream JasperFx Critter Stack ai-skills library" framing.** That framing remains accurate — the defer-to-upstream discipline is unchanged. Only the "empty / may stay empty" claim about `docs/skills/` is stale; the layering rationale is not.

## Output structure

The three file edits, in working order:

### Edit 1 — `CLAUDE.md` *Artifact layer map* table

Drop `*(forthcoming)*` from these 8 rows (paths only):

| Row | Current path cell | Target path cell (suggested) |
|---|---|---|
| Context map | `docs/context-map/` *(forthcoming)* | `docs/context-map/` |
| Workshops | `docs/workshops/` *(forthcoming)* | `docs/workshops/` ([README](docs/workshops/README.md)) |
| Narratives | `docs/narratives/` *(forthcoming)* | `docs/narratives/` ([README](docs/narratives/README.md)) |
| Prompts | `docs/prompts/` *(forthcoming)* | `docs/prompts/` ([README](docs/prompts/README.md)) |
| Retrospectives | `docs/retrospectives/` *(forthcoming)* | `docs/retrospectives/` ([README](docs/retrospectives/README.md)) |
| Skills | `docs/skills/` *(forthcoming; may stay empty in round one)* | `docs/skills/` ([README](docs/skills/README.md)) |
| Rules | `docs/rules/` *(forthcoming)* | `docs/rules/` |
| ADRs | `docs/decisions/` *(forthcoming)* | `docs/decisions/` |

Keep these two rows as-is (still genuinely forthcoming):

| Row | Path cell |
|---|---|
| OpenSpec proposals | `docs/specs/` *(forthcoming)* |
| Research | `docs/research/` *(forthcoming)* |

The Skills row's What-it-holds cell additionally needs updating — drop any implication of emptiness, describe the folder accurately. Suggested: "Component-scoped patterns local to CritterMart (one current skill: event-modeling)".

The README-link parentheticals are suggested-not-mandated — if the session-runner judges the table reads cleaner with the path-only form and a separate "and there's a folder README" note above or below the table, that's equally valid. The structural goal is "an accurate state of each folder is discoverable from this table"; the typographic shape is a judgment call.

### Edit 2 — `CLAUDE.md` Skills supporting-layer section prose

Find the paragraph in the *Supporting Layers → Skills — component-scoped patterns* sub-section containing "An empty `docs/skills/` during round one is intentional, not debt." Rewrite the sentence to reflect current state. One acceptable shape: replace "empty" with "minimally populated" and add a phrase naming the event-modeling skill as the current occupant. Preserve the surrounding "defer to upstream" framing — only the empty/may-stay-empty claim is at issue.

### Edit 3 — `docs/skills/event-modeling/SKILL.md`

In the *Pipeline Integration → Existing Documents to Load* table, find the row:

| [`docs/rules/structural-constraints.md`](../../rules/structural-constraints.md) *(forthcoming)* | When present — service-boundary rules, transport selection. |

Drop `*(forthcoming)*` from the left cell. Optionally tighten the right cell — "When present —" is no longer conditional, so the cell can read simply "Service-boundary rules, transport selection."

### Edit 4 — `docs/prompts/README.md`

Add a new sub-section under *Operating discipline* (or extend the existing sub-section list) capturing the branch-per-prompt convention. Suggested shape:

> **Branch-per-prompt naming.** Each prompt-driven session runs on its own branch named `{type}/{slug}`, where `{type}` matches the conventional-commit prefix of the session's commit subject (`tidy`, `feat`, `docs`, `fix`, etc.) and `{slug}` is a short kebab-case identifier mirroring the commit's scope. PR #1 established the precedent: branch `tidy/docs-folder-readmes` ↔ commit `tidy: docs — add folder READMEs for routing-layer narrowing`. The convention operationalizes the one-prompt-one-session-one-PR rule and makes the branch ↔ commit ↔ prompt triple one-glance-obvious in `git log` and PR listings.

The session-runner may revise the exact wording; the structural requirement is "the branch-per-prompt naming pattern is captured in `docs/prompts/README.md` so the next session-runner does not re-derive it."

## Working pattern

Five passes:

1. **Orientation pass.** Read the orientation files in the order listed. Confirm the PR #1 retro's *Outstanding items #1* matches the scope of this session. Verify the rules file exists and is non-empty. Verify each of the 8 folders due an annotation update is genuinely populated (not just the README from PR #1 — any folder where the only file is its own README still counts as populated, since the README's presence is what the table annotation is acknowledging).
2. **Edit pass.** Apply the four edits in the order listed (CLAUDE.md table → CLAUDE.md Skills prose → event-modeling skill → prompts README). Order matters because the CLAUDE.md edits inform the framing of the prompts README addition (the branch convention is consistent with the operating discipline CLAUDE.md describes).
3. **Cross-check pass.** Re-grep the repo for `forthcoming` after the edits. Confirm that the remaining hits are: (a) frozen historical files (prompts/retros — leave alone), (b) genuinely-forthcoming references (specs, narratives content, per-narrative session subdirs — leave alone), or (c) folder READMEs whose forthcoming references are still accurate. If anything in the post-edit grep looks newly stale, flag it in the retro's *Outstanding items* — do not opportunistically fix during this session.
4. **Tightness pass.** Re-read each edited file's surrounding paragraphs to confirm the edits read cleanly in context. No stranded references, no broken sentence flow, no orphaned "(forthcoming)" left in a parent paragraph because only the table cell was updated.
5. **Retro pass.** Author the retrospective. The retro must explicitly confirm that PR #1's *Outstanding item #1* now closes; flag the still-deferred items (#2 template, #3 DEBT.md, #4 specs/README.md) as still-deferred with their original triggers intact.

Author the retrospective at `docs/retrospectives/docs/003-housekeeping-staleness-sweep.md` before opening the PR. Use the seven-section format established by `docs/retrospectives/rules/001-...md` and `docs/retrospectives/docs/001-...md`.

The session is one PR per `CLAUDE.md`'s "one prompt = one session = one PR" discipline. The branch follows the convention this session is itself codifying: `tidy/docs-housekeeping-staleness-sweep` (or a similar `tidy/{slug}` form). The PR contains exactly this prompt (pre-authored and uncommitted at session start), the three edited files, and the retrospective. Nothing else.

## Spec delta

`CLAUDE.md`'s *Artifact layer map* table now accurately reflects which folders are populated and which remain forthcoming. The Skills section prose acknowledges the event-modeling skill rather than claiming the folder is empty. The event-modeling skill no longer mis-claims the rules file is forthcoming. `docs/prompts/README.md` carries the branch-per-prompt convention as a documented operating discipline, validating the pattern PR #1 introduced.

PR #1's retrospective *Outstanding item #1* closes. The other three outstanding items from PR #1's retro (template, DEBT.md, specs/README.md) explicitly remain deferred — their triggers (first-need, first-deferred-gap, first-OpenSpec-proposal) are unchanged. The pre-authored README-overhaul prompt at [`docs/prompts/docs/002-readme-overhaul.md`](002-readme-overhaul.md) remains uncommitted, awaiting its own session.

Forward-compatibility note for the retro: this session is the second `docs/` kind PR and validates that the kind is reusable for general doc-surface housekeeping. The `docs/` kind has now seen folder-README authoring (PR #1) and table/prose maintenance (this session), with the pre-authored README overhaul as the third upcoming use. The pattern is general — any documentation-surface change that does not produce a first-class artifact kind (workshop, narrative, OpenSpec proposal, ADR, code slice) can land as a `docs/` kind prompt. Worth confirming in the retro that this generalization holds.
