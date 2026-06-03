---
retrospective: 002
kind: rules
prompt: docs/prompts/rules/002-encode-bundle.md
deliverable: CLAUDE.md, docs/rules/structural-constraints.md, docs/skills/marten-projection-conventions/SKILL.md
date: 2026-06-02
mode: solo synthesis (three forks resolved with the user before the prompt was frozen)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Rules 002: Encode Bundle

## Outcome summary

The pipeline's first `tidy: encode` session — the kind CLAUDE.md § Operating Disciplines named (`tidy: encode-<rule>`) but never exercised. Three conventions left retrospective archaeology and landed in their durable homes:

1. **The tidy ceremony rule** (held 5× across retros docs/007–010) → a new `### Tidy ceremony rule` subsection in CLAUDE.md § Operating Disciplines, plus a flat imperative in `docs/rules/structural-constraints.md` § SDD pipeline discipline.
2. **One capability per aggregate** (evidence chain: Catalog 3/3 → Inventory → Orders' two capabilities) → a new `**Capability granularity:**` bullet in CLAUDE.md § 4a, plus a flat imperative in the rules file.
3. **The CritterMart Marten projection conventions** (DEBT row 1, each used 3×) → `docs/skills/marten-projection-conventions/SKILL.md`, the project's second local skill. DEBT row 1 → **Drained**; no open DEBT rows remain.

Riding along as named: Workshop 001's frontmatter `version:` fixed (v1.0 → v1.5, matching its Document History), and the convention preventing recurrence (frontmatter tracks Document History) encoded in `docs/workshops/README.md` § Output discipline plus a third rules-file imperative. `structural-constraints.md` → **v1.3**. Prompts/retrospectives README population counts updated (`rules/` 1 → 2).

## What worked

- **The session was assembled entirely from file-shaped inputs.** Retro docs/010's Outstanding section named the exact scope; DEBT row 1 carried the skill's content inventory; retros 007–010 carried the ceremony rule's wording; the shipped Orders code carried the projection conventions. Zero re-derivation — which is itself the proof of the discipline this session encodes: the conventions were *recoverable* from retros, but only by reading five of them. Now they're readable in one place each.
- **Verifying the skill's file references against the actual code before shipping.** Every `src/` and `tests/` path named in the skill was checked to exist (Glob), and the rebuild-test reference was confirmed to live in `CartAbandonmentTests.cs` (Grep for `RebuildProjectionAsync`) rather than the file its name might suggest (`CartAbandonmentReportProjectionTests.cs`). A skill whose references are wrong is worse than no skill.
- **Resolving the three forks before freezing the prompt** (the 013 pattern, now standard): both-surfaces-paired encoding, the rules/002 kind, and frontmatter-fix-plus-convention were all settled with the user in one exchange. The prompt froze decisions, not options.
- **The skill leads with the `partial` trap**, per retro implementations/013's explicit instruction ("the future skill note should lead with it"). Convention ordering inside a skill is itself content: the thing that cost a session goes first.

## What was harder than expected

- **Nothing was hard.** Like the settled-shape doc tidies, the encode session is mechanical once its inputs are file-shaped. The judgment calls (where each convention lives, how the rules cite retros rather than ADRs) were pre-resolved by the user forks.
- **One judgment call worth recording: the rules file now cites retrospectives.** Every prior rule cites an ADR, a vision/context-map section, CLAUDE.md, or the workshop. The three new rules cite CLAUDE.md sections (which this session also wrote) and retros (where the evidence lives). This is correct — these are *process* conventions whose "decision record" is the retro chain, not an ADR — but it's a new cite shape for the file. If a future session finds process rules accumulating, a dedicated conventions ADR could consolidate the cite targets.

## Methodology refinements that emerged

1. **An encode session's quality is bounded by how file-shaped its inputs are.** This one was fast *because* five retros and a DEBT row had already done the wording. The corollary: when a retro flags something as "ready to encode," it should include the canonical wording — the encode session then only chooses homes, not words.
2. **Local skills should name their own staleness mechanism.** The new skill states "when this skill and the code disagree, the code wins and this skill gets a DEBT row" — borrowing the rules file's paired-update discipline. Recommend this line in every future local skill.

## Outstanding items / next-session inputs

- **Next session: the vision-level conversation.** Round one is complete and the first talk (ImprovingU) has been delivered. The user has confirmed a **functional UI must exist before the second talk** (online .NET user group). The conversation must produce: a frontend stack decision (ADR-worthy; `docs/prompts/research/002-ecommerce-frontend-stack.md` + its retro are prior art), a decision on how UI work enters the pipeline (workshop amendment with UI slices vs. thin client outside the modeled boundary), and round-two scope. Identity's hardcoded-customer-ID stub (ADR 009) becomes load-bearing the moment a frontend exists — the conversation should name where that ID actually lives.
- **Surfaced, not fixed (out of scope per the prompt): Workshop 001's frontmatter `status:` field reads `Draft`.** The workshop's modeled set is fully implemented; `Draft` is arguably stale the same way `version:` was. Not named by retro docs/010, so not touched here. One-word candidate for the next workshop-touching session — which, if round two opens, would also decide what `status:` values the convention even allows (Draft → Active → Superseded?).
- **Carry-forward unchanged:** `StockCommitted` still unmodelled (do not invent); CritterWatch (ADR 013) still blocked on tier/feed/license.
- **Design-return cadence:** moot until round two opens; this session is a design-tidy and would bank the credit regardless.

## Spec-delta — landed?

**Yes, as named.** The prompt named: two CLAUDE.md convention passages (§ Operating Disciplines, § 4a); structural-constraints.md → v1.3 with three new SDD rules + Document History row; the new `marten-projection-conventions` skill + DEBT row 1 → Drained; Workshop 001 frontmatter `version:` → v1.5 (value only) + the workshops README convention sentence; both population counts. All landed; no expansion, no shortfall. The one unnamed surfacing (`status: Draft`) was recorded above rather than edited — the no-opportunistic-edits discipline held.
