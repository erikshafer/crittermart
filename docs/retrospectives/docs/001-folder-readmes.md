---
retrospective: 001
kind: docs
prompt: docs/prompts/docs/001-folder-readmes.md
deliverable: docs/skills/README.md, docs/workshops/README.md, docs/narratives/README.md, docs/prompts/README.md, docs/retrospectives/README.md
date: 2026-05-26
mode: solo synthesis
session-runner: Claude (Opus 4.7)
---

# Retrospective — Docs 001: Folder READMEs for Routing-Layer Narrowing

## Outcome summary

The session produced five folder READMEs: `docs/skills/README.md`, `docs/workshops/README.md`, `docs/narratives/README.md`, `docs/prompts/README.md`, and `docs/retrospectives/README.md`. Each README is convention-shaped (folder-local naming, frontmatter, required-sections list, in-repo format precedent pointer, current-population state) and deliberately not index-shaped (no file enumeration, no duplication of CLAUDE.md prose). The session also bootstrapped the `docs/prompts/docs/` and `docs/retrospectives/docs/` subdirectories with their first occupants (this prompt and retrospective). Length per README landed in the 35–60 line range, well inside the prompt's 30–80 target.

CLAUDE.md was not edited. The *Artifact layer map* table's *forthcoming* annotations for the five affected folders are now stale (each folder has at least one file beyond what the table acknowledges), and that staleness is honestly flagged in *Outstanding items* below rather than fixed inline.

The retrospective itself is this file.

## What worked

- **Reading the in-repo precedent files (`prompts/rules/001-...md`, `retrospectives/rules/001-...md`, `workshops/001-crittermart-event-model.md`, `skills/event-modeling/SKILL.md`) before drafting.** CLAUDE.md prescribes format conventions in prose; the in-repo files realize them in practice. The README content draws from both — CLAUDE.md for the *what must be there*, the in-repo files for the *what does it look like when authored*. Without reading both, the READMEs would have either restated CLAUDE.md verbatim (dead weight) or invented conventions the existing files don't follow.
- **Writing skills/README.md first.** The "local skills are CritterMart-specific divergences from upstream, not duplications" framing is genuinely load-bearing and not stated in CLAUDE.md with the same crispness. Establishing that framing first made the workshops/, narratives/, prompts/, and retrospectives/ READMEs easier to write because each one has a similar "upstream + local" or "CLAUDE.md + in-repo precedent" structure that draws from the skills/ framing.
- **The convention-shaped, not index-shaped discipline held.** No README enumerates the files in its folder. Each README points at a single representative example (`event-modeling/SKILL.md` from skills, `001-crittermart-event-model.md` from workshops, the rules/001 prompt+retro from prompts/ and retrospectives/) as a precedent. The "Current population" section at the bottom of each README names the population state at *count + first occupants* granularity (e.g., "Kinds populated for round one: rules/ (one prompt), workshops/ (one prompt), docs/ (one prompt — this folder-READMEs session)"), which is stable across single-file additions and only needs editing on kind-level changes.
- **Cross-references between prompts/ and retrospectives/ READMEs landed cleanly.** Each names the other as the paired layer in its *Cross-references* section. A session-runner landing at one folder discovers the other via the cross-reference, without needing to re-read CLAUDE.md to learn the prompts/retros pairing.
- **The "forthcoming companions" framing in skills/README.md (`_template/SKILL.md` and `DEBT.md`)** honored the CLAUDE.md "subdirectories appear as their first artifact lands; don't pre-create empty ones" discipline while still surfacing the existence of those files for a future session that would otherwise re-derive them.
- **The "tightness test" — does this duplicate CLAUDE.md? — was the most useful single filter.** Several candidate sentences failed it and were cut during the tightness pass. The remaining content is genuinely folder-local detail (naming patterns, in-repo format precedent pointers, sibling-artifact relations, current-population state) that CLAUDE.md does not carry.
- **The four-pass working pattern (read → draft → tighten → cross-reference) mapped one-to-one onto the session.** No pass was skipped or compressed; no extra pass was needed. Recommend this pattern for future README-synthesis sessions.

## What was harder than expected

