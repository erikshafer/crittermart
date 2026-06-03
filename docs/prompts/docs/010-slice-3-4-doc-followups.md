# Prompt: Docs 010 — Slice 3.4 doc follow-ups (archive, workshop § 6.1 + § 8 amendment, README/index refresh)

**Kind**: `tidy: docs` — drains the post-merge follow-ups slice 3.4 (#41) deferred; one PR. No new ADR, no new slice content; the lead artifact class is maintenance, so the commit subject is `tidy:` (per CLAUDE.md: new artifacts don't use `tidy:`, maintenance does).
**Files touched**: this prompt; `openspec/changes/slice-3-4-cart-abandonment/**` → `openspec/changes/archive/` (CLI move — slug only, the CLI stamps the dated prefix per retro 007's lesson); `openspec/specs/shopping-cart/spec.md` (folded by archive: 4 → 7 requirements, 1 modified); `docs/workshops/001-crittermart-event-model.md` (§ 6.1 slice 3.4 amendment + § 8 open question 1 resolution + Document History → v1.5); `README.md` (Orders BC-status row + Getting Started status note); `docs/prompts/README.md` + `docs/retrospectives/README.md` (Current population counts); `docs/narratives/README.md` (narrative 004 row → v1.7); `docs/skills/README.md` (DEBT.md exists now — move out of "Forthcoming companions"); `docs/retrospectives/docs/010-slice-3-4-doc-followups.md` (forthcoming)
**Mode**: solo tidy; no code changes
**Commit subject**: `tidy: docs — archive slice-3-4, workshop v1.5 amendment, README/index refresh`

## Framing

The slice 3.4 feat PR (#41) deliberately left its doc follow-ups out of scope (no opportunistic edits), named in retro 013's Outstanding section: archiving the shipped change (which folds the three ADDED requirements *and the MODIFIED add-item requirement* into the durable `shopping-cart` main spec), the Workshop 001 § 6.1 + § 8 amendment (recording the fire-and-check resolution **with the Wolverine factual correction**, the fat-event divergence, and the dissolution of the 3.2/3.3 refresh clauses — design.md faithfulness notes 1–5), and the README/index refresh. This session drains them as one `tidy: docs` PR.

This is the workshop amendment with the most teeth so far: it doesn't just record a divergence, it records that **§ 8 open question 1 contained a factual error** ("Wolverine supports both" — it does not; there is no scheduled-message cancellation API, so cancel-and-reschedule was never implementable). The model gets corrected by verified reality.

Ceremony: full pipeline ceremony (this prompt + retro 010 ride the PR) — this tidy authors *spec content* (the workshop amendment), which per the rule retros 007/008/009 settled (held three times, flagged overdue-to-encode) carries the prompt/retro pair.

Cadence note: this tidy banks the design-return credit after #41 (the 1st implementation PR of the current budget). Moot for round one — no slices remain — but the rule applies if round two opens.

## Goal

`openspec archive slice-3-4-cart-abandonment` folds the deltas into the durable `shopping-cart` main spec (4 → 7 requirements; the add-item requirement gains schedule-on-creation), leaving no active changes and `openspec validate --all --strict` green. Workshop 001 gains a § 6.1 slice 3.4 amendment note recording (1) the **fire-and-check** resolution of § 8 open question 1 *including the factual correction* (Wolverine has no scheduled-message cancellation API; "Wolverine supports both" was wrong; the GWT label "assume cancel-and-reschedule" described fire-and-check behavior all along), (2) the fat `CartAbandoned` divergence (`{ reason }` → `{ reason, lines, totalValue }`), (3) the dissolution of the 3.2/3.3 "refresh `CartActivityTimeout`" clauses (under fire-and-check, edit-event timestamps *are* the refresh — no code needed), and (4) the report's grouping key (UTC calendar day) and document shape as implementation decisions — plus a § 8 item 1 resolution pointer and a Document History v1.5 row. The root README's Orders BC row and Getting Started note reflect the Orders BC **complete** (round one's modeled implementation set is done); the prompts/retrospectives folder READMEs' population counts reflect reality (`docs/` 9 → 10, `implementations/` 12 → 13); the narratives README's 004 row reflects v1.7 / Moments 1–6 + 1A + 1B (journey fully authored); the skills README moves `DEBT.md` from "Forthcoming companions" to current.

## Spec delta

The workshop amendment **is** the spec delta: Workshop 001 § 6.1 (slice 3.4) gains the fire-and-check + factual-correction + fat-event + refresh-dissolution amendment note, § 8 open question 1 gains its resolution pointer, and Document History gains the v1.5 row. The openspec side is closure-only — the SHALL deltas were authored and `--strict`-validated in #41; this session folds them into the durable main spec via archive. The README/index edits are not spec-shaped.

## Orientation

1. **`docs/retrospectives/implementations/013-slice-3-4-cart-abandonment.md`** — the Outstanding section names exactly these follow-ups; the faithfulness notes to lift are in the change's design.md.
2. **`openspec/changes/slice-3-4-cart-abandonment/design.md`** — faithfulness notes 1–5 are the source text for the workshop amendment.
3. **`docs/workshops/001-crittermart-event-model.md`** § 6.1 slice 3.4 (lines ~309–324), § 8 open question 1 (line ~455), § 9 Document History — plus the v1.1/v1.4 amendment blockquotes as the in-file precedent (same blockquote style, placed after the slice's GWTs).
4. **`docs/prompts/docs/009-slices-3-2-3-3-doc-followups.md` + retro 009** — the direct precedent tidy this mirrors (same shape, prior slice).
5. **`openspec/specs/shopping-cart/spec.md`** — the durable main spec the archive folds into (4 requirements before this session).

## Out of scope

- **No code, no test changes. No new slice content. No new ADR.**
- **The encode/skills bundle** — `tidy: encode-ceremony-rule` (overdue 4×) + one-capability-per-aggregate encoding + skills DEBT row 1 drain. Confirmed with the user at session start: that is the **next session**, a separate PR, not bundled here.
- **The vision-level conversation** (frontend? talk storyboard? round two? CritterWatch?) — opens after this PR merges; round one's modeled implementation set is complete once this tidy lands.
- **`StockCommitted` / commit-on-confirmation** — still unmodelled by design; do not invent.
- **CritterWatch (ADR 013)** — stays deferred (tier/feed/license unresolved).
- **No `docs/skills/DEBT.md` content edits** — row 1 is drained by the encode/skills session, not here. (The skills *README* index edit is in scope; the DEBT row itself is not.)
