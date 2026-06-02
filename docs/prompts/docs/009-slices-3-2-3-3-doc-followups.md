# Prompt: Docs 009 — Slices 3.2+3.3 doc follow-ups (archive, workshop § 6.1 amendment, README/index refresh)

**Kind**: `tidy: docs` — drains the post-merge follow-ups slices 3.2+3.3 (#39) deferred; one PR. No new ADR, no new slice content; the lead artifact class is maintenance, so the commit subject is `tidy:` (per CLAUDE.md: new artifacts don't use `tidy:`, maintenance does).
**Files touched**: this prompt; `openspec/changes/slices-3-2-3-3-cart-item-edits/**` → `openspec/changes/archive/` (CLI move — slug only, the CLI stamps the dated prefix per retro 007's lesson); `openspec/specs/shopping-cart/spec.md` (folded by archive: 2 → 4 requirements, 1 modified); `docs/workshops/001-crittermart-event-model.md` (§ 6.1 slices 3.2/3.3 amendment + Document History → v1.4); `README.md` (Orders BC-status row); `docs/prompts/README.md` + `docs/retrospectives/README.md` (Current population counts); `docs/narratives/README.md` (narrative 004 row → v1.6); `docs/retrospectives/docs/009-slices-3-2-3-3-doc-followups.md` (forthcoming)
**Mode**: solo tidy; no code changes
**Commit subject**: `tidy: docs — archive slices-3-2-3-3, workshop v1.4 amendment, README/index refresh`

## Framing

The slices 3.2+3.3 feat PR (#39) deliberately left its doc follow-ups out of scope (no opportunistic edits), named in retro 012's Outstanding section: archiving the shipped change (which folds the two ADDED requirements *and the MODIFIED add-item requirement* into the durable `shopping-cart` main spec), the Workshop 001 § 6.1 amendment (recording the merge-by-SKU line-identity resolution and the `CartItemNotPresent`-on-quantity-change extension — design.md faithfulness notes 1–2), and the README/index refresh. This session drains them as one `tidy: docs` PR.

Ceremony: full pipeline ceremony (this prompt + retro 009 ride the PR) — this tidy authors *spec content* (the workshop amendment), which per the rule retros 007/008 settled (held twice, recorded as encodable) carries the prompt/retro pair.

Cadence note: this tidy banks the design-return credit after #39 (the 1st implementation PR of the current budget). Keeping the archive/amendment loop tight to the slice that created it is the established rhythm.

## Goal

`openspec archive slices-3-2-3-3-cart-item-edits` folds the deltas into the durable `shopping-cart` main spec (2 → 4 requirements; the add-item requirement's view contract updated to SKU-keyed lines), leaving no active changes and `openspec validate --all --strict` green. Workshop 001 gains a § 6.1 slices 3.2/3.3 amendment note recording (1) the merge-by-SKU resolution of 3.1's deferred line-identity question, (2) the `CartItemNotPresent` guard on quantity changes (an extension beyond the workshop's non-positive-quantity failure path), and (3) the continued deferral of the `CartActivityTimeout` refresh clauses to 3.4 — plus a Document History v1.4 row. The root README's Orders BC row reflects cart edits shipped (only 3.4 remains); the prompts/retrospectives folder READMEs' population counts reflect reality (`docs/` 8 → 9, `implementations/` 11 → 12); the narratives README's 004 row reflects v1.6 / Moments 1–6 + 1A.

## Spec delta

The workshop amendment **is** the spec delta: Workshop 001 § 6.1 (slices 3.2/3.3) gains the merge-by-SKU + `CartItemNotPresent` amendment note + Document History v1.4. The openspec side is closure-only — the SHALL deltas were authored and `--strict`-validated in #39; this session folds them into the durable main spec via archive. The README/index edits are not spec-shaped.

## Orientation

1. **`docs/retrospectives/implementations/012-slices-3-2-3-3-cart-item-edits.md`** — the Outstanding section names exactly these follow-ups, including the two faithfulness notes to lift into the workshop.
2. **`docs/workshops/001-crittermart-event-model.md`** § 6.1 slices 3.2/3.3 (lines ~283–305) + the v1.1 amendment blockquote at § 6.1 slice 3.1 (the in-file precedent this extends — same blockquote style, placed after the slice's GWTs) + § 9 Document History.
3. **`docs/prompts/docs/008-slice-4-7-doc-followups.md` + retro 008** — the direct precedent tidy this mirrors (same shape, prior slice).
4. **`openspec/changes/slices-3-2-3-3-cart-item-edits/design.md`** — faithfulness notes 1–3 are the source text for the workshop amendment.
5. **`openspec/specs/shopping-cart/spec.md`** — the durable main spec the archive folds into (2 requirements before this session).

## Out of scope

- **No code, no test changes. No new slice content. No new ADR.**
- **Slice 3.4 (cart abandonment)** — the next implementation session, separately; its prompt (013) is authored in that session, not here. The memory/handoff updates for tomorrow's pickup ride *outside* this PR (memory is not a repo artifact).
- **`StockCommitted` / commit-on-confirmation** — still unmodelled by design; do not invent.
- **CritterWatch (ADR 013)** — stays deferred (tier/feed/license unresolved).
- **No `docs/skills/` or DEBT edits** — the Marten-pattern third use lands with 3.4's projection, not here.
- **No encoding of the ceremony rule into CLAUDE.md** — it held a third time this session; the encoding remains a separate `tidy: encode-` session.