- **Choosing the prompt's kind subdirectory.** Existing kinds (`rules/`, `workshops/`) match the artifact they produce. This session's deliverable is folder READMEs — meta-documentation that doesn't fit `workshops`, `narratives`, `skills`, `decisions`, or `implementations` (the kinds CLAUDE.md names). The choice came down to `docs/` (most literal description of the work) vs `tidy/` (matches the `tidy:` commit subject convention). Picked `docs/` because `tidy:` is a *commit-subject* convention, not a *prompt-kind* convention; conflating the two layers would muddy both. The `docs/` kind is also reusable for future doc-surface work (skill template, DEBT.md, CLAUDE.md housekeeping).
- **The narratives/README.md had no in-repo precedent to draw from.** Unlike workshops, prompts, and retrospectives — each of which has at least one existing file in the folder — narratives are forthcoming for round one. The README was authored from CLAUDE.md § 4b prose alone. This is fine but means the README will likely receive a small update when the first narrative lands and reveals subtleties CLAUDE.md's prose did not fully cover (frontmatter shape variations, Moment-structure conventions in practice, Document History table format). Flagged as a next-session input.
- **The CLAUDE.md staleness was tempting to fix inline.** With the five READMEs authored, the *Artifact layer map* table reads slightly stale — the *forthcoming* annotations on `docs/skills/`, `docs/narratives/`, `docs/prompts/`, and `docs/retrospectives/` (and `docs/workshops/`, `docs/decisions/`, `docs/rules/`, `docs/context-map/`, all of which are populated) are inconsistent with reality. Honoring the no-opportunistic-edits discipline and the prompt's explicit out-of-scope line, the inconsistencies were left in place and flagged in *Outstanding items*. The discipline is doing its job — opportunistic CLAUDE.md edits would have inflated this session's PR scope unpredictably.
- **The "Current population" line risked drifting into index territory.** Initial drafts named every file in each folder; the rewrite landed on *count + first occupants* (or *kinds populated + first occupants* for prompts/ and retrospectives/). The discipline-test for this kind of line is "does this need editing every time a file is added?" — if yes, the line is too detailed. The chosen form needs editing only when a new *kind* of file appears, which is a slower cadence and a more meaningful change.
- **Deciding how aggressively to cross-reference between READMEs.** Initial drafts had every README pointing at every other; cutting back to the genuinely-paired cross-references (prompts ↔ retrospectives; workshops → event-modeling skill; narratives → specs + workshops) produced cleaner READMEs without losing discoverability. The discipline: cross-reference where the relationship is structural, not merely topical.

## Methodology refinements that emerged

These are observations about the README-authoring process worth carrying forward.

