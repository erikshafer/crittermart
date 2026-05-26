---
retrospective: 003
kind: docs
prompt: docs/prompts/docs/003-housekeeping-staleness-sweep.md
deliverable: CLAUDE.md (edit), docs/skills/event-modeling/SKILL.md (edit), docs/prompts/README.md (edit)
date: 2026-05-26
mode: solo synthesis
session-runner: Claude (Opus 4.7)
---

# Retrospective — Docs 003: Housekeeping Staleness Sweep + Branch Convention

## Outcome summary

The session produced four targeted edits across three existing files:

1. **`CLAUDE.md` *Artifact layer map* table** — dropped `*(forthcoming)*` annotations from 8 of 10 rows (Context map, Workshops, Narratives, Prompts, Retrospectives, Skills, Rules, ADRs), kept two rows as legitimately forthcoming (OpenSpec proposals, Research), and added link wrappers in the Path column so every populated row now points at either its canonical artifact (Context map's README, Rules' structural-constraints.md) or its folder-local README (Workshops, Narratives, Prompts, Retrospectives, Skills) or its folder (ADRs, since multiple ordered files share the folder without a meta-README). The Skills row's What-it-holds cell additionally gained an acknowledgment of the current local skill: "Component-scoped patterns local to CritterMart (one current skill: event-modeling)".
2. **`CLAUDE.md` Skills supporting-layer section prose** — replaced the "An empty `docs/skills/` during round one is intentional, not debt" line with "A minimally populated `docs/skills/` during round one is intentional, not debt", and extended the preceding sentence to acknowledge that a local skill is also authored when a project-specific methodology needs its own home (with `event-modeling` as the in-repo example). The surrounding "defer to upstream" framing was preserved intact — it remains accurate.
3. **`docs/skills/event-modeling/SKILL.md` Pipeline Integration table** — dropped `*(forthcoming)*` from the `docs/rules/structural-constraints.md` row and tightened the right-hand cell from "When present — service-boundary rules, transport selection" to "Service-boundary rules, transport selection" (the file is no longer conditional).
4. **`docs/prompts/README.md` Operating discipline section** — added a fourth bullet titled "Branch-per-prompt naming" documenting the `{type}/{slug}` branch convention with PR #1's `tidy/docs-folder-readmes` ↔ `tidy: docs — add folder READMEs for routing-layer narrowing` as the in-repo precedent.

PR #1's retro *Outstanding item #1* (the `tidy: housekeeping` CLAUDE.md table update) explicitly closes with this session. The other three outstanding items from PR #1's retro (skill template, DEBT.md, specs/README.md) remain deferred with their original triggers unchanged.

The retrospective itself is this file.

## What worked

