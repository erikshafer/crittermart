# Prompt: Docs 007 — Slice 4.6 doc follow-ups (archive, workshop amendment, README/index refresh)

**Kind**: `tidy: docs` — drains the post-merge follow-ups slice 4.6 (#35) deferred; one PR. No new ADR, no new slice content; the lead artifact class is maintenance, so the commit subject is `tidy:` (per CLAUDE.md: new artifacts don't use `tidy:`, maintenance does).
**Files touched**: this prompt; `openspec/changes/slice-4-6-cancel-on-payment-decline/**` → `openspec/changes/archive/2026-05-31-slice-4-6-cancel-on-payment-decline/**` (CLI move); `openspec/specs/order-lifecycle/spec.md` + `openspec/specs/stock-management/spec.md` (folded by archive); `docs/workshops/001-crittermart-event-model.md` (§ 6 amendment + Document History → v1.2); `README.md` (BC-status rows, Getting Started status note); `docs/prompts/README.md` + `docs/retrospectives/README.md` (Current population counts); `docs/narratives/README.md` (narrative 004 row); `docs/retrospectives/docs/007-slice-4-6-doc-followups.md` (forthcoming)
**Mode**: solo tidy; no code changes
**Commit subject**: `tidy: docs — archive slice-4-6, workshop v1.2 amendment, README/index refresh`

## Framing

The slice 4.6 feat PR (#35) deliberately left its doc follow-ups out of scope (no opportunistic edits), named in retro 010's Outstanding section: archiving the shipped change (which folds the `order-lifecycle` and `stock-management` deltas into the durable main specs), the Workshop 001 amendment recording the `ReleaseStock`-vs-`OrderCancelled` message-shape divergence (promised by the change's design.md Decision 1 faithfulness note: "The Workshop is amended via this slice's Document History on archive"), and the README/index refresh. This session drains them as one `tidy: docs` PR.

Two ceremony forks were resolved with the user before this prompt was frozen: (1) full pipeline ceremony — this prompt + retro 007 ride the PR (the #28/#30/#32 precedent, not the lighter #34 precedent), because the session authors spec content (the workshop amendment), not just file moves; (2) the index refresh covers the named root-README items **plus** the stale folder-README population counts (prompts/, retrospectives/, narratives/), since this session's own prompt/retro change those counts anyway.

Cadence note: this is a design-return-shaped tidy after the 1st implementation PR (#35) of the current budget. It banks no urgency — the budget allows 1–2 more implementation slices (4.7 next) before a mandatory interleave — but keeping the archive/amendment loop tight to the slice that created it is the established rhythm.

## Goal

`openspec archive slice-4-6-cancel-on-payment-decline` folds the two ADDED requirements into the durable main specs (`order-lifecycle`: cancel on payment decline; `stock-management`: release reserved stock on cancellation), leaving no active changes and `openspec validate --all --strict` green. Workshop 001 gains a § 6 amendment note at slices 2.3/4.6 recording the deliberate message-shape divergence (cross-BC release rides a `ReleaseStock { orderId, lines }` published-language command per ADR 014, not the workshop's literal `OrderCancelled { orderId }` event) and a Document History v1.2 row. The root README's Inventory/Orders BC rows and Getting Started status note reflect 4.3/4.4/4.6/2.3 shipped; the prompts/retrospectives folder READMEs' population counts reflect reality (docs 6→7, implementations 6→10 plus this session); the narratives README's 004 row reflects v1.4 / Moments 1–5.

## Spec delta

The workshop amendment **is** the spec delta: Workshop 001 § 6 (slices 2.3 and 4.6) gains the divergence note + Document History v1.2. The openspec side is closure-only — the SHALL deltas were authored and `--strict`-validated in #35; this session folds them into the durable main specs via archive. The README/index edits are not spec-shaped.

## Orientation

1. **`docs/retrospectives/implementations/010-slice-4-6-cancel-on-payment-decline.md`** — the Outstanding section names exactly these follow-ups.
2. **`openspec/changes/slice-4-6-cancel-on-payment-decline/design.md`** Decision 1 — the faithfulness note whose workshop-amendment promise this session keeps.
3. **`docs/workshops/001-crittermart-event-model.md`** § 6 (slices 2.3, 4.6) + § 9 Document History — the v1.1 amendment (slice 3.1, PR #28) is the in-file precedent for amendment style.
4. **`docs/prompts/docs/006-slice-4-2-doc-followups.md` + retro** — the precedent design-return this mirrors (archive + README pattern).

## Out of scope

- **No code, no test changes. No new slice content. No new ADR** (the divergence rationale already lives in ADR 014 + the change's design.md; the workshop amendment cross-references, it does not re-derive).
- **Slice 4.7** (cancel on payment timeout) — the next implementation session, separately.
- **`StockCommitted` / commit-on-confirmation** — still unmodelled by design; do not invent.
- **CritterWatch (ADR 013)** — stays deferred (tier/feed/license unresolved).
- **No `docs/skills/` or DEBT edits** — none surfaced by 4.6.