1. **The "does this duplicate CLAUDE.md?" tightness test belongs in any docs-surface synthesis session.** It is the single filter that prevented the READMEs from becoming a parallel copy of CLAUDE.md's pipeline prose. Recommend codifying as a standard pass for future doc-surface sessions.
2. **The "Current population" section is convention-shaped, not index-shaped, only when it names *count + first occupant* (or *kinds populated + first occupant*) rather than *every file*.** This is a subtle distinction worth naming: a population line that says "Kinds populated for round one: rules/ (one prompt), workshops/ (one prompt), docs/ (one prompt)" is convention-shaped (describes the folder's current zoom-level state); a line that lists each file is index-shaped (duplicates `ls`). The former is stable; the latter rots.
3. **Folder READMEs should cross-reference each other only when the relationship is structural.** prompts ↔ retrospectives are paired by discipline (one-prompt-one-PR, spec-delta closure). workshops → event-modeling skill is paired by process (the skill is *how*; the workshop is *what*). narratives → specs is paired by sibling-spec rule. Other relationships are topical; topical cross-references inflate without adding navigation value.
4. **The "forthcoming companions" framing (used in skills/README.md for `_template/SKILL.md` and `DEBT.md`) is a model for any folder that has known-future occupants but doesn't pre-create them.** The pattern surfaces what's coming without ceremony. Recommend reusing in any future README that has similar shape.
5. **The retrospective format from `rules/001-...md` transferred cleanly to a docs-kind session.** This is the second confirmation that the seven-section format is kind-agnostic (the first confirmation was the rules retro itself reporting the format transferred from the workshop retro). Recommend treating the format as project-wide and not per-kind.
6. **The four-pass synthesis (read → draft → tighten → cross-reference) is a good shape for any README-synthesis session.** Each pass has a distinct role; none was redundant. Recommend codifying this in the prompts/README.md or a future skills entry if README-synthesis becomes a recurring activity.

## Outstanding items / next-session inputs

1. **`tidy: housekeeping` PR to update CLAUDE.md's *Artifact layer map* table.** With this session, the table's *forthcoming* annotations are stale for `docs/skills/`, `docs/narratives/`, `docs/prompts/`, `docs/retrospectives/`, `docs/workshops/`, `docs/decisions/`, `docs/rules/`, and `docs/context-map/` (every annotated folder is now populated). A future housekeeping session should drop the annotations from those rows. Optionally, the table could gain a footnote or column indicating which folders carry a local README. Out-of-scope per this session's prompt; the maintainer should schedule this when convenient — it is not load-bearing for any downstream session.
2. **`docs/skills/_template/SKILL.md` not authored.** The README references it as forthcoming. Will land when the first need for a second local skill arises or when a template gap is explicitly recognized. Suggested commit subject: `tidy: docs/skills — add skill authoring template`.
3. **`docs/skills/DEBT.md` not authored.** The README references it as forthcoming. Will land when the first skill-gap is deferred and recorded. Suggested commit subject: `tidy: docs/skills — add DEBT.md with first deferred gap`.
4. **`docs/specs/README.md` deferred.** Per scope discussion, the specs/ folder's convention layer waits until the first OpenSpec proposal lands and the upstream OpenSpec tool's conventions are observed in practice. The narratives/README.md references `docs/specs/` as a forthcoming sibling and will earn an update when specs/README.md lands.
5. **The narratives/README.md will receive its first practical-use update when the first narrative lands.** The README documents the shape from CLAUDE.md § 4b prose alone; the first authored narrative may surface subtleties (frontmatter variations, Moment-structure conventions, Document History table format) that earn a small README amendment. This is expected and not a deficiency.
6. **No new ADR triggered.** This session is documentation-only; no architectural decision was made. The choice of `docs/` as the prompt kind subdirectory is below the ADR threshold (the pipeline's kinds list is explicitly descriptive-not-prescriptive per CLAUDE.md § 5, so adding a new kind is a normal-cadence event, not an architectural one).

## Spec-delta — landed?

**Yes.** The prompt's spec delta named four things:

1. Five folder READMEs land in the same PR — **landed.** All five are authored, each as a thin convention-shaped narrowing surface within the 30–80 line target.
2. The `docs/prompts/docs/` and `docs/retrospectives/docs/` subdirectories appear with their first occupants — **landed.** Both subdirectories are created via this session's prompt and retro.
3. `docs/skills/` gains its first non-skill artifact (a folder-meta README, not a SKILL.md) — **landed.**
4. Downstream sessions landing in any of the five folders gain a thin convention layer between CLAUDE.md and the artifacts themselves — **landed.** The convention-vs-index discipline held; each README answers folder-local questions CLAUDE.md does not, without duplicating CLAUDE.md.

The forward-compatibility note from the prompt ("each README's purpose is folder-local convention, not pipeline philosophy; future CLAUDE.md changes pair with matching README updates in the same PR") is recorded in the prompt's spec-delta section and reaffirmed here.

The CLAUDE.md staleness flagged in *Outstanding items* is honest negative-space on the spec delta — the prompt explicitly out-of-scoped CLAUDE.md edits, and the retro reports the resulting (intentional) inconsistency rather than confabulating its closure.

## Process notes

- One prompt, one session, one PR — the PR contains exactly the seven named files (prompt + five READMEs + retro) and nothing else. CLAUDE.md was not edited (explicitly out-of-scope per the prompt). The `docs/skills/_template/`, `docs/skills/DEBT.md`, `docs/specs/README.md`, `docs/decisions/README.md`, `docs/research/README.md`, `docs/rules/README.md`, and `docs/context-map/README.md` files referenced in scope discussion were either explicitly deferred (template, DEBT.md, specs/README.md) or explicitly declined (decisions/, research/, rules/, context-map/) per the prompt's out-of-scope list.
- No code committed (per the prompt's out-of-scope list). This is documentation.
- The session's deliverable count (7 files) is the highest in the pipeline so far; prior sessions produced 2 files each (artifact + retro). The count is higher because folder READMEs are inherently many-to-one with the underlying convention CLAUDE.md describes — each folder needs its own README. Future `docs/` kind sessions (housekeeping, doc-surface maintenance) will likely return to the 1–3 file range.
- The four-pass synthesis model (read → draft → tighten → cross-reference) is a candidate for a future skills entry if README-synthesis recurs. Not authored this session; flagged as a possible future skill.