- **The pre-authored prompt made execution mechanical.** Each of the four edits had a clearly-named target, a clearly-stated old/new content shape, and an explicit validation criterion in the prompt's Output structure section. The session-runner did not need to invent any of the four edits' shape during execution — only confirm against the orientation material and apply. This is the strongest argument so far for the pre-authoring pattern: the second time a prompt was authored ahead of execution (the first being prompt 002 for the README overhaul), the execution cost dropped noticeably.
- **The cross-check grep pass (working-pattern step 3) was the single discipline that confirmed the sweep was complete.** The pre-edit grep had identified the staleness scope; the post-edit grep confirmed the remaining hits were all legitimate. Without the post-edit pass, the session would have ended with reasonable confidence; with it, the session ended with high confidence. Recommend codifying as a standard pass in future staleness-sweep sessions.
- **The two-stage CLAUDE.md edit approach (table first, then prose, with the table edit batched alongside the two other-file edits) avoided same-file race conditions.** The Edit tool's read-modify-write semantics mean parallel edits to the same file can clobber each other. Sequencing the CLAUDE.md edits while parallelizing the cross-file edits gave the best of both: throughput where independent, sequence where dependent.
- **The README cross-link convention in the Artifact layer map** (link the path-in-backticks to the folder's README, or to the canonical artifact when the folder has one) enriches the table without making it visually noisier. A reader scanning the table now finds, in one glance, both "which folder is this?" and "where do I read more?" without needing a second column or a separate index.
- **The Vision row was correctly left untouched.** Its existing `[docs/vision.md](docs/vision.md)` form is the same pattern the other rows now adopt — the consistency is now table-wide rather than vision-only.
- **The "frozen historical files are sacrosanct" discipline held cleanly.** The cross-check grep returned many hits inside `docs/retrospectives/` and `docs/prompts/` (PR #1's retro and prompt, the rules retro and prompt, the workshop retro). None were edited. The historical record of "what was true at session start" is preserved.

## What was harder than expected

- **Triaging the cross-check grep's results required care.** The pre-edit grep had 30+ hits; the post-edit grep still had ~25. Each hit had to be categorized as one of: (a) still-truthful reference to a genuinely-forthcoming artifact (specs/, research/, narrative content), (b) frozen historical file (must-not-edit), or (c) genuinely-stale leftover. The triage was unambiguous in every case, but the volume meant the cross-check pass took noticeably longer than the four edits themselves combined. Future staleness sweeps with similar hit-density should expect the same cost profile.
- **Choosing the link target for the Context map row.** The folder IS its README — `docs/context-map/README.md` is the canonical artifact, not a meta-README. The question became: link the folder path to its README (pattern that fits Workshops/Narratives/etc.) or link the README path directly to itself (most honest reflection of what the artifact is)? Picked the second: `[docs/context-map/README.md](docs/context-map/README.md)`. The Rules row faced the same question and got the same treatment (`[docs/rules/structural-constraints.md](docs/rules/structural-constraints.md)`) — single-artifact folders link directly to their artifact; multi-artifact folders with a meta-README link the folder path to the README; multi-artifact folders without a meta-README (ADRs) link the folder path to itself.
- **The Skills row's What-it-holds update risked drift into index territory.** "Component-scoped patterns local to CritterMart (one current skill: event-modeling)" names the one current occupant in a way that needs editing only when a second local skill lands. If three or more local skills accumulate, the parenthetical should drop in favor of a count ("several local skills, currently: ..." or just remove the parenthetical and let the README do the enumeration). Worth noting the threshold for future maintainers.
- **The branch-per-prompt convention's framing.** The bullet documents what PR #1 did — branch named `{type}/{slug}` mirroring the conventional-commit subject. Choosing to lock in that exact pattern, rather than leaving the convention looser ("any consistent branch-per-PR scheme"), was a judgment call. Locking it in matches what PR #1 actually did and gives future sessions an unambiguous pattern to follow; loosening it would leave room for divergence. Picked locking-in; the retro's *Outstanding items* flags that the convention's wording can be revisited if the second or third session's natural branch name doesn't fit the pattern.

## Methodology refinements that emerged

These are observations about the staleness-sweep process worth carrying forward.

1. **The cross-check grep pass earns its own bullet in any future staleness-sweep prompt.** It is the single best validator that a sweep is complete. The pattern: pre-edit grep identifies the staleness scope, edits apply, post-edit grep confirms the remaining hits are all legitimate. Both grep passes are short; both are high-leverage. Recommend including in the working-pattern section of any future sweep prompt.
2. **The frozen-historical-files discipline should be a named explicit out-of-scope item in any sweep prompt.** This session's prompt named it; the discipline held cleanly. Without an explicit out-of-scope statement, the temptation to "tidy up" outdated language in old prompts/retros would be real. The rule is: a frozen artifact's content reflects what was true at the moment of its session's close, and editing it retroactively is rewriting history, not housekeeping.
3. **Same-file edit ordering in tool batching is a real concern with the Edit tool.** Two edits to the same file in one tool batch can clobber each other (each Edit operation does read-modify-write on the full file; the second write overwrites the first if both ran in parallel). The mitigation is straightforward: sequence same-file edits, parallel different-file edits. Worth noting in a future "edit-driven session" skill or in a CLAUDE.md operating-discipline note if it recurs.
4. **The link-the-path-to-its-README pattern in artifact-registry tables is reusable.** The CLAUDE.md *Artifact layer map* now uses it; any similar table elsewhere (e.g., a future `docs/skills/README.md` Current local skills table, a future `docs/decisions/` index) could adopt it for consistency. Could be extracted into a folder-README convention if the pattern recurs.
5. **Pre-authoring prompts and executing them later validates the prompt's own clarity.** This session's prompt was authored in a prior turn with no immediate intent to execute; execution in this turn was the first test of whether the prompt was self-sufficient. It was — the session-runner did not need to ask any clarifying question of the prompt-author (also the session-runner) during execution. This is a positive signal for the pre-authoring pattern, though N=1 is not yet evidence.

## Outstanding items / next-session inputs

1. **PR #1's retro outstanding items #2, #3, #4 remain deferred with original triggers intact.**
   - `docs/skills/_template/SKILL.md` — lands when the first need for a second local skill arises or when a template gap is explicitly recognized.
   - `docs/skills/DEBT.md` — lands when the first skill-file gap is deferred and recorded.
   - `docs/specs/README.md` — lands after the first OpenSpec proposal exists and its conventions are observed in practice.
2. **Session 002 (root README overhaul) is the second session in this bundled PR.** The pre-authored prompt at `docs/prompts/docs/002-readme-overhaul.md` is the next session's intent. Session 002 is run, retro'd, and committed on the same branch as this session; the two commits land together in one PR per maintainer direction (see *Process notes*).
3. **The CLAUDE.md Skills row's "(one current skill: event-modeling)" parenthetical will need updating when a second local skill lands.** Not a deferral so much as a natural maintenance touch — the session that authors the second skill should drop the parenthetical or rephrase it.
4. **The branch-per-prompt convention's wording can be revisited.** If a future session's natural branch name doesn't fit the `{type}/{slug}` pattern (e.g., a branch that pairs with a commit subject lacking a conventional-commit prefix), the convention earns a revision. Currently the convention assumes every prompt's commit subject carries a conventional-commit prefix, which has held for all three sessions so far.
5. **No new ADR triggered.** This session is purely documentation maintenance; no architectural decision was made or revisited. The Skills row's small-but-real-content update is not an architectural change — it's a state-of-fact update.

## Spec-delta — landed?

**Yes.** The prompt's spec delta named four items plus PR #1 outstanding-item closure:

1. CLAUDE.md *Artifact layer map* accurately reflects populated/forthcoming state — **landed.** Eight rows updated; two rows correctly preserved as forthcoming.
2. CLAUDE.md Skills section prose acknowledges the event-modeling skill rather than claiming the folder is empty — **landed.** The "minimally populated" framing replaces "empty" while preserving the "defer to upstream" intent.
3. Event-modeling skill no longer mis-claims the rules file is forthcoming — **landed.** One-line edit applied; right cell tightened.
4. `docs/prompts/README.md` carries the branch-per-prompt convention as a documented operating discipline — **landed.** New bullet under *Operating discipline* documents the `{type}/{slug}` pattern with PR #1 as precedent.
5. PR #1's *Outstanding item #1* closes — **confirmed.** The retro for PR #1 explicitly named "`tidy: housekeeping` PR to update CLAUDE.md's artifact-layer map" as a follow-up; this session is that PR.

The forward-compatibility note from the prompt ("future canonical-artifact changes earn matching README/CLAUDE.md updates in the same PR") is reaffirmed here: the discipline is the convention's expression in CLAUDE.md's operating disciplines, not a new rule introduced by this session.

## Process notes

- **Multi-session bundling per maintainer direction.** Per the maintainer's explicit choice in the conversation that led to this session, this session's deliverables and the deliverables of session 002 (root README overhaul) are bundled into a single PR titled around root document cleanup. This deliberately deviates from CLAUDE.md's "one prompt = one session = one PR" rule. The deviation is honored at the PR level only; both sessions retain their own commits with their own prompts, deliverables, and retros one-to-one. A future contributor reading `git log` finds the expected one-session-one-commit shape; the bundling is visible only at the PR-level review. The maintainer accepted this deviation knowingly; recording it here makes the choice part of the historical record rather than an undocumented exception.
- **First Edit-tool-driven session.** Prior sessions (PR #1, the workshop, the rules synthesis) wrote new files via the Write tool. This session uses Edit against three existing files. The mechanics differ — Edit requires precise old_string matching, which made the pre-authored prompt's specificity about the exact text to change especially valuable. Future Edit-driven sessions should consider including representative old_string snippets in the prompt's Output structure section to make execution similarly mechanical.
- **Two-stage CLAUDE.md edit batching.** The CLAUDE.md table edit was batched in parallel with the two other-file edits (event-modeling skill, prompts/README) since those are independent files; the CLAUDE.md prose edit was then run sequentially. This avoided a same-file race condition. The pattern is: parallel where files are independent, sequential where files overlap. Worth noting for any future session with multiple edits to a single file.
- **One prompt, one session — but committed alongside session 002's commit on a shared branch.** The PR contains exactly the four named files this session edited plus this session's prompt and retro, in one commit titled `tidy: housekeeping — close PR #1 deferred items and add branch convention`. Session 002's commit (forthcoming on the same branch) carries that session's deliverables. The branch (`tidy/docs-root-document-cleanup`) and PR title cover the bundled framing; the commits cover the per-session granularity.
- **No code committed.** This session is documentation maintenance only.
