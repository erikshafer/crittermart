# Retrospective: Docs 010 — Slice 3.4 doc follow-ups (archive, workshop v1.5 amendment, README/index refresh)

**Prompt**: `docs/prompts/docs/010-slice-3-4-doc-followups.md`
**Outcome**: shipped — all of retro 013's named post-merge follow-ups drained in one `tidy: docs` PR. `openspec archive slice-3-4-cart-abandonment` folded the deltas into the durable `shopping-cart` main spec (4 → 7 requirements; the add-item requirement gained schedule-on-creation); the CLI stamped the archive folder `2026-06-03-slice-3-4-cart-abandonment` (UTC date again — retro 007's slug-only lesson held a third time). Workshop 001 → **v1.5**: § 6.1 slice 3.4 amendment blockquote (fire-and-check resolution **with the Wolverine factual correction**, fat `CartAbandoned`, 3.2/3.3 refresh-clause dissolution, report-shape decisions), § 8 open question 1 marked **RESOLVED** with the corrected premise, and the Document History row declaring **round one's modeled implementation set complete**. Root README Orders BC row → Implemented (both aggregates complete) + Getting Started status note rewritten around completion; prompts/retrospectives folder-README counts (`docs/` 9 → 10, `implementations/` 12 → 13); narratives README 004 row → v1.7 (journey fully authored); skills README — `DEBT.md` moved from "Forthcoming companions" to a new "Skill debt" section. `openspec validate --all --strict` 4/4, no active changes.
**Tests**: not applicable — docs-only PR, no code or test changes (CI build/unit/integration jobs run unchanged).

## What worked

- **Retro 013's Outstanding section was file-shaped, and this session was assembled directly from it** — same as the 009 experience. The design.md faithfulness notes 1–5 were lift-ready as the workshop amendment's source text; zero re-derivation. The file-shaped discipline (retro 008's lesson) is now the stable norm.
- **The fifth consecutive tidy of the same shape** (006 → 007 → 008 → 009 → 010). The shape is fully settled: archive → workshop amendment → README/index refresh → prompt/retro pair. Nothing about this session required a decision beyond the scope fork resolved with the user at session start (pure tidy vs. bundling the encode/skills work — pure tidy won).
- **This was the first workshop amendment that corrects the model's facts, not just records a divergence.** § 8 open question 1's premise ("Wolverine supports both") was wrong; the amendment both resolves the question and corrects the record, with the resolution pointer placed *on the open question itself* so a future reader doesn't take the stale premise at face value. The design-return cadence exists exactly for this: implementation reality flowing back into the upstream model.

## What was harder / notable

- Nothing was hard. The session ran in a fraction of an implementation session's time, as expected for the settled shape.
- **Surfaced (not fixed — no opportunistic edits): the workshop's frontmatter `version:` field is stale at `v1.0`** while its Document History now reaches v1.5. All five amendment tidies (v1.1–v1.5) edited only the history table, never the frontmatter — so this is established (if probably accidental) precedent, and this session followed it rather than silently diverging. One-line fix; a candidate for the next tidy or the encode session. Contrast: narrative 004's frontmatter version *is* bumped each amendment — the two artifact classes have drifted into different conventions.
- **The ceremony rule has now held a fourth and fifth time** (this prompt/retro pair + 009's). The encode session is no longer just overdue — it was explicitly scheduled with the user at this session's start as the **next session**.

## Methodology refinements

- None new. The session exercised already-recorded refinements (file-shaped outstanding items, slug-only archive naming, the ceremony rule, the session-start scope fork). The methodology layer is stable; the remaining work is *encoding* it (the next session's job), not refining it.

## Outstanding / next-session inputs

- **ROUND ONE'S MODELED IMPLEMENTATION SET IS COMPLETE.** Every slice in Workshop 001 has shipped, every journey in Narrative 004 is authored, all four durable main specs are folded (`product-catalog` 4 reqs, `stock-management` 4 reqs, `shopping-cart` 7 reqs, `order-lifecycle` 8 reqs), and there are no active openspec changes.
- **Next session (confirmed with the user at this session's start): the `tidy: encode` bundle** — (1) encode the ceremony rule (spec-content tidy → full prompt/retro pair; mechanical tidy → may run light) into CLAUDE.md's operating disciplines or `docs/rules/`; (2) encode the one-capability-per-aggregate convention (Orders BC shape is final: `shopping-cart` + `order-lifecycle`); (3) drain skills DEBT row 1 (the `marten-projection-conventions` local skill). Optionally also fix the workshop frontmatter-version staleness surfaced above.
- **After the encode session: the vision-level conversation** — frontend, talk storyboard assets, round-two modeling, CritterWatch (ADR 013, still blocked on tier/feed/license). Not a slice prompt; round one has no slices left.
- **Design-return cadence**: this tidy banks the credit after #41. Moot unless round two opens.
- **Carry-forward unchanged**: `StockCommitted` still unmodelled (do not invent); CritterWatch (ADR 013) still blocked on tier/feed/license.

## Spec-delta — landed?

**Yes.** The prompt named: Workshop 001 § 6.1 slice 3.4 amendment + § 8 open question 1 resolution + Document History v1.5 as the spec delta, with the openspec archive as closure-only and the README edits as non-spec-shaped. All landed as named — no expansion, no shortfall. `openspec validate --all --strict`: 4/4 passed, zero active changes.
