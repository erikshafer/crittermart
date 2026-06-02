# Retrospective: Docs 009 — Slices 3.2+3.3 doc follow-ups (archive, workshop v1.4 amendment, README/index refresh)

**Prompt**: `docs/prompts/docs/009-slices-3-2-3-3-doc-followups.md`
**Outcome**: shipped — all of retro 012's named post-merge follow-ups drained in one `tidy: docs` PR. `openspec archive slices-3-2-3-3-cart-item-edits` folded the deltas into the durable `shopping-cart` main spec (2 → 4 requirements; the add-item requirement's view contract updated to SKU-keyed lines); the CLI stamped the archive folder `2026-06-02-slices-3-2-3-3-cart-item-edits` (UTC date, slug named without a predicted prefix — retro 007's lesson held). Workshop 001 → **v1.4**: § 6.1 slices 3.2/3.3 amendment blockquote (merge-by-SKU line identity, `CartItemNotPresent` on quantity change, continued 3.4 deferral of the timeout-refresh clauses) + Document History row. Root README Orders BC row + Getting Started status note; prompts/retrospectives folder-README counts (`docs/` 8 → 9, `implementations/` 11 → 12); narratives README 004 row → v1.6. `openspec validate --all --strict` 4/4, no active changes.
**Tests**: not applicable — docs-only PR, no code or test changes (CI build/unit/integration jobs run unchanged).

## What worked

- **Retro 012's Outstanding section was file-shaped, and this session was assembled directly from it** — every item named a file and the content to put there (the design.md faithfulness notes were lift-ready as the workshop amendment's source text). Zero re-derivation. Retro 008's "file-shaped, not impression-shaped" lesson is now self-sustaining.
- **The archive's MODIFIED fold worked exactly as designed**: the durable `shopping-cart` spec's add-item requirement now carries the SKU-keyed-lines contract, and the merge-by-SKU scenario rides with it. The near-miss retro 012 recorded (ADDED-only would have left the durable spec stale) is confirmed closed by inspection of the folded spec.
- **The in-file amendment precedent (v1.1's blockquote style) made the workshop edit mechanical** — same blockquote shape, same placement (after the slice's GWTs), same closing cross-reference trail (durable spec → archived design.md → narrative → retro).

## What was harder / notable

- Nothing was hard. This is the third consecutive tidy following the same shape (007 → 008 → 009); it ran in a fraction of the prior sessions' time precisely because the shape is settled.
- **The ceremony rule has now held a third time** (spec-content tidy → full prompt/retro). Retro 008 recorded it as "ready to encode"; this retro escalates: **the encoding session is overdue** — a small `tidy: encode-ceremony-rule` PR lifting the rule into CLAUDE.md's operating disciplines (or `docs/rules/`), so future sessions stop re-deriving it from retro archaeology.

## Methodology refinements

- None new. The session was an exercise of three already-recorded refinements (file-shaped outstanding items, slug-only archive naming, the ceremony rule) — which is itself the data point: the methodology layer is stabilizing.

## Outstanding / next-session inputs

- **Slice 3.4 (cart abandonment) is the only remaining Orders BC slice** — the other Bruun temporal automation + the `CartAbandonmentReport` async projection teaser (ADR 008). Next implementation prompt = **013**. The OS-temp handoff (`crittermart-handoff-cart-slices.md`) carries its mirror table, the scheduling-policy fork (cancel-and-reschedule vs fire-and-check — Workshop § 8 open question 1, to be presented collaboratively), and the two fresh ctx7 verifications it needs (scheduled-message cancellation/supersession; async projection registration + rebuild under ADR 008's constraint).
- **Marten-pattern third use lands in 3.4**: `CartsAwaitingActivity*` will be the third instance-registered projection / `IEvent<T>` fold → that session decides the `docs/skills/` note or DEBT row (flagged by retros 011, 008, 012).
- **`tidy: encode-ceremony-rule`** — now overdue (held 3×). Candidate to bundle with the one-capability-per-aggregate convention encoding (also deferred, waiting on the full Orders shape — which 3.4 completes).
- **Design-return cadence**: this tidy banks the credit after #39. Budget resets — room for 2–3 implementation slices; 3.4 is the only one left in round one.
- **Carry-forward unchanged**: `StockCommitted` still unmodelled (do not invent); CritterWatch (ADR 013) still blocked on tier/feed/license.

## Spec-delta — landed?

**Yes.** The prompt named: Workshop 001 § 6.1 amendment (merge-by-SKU + `CartItemNotPresent` + 3.4 deferral) + Document History v1.4 as the spec delta, with the openspec archive as closure-only and the README edits as non-spec-shaped. All landed as named — no expansion, no shortfall. `openspec validate --all --strict`: 4/4 passed, zero active changes.
