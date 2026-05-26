---
retrospective: 001
kind: rules
prompt: docs/prompts/rules/001-round-one-structural-constraints.md
deliverable: docs/rules/structural-constraints.md
date: 2026-05-26
mode: solo synthesis (Architect-voice sanity-check pass at the end)
session-runner: Claude (Opus 4.7)
---

# Retrospective — Rules 001: CritterMart Round-One Structural Constraints

## Outcome summary

The session produced `docs/rules/structural-constraints.md` (v1.0) as a flat list of round-one structural constraints distilled from ten ADRs, vision.md, the context map, Workshop 001, and CLAUDE.md's operating disciplines. The file is organized into eleven groups — service topology, persistence, cross-service messaging, HTTP surface, observability, identity, aggregates and process managers, projection lifecycle, event naming, SDD pipeline discipline, and round-one explicit deferrals — for a total of 47 imperative rules. Every ADR (001–010) is cited by at least one rule; ADR 007's four load-bearing event names appear in the event-naming group; every bullet of vision.md § "What this deliberately is not" has a matching rule in the deferrals group; every named operating discipline in CLAUDE.md (one-prompt-one-PR, no opportunistic edits, design-return cadence, spec-delta closure, `tidy:` convention) has a rule in the pipeline-discipline group.

The retrospective itself is this file.

## What worked

- **Reading the orientation sources in the prompt's specified order, in full.** CLAUDE.md first set the artifact's purpose (terse imperative list, the cite is the rationale) and the operating-disciplines source for the pipeline group. The vision doc then anchored the deferrals group. The context map's round-one stubs section turned out to be the canonical source for the no-synchronous-HTTP rule — the ADRs argue the reason; the context map records the rule. The ten ADRs then mapped one-to-many onto the architectural groups, and Workshop 001 supplied the event-naming rules and reinforced the projection-lifecycle constraints. By the time the writing began, the rule list was determined.
- **Naming the canonical source per rule rather than listing every source.** "No synchronous service-to-service HTTP" appears in ADR 001 consequences, ADR 003 decision rationale, and context map § Round-one stubs. The rule cites the context map as primary and ADR 001 as secondary; ADR 003 was dropped from the multi-cite as the third source was rationale-only. This keeps cites pointing at where the prohibition is *recorded* rather than where its reasoning is argued.
- **The eleven suggested groupings from the prompt held up.** The synthesis did not surface a case where a rule belonged in two groups simultaneously, and no group became too small to justify (smallest group is HTTP surface with two rules; largest is SDD pipeline discipline with ten). Group ordering — topology → persistence → messaging → HTTP → observability → identity → aggregates → projection lifecycle → event naming → SDD pipeline → deferrals — reads as architectural-shape-first, process-and-deferrals-last, which matches what an AI session-runner needs in order: "what is this thing" before "how do we work on it" before "what's not in scope."
- **The ~20-word ceiling per rule held in every case.** The longest rule landed at 19 words ("Each slice has both an OpenSpec proposal and a sibling narrative authored before its implementation prompt; both must agree."). Several rules naturally clocked in at 5–10 words and gained no clarity from padding. The discipline forced two candidate "rules" that were really two rules masquerading as one (ADR 007's PMvH-and-no-Saga-base-class line is genuinely two facts conjoined by a semicolon; the workshop-001-§-4 authoritative-source rule was kept separate from the naming-convention rule even though both cite §4). Both splits read cleaner as two rules.
- **The sanity-check pass caught two genuine gaps before file commit.** First pass omitted ADR 004's AspireHost rule entirely — Aspire was treated as orchestration-already-implied-by-OTel, but the AspireHost project shape is a real round-one structural commitment. Added under service topology. Second pass omitted the "vision-doc updates are deliberate" line from CLAUDE.md § Routing Layer — easy to miss because it's a meta-note rather than a numbered discipline. Added under SDD pipeline discipline.

## What was harder than expected

- **Drawing the line between an "architectural" rule and a "code-stylistic" rule was less clean than the prompt's out-of-scope list suggested.** ADR 007 says "process-manager handlers are pure functions, unit-testable without Wolverine or Marten." That sentence sits at a fuzzy boundary: "pure function" is a code-stylistic property, but the testability claim is an architectural commitment about how the PMvH pattern manifests. The rule went into aggregates-and-process-managers under the reading that pure-function handlers are an architectural property of how PMvH is realized in this codebase. Future synthesis sessions may want to revisit this boundary if `docs/skills/` later carries a competing convention.
- **The "where does this rule's canonical source live" question for the no-sync-HTTP rule.** ADR 001's *Consequences* section first names the constraint by implication ("topology is a deployment decision"); ADR 003's *Decision* section names brokered messaging as the mechanism; the context map § Round-one stubs names the prohibition explicitly. All three are legitimate. The synthesis cited the context map as primary (it's the most direct statement of the prohibition) with ADR 001 as the secondary cite for the topological origin. This was a judgment call and could reasonably go the other way.
- **The Catalog "no BC-level integration" rule needed a more careful framing than expected.** The bare statement "Catalog does not communicate with other services" is wrong — the frontend reads Catalog over HTTP, and Cart commands carry product snapshots that originated in Catalog. The rule had to be rephrased to "product fields cross only via the frontend" so that downstream session-runners understand the rule is about *bounded-context-level* integration (no cross-BC messages, no shared kernel, no anti-corruption layer), not about Catalog being isolated in some absolute sense. This is the kind of subtlety that justifies the cite — a reader confused by the terse rule can follow it to the context map's two-paragraph treatment.
- **Choosing whether the "one async projection is `CartAbandonmentReport`" rule cited ADR 008 or Workshop 001 § 7.** ADR 008 specifies the *constraint* (one async teaser, no daemon driven). Workshop 001 § 7 names the *specific projection*. The synthesis split the difference: the projection-existence rule cites ADR 008, and the name-the-projection rule cites Workshop 001 § 7. This is consistent with the deduplication-pass rule of "cite where the rule was made, not where it was argued."
- **The deferrals group needed a composite long-road rule** to honor the prompt's out-of-scope note ("Do not add long-road parking-lot items as their own rules"). Listing each long-road item as a separate rule would have produced six near-identical "X is deferred" lines; collapsing them into one composite rule with the items in parentheses preserved the information density without the noise. The composite cites both vision.md § Long road and context map § Long road.

## Methodology refinements that emerged

These are observations about the rules-synthesis process worth carrying forward, not corrections to the rule list itself.

1. **The inventory-pass count (47 rules from 5 orientation sources) was higher than the prompt's "25–45 candidate" estimate.** The overrun came primarily from CLAUDE.md's operating disciplines section, which carries roughly ten distinct disciplines that all became rules in the SDD pipeline group. If future round-N synthesis sessions add more disciplines, the SDD pipeline group will continue to grow disproportionately. This is fine — the group naturally accumulates as the pipeline matures.
2. **Mandating that every ADR is cited by at least one rule** turned out to be a useful pressure. It surfaced two near-misses (ADR 002's `DatabaseSchemaName` configuration call was almost left implicit; ADR 005's three sub-rules around `TrackLevel.Verbose`, `TrackEventCounters()`, and Wolverine instrumentation were almost compressed into one observability rule). Forcing the cite explicit made the synthesis more faithful to each ADR's actual content. Recommend keeping this discipline in future rule-synthesis sessions.
3. **The four-tier cite format (`(ADR NNN)`, `(vision.md § Section)`, `(context map § Section)`, `(Workshop NNN § Section)`, `(CLAUDE.md § Section)`) is unambiguous and short.** No alternative formats were considered seriously. Recommend keeping.
4. **The forward-compatibility commitment** ("a future ADR that changes a structural constraint pairs with a rule-file update in the same PR") was included as a rule citing this file itself. This is mildly recursive but appropriate: the rule about how the rule file updates lives in the rule file. The alternative — a footnote or convention note — would have been less discoverable for an AI session-runner reading just the rules list.
5. **The retrospective format from Workshop 001's retro** transferred cleanly to this synthesis session despite the different deliverable kind. The seven sections (metadata, outcome, what worked, what was harder, methodology refinements, outstanding items, spec-delta) work for both workshops and rule-synthesis. Recommend treating the format as kind-agnostic going forward.

## Outstanding items / next-session inputs

1. **`tidy: housekeeping` PR to update CLAUDE.md's artifact-layer map.** The map currently shows `docs/rules/` and `docs/retrospectives/` (rules subdirectory) as *forthcoming*. With this session, both are concrete. CLAUDE.md should be updated to drop "forthcoming" from the `docs/rules/` row and add a `docs/retrospectives/rules/` row (or extend the existing retrospectives row's note). Explicitly out-of-scope for this session per the prompt; flagged for a future maintenance session.
2. **Rules that surfaced as candidates but were judged out-of-scope per this prompt:**
   - *"Each service uses Wolverine's transactional middleware (`AutoApplyTransactions`)."* — implementation pattern, belongs in `docs/skills/` not rules.
   - *"Marten document storage uses serialized JSONB columns; event streams use the events schema."* — code-stylistic / Marten convention, belongs in skills.
   - *"Slice numbering follows the workshop's `{BC-area}.{slice}` scheme (e.g., 4.1 for Place Order)."* — naming convention, not an architectural rule; belongs in a workshop skill if anywhere.
   - *"Each handler is registered via Wolverine's discovery conventions."* — code convention, belongs in skills.
   These are not lost — they are candidates for `docs/skills/` entries once the first implementation session surfaces them as recurring patterns.
3. **The forward-compatibility rule will earn its first exercise** the next time an ADR changes a constraint. ADR 011 (whenever it lands) and any subsequent constraint-changing ADR should arrive in a PR that also bumps this file's version, appends a Document-History entry, and updates whichever rules the ADR changes. The retrospective for that next session will be the first chance to confirm the discipline holds.
4. **No new ADR triggered by this session.** This is purely derivative synthesis — no new architectural decisions were made. Any rule the synthesis surfaced that *would* have been a new constraint went into the out-of-scope list above, not into the rule file.
5. **Downstream prompt orientation savings.** The next per-slice implementation prompt can cite `docs/rules/structural-constraints.md` as a single orientation entry instead of enumerating ADR-001-through-010 individually. This reduces per-prompt orientation overhead by roughly nine lines per prompt; over the seventeen round-one slices, that compounds. The first slice's implementation prompt should validate the orientation savings empirically and either confirm or refine the rule file's coverage.
6. **The `docs/rules/` directory now has its first occupant.** Per CLAUDE.md's "subdirectories appear as their first artifact lands; don't pre-create empty ones," the directory is now legitimately present. The directory's purpose-of-existing — terse AI-readable structural constraints — is fulfilled by this single file for round one. If round two introduces orthogonal rule classes (e.g., per-tenant constraints, per-service constraints that vary), they would land as sibling files; not anticipated for round one.

## Spec-delta — landed?

**Yes.** The prompt's spec delta named three things:

1. `docs/rules/structural-constraints.md` created — **landed** at v1.0 with eleven groups and 47 rules.
2. `docs/retrospectives/rules/001-round-one-structural-constraints.md` created — **landed** (this file).
3. The forthcoming `docs/rules/` and `docs/retrospectives/rules/` directories in CLAUDE.md's artifact-layer map become concrete with their first occupants — **landed** (both directories now exist and each contains its first artifact).

No spec-delta items were dropped or downscoped during execution. The forward-compatibility commitment ("any future ADR that changes a structural constraint pairs with a rule-file update in the same PR") is recorded in two places: the rule file's header paragraph and the rule list's SDD pipeline discipline group.

## Process notes

- One prompt, one session, one PR — the PR contains exactly the two named artifacts (rule file + retrospective) and nothing else. CLAUDE.md's artifact-layer map was not edited (explicitly out-of-scope per the prompt). No ADR, vision doc, context map, or workshop file was modified.
- No code committed (per the prompt's out-of-scope list). This is documentation.
- The `Document History` table in the rule file is stamped v1.0 per CLAUDE.md § 4b. Future sessions that touch the rule file append entries here and bump the version.
