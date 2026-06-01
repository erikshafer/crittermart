# Retrospective: Docs 007 — Slice 4.6 doc follow-ups (archive, workshop amendment, README/index refresh)

**Prompt**: `docs/prompts/docs/007-slice-4-6-doc-followups.md`
**Outcome**: shipped — slice 4.6's post-merge follow-ups (named in retro 010's Outstanding section) drained in one `tidy: docs` PR. Two ceremony forks were resolved with the user before the prompt was frozen: full pipeline ceremony (this prompt/retro pair rides the PR, the #28/#30/#32 precedent over the lighter #34 one) and an index-refresh scope that covers the stale folder-README population counts alongside the named root-README rows.
**Validation**: `openspec validate --all --strict` → 4 passed (order-lifecycle, product-catalog, shopping-cart, stock-management); no active changes remain.

## What shipped

- **Archived `slice-4-6-cancel-on-payment-decline`** → `openspec/changes/archive/2026-06-01-slice-4-6-cancel-on-payment-decline/`. The CLI folded both deltas into the durable main specs: `order-lifecycle` `+1 added` (cancel an order when payment is declined) and `stock-management` `+1 added` (release reserved stock on cancellation). `order-lifecycle` now carries 6 requirements; `stock-management` 4.
- **Workshop 001 → v1.2.** Amendment blockquotes at § 6 slice 2.3 and slice 4.6, recording the message-shape divergence: the cross-BC release rides a `ReleaseStock { orderId, lines }` published-language command (`CritterMart.Contracts`, ADR 014), not the workshop's literal `OrderCancelled { orderId }` event. Behavior honored exactly; only the contract name/shape differs (the same divergence kind 4.2 made for `ReserveStock`). Document History row appended; slice table (§ 5) left at model-level intent intentionally, per the v1.1 precedent. This keeps the promise the change's design.md Decision 1 made ("the Workshop is amended via this slice's Document History on archive").
- **Root README refresh.** Inventory BC row gains 2.3 (release on cancellation, per-SKU idempotent); Orders BC row moves 4.6 from forthcoming to shipped; the Getting Started status note now reads 1.1–1.3 / 2.1–2.3 / 3.1 / 4.1–4.6 shipped with 4.7 + 3.2–3.4 remaining.
- **Folder-README index refresh.** `docs/prompts/README.md` + `docs/retrospectives/README.md` Current population counts: `docs/` 3 → 7, `implementations/` 6 → 10. `docs/narratives/README.md` narrative-004 row: v1.4, Moments 1–5, slices 3.1 + 4.1–4.6 covered.

## What worked

- **The retro-named follow-up list was the whole work order — third time running.** Retro 010's Outstanding section named the archive, the workshop amendment, and the README refresh; nothing had to be rediscovered. The spec-delta closure loop continues to pay for itself.
- **The amendment cross-references stayed date-free.** The workshop amendment cites the archived change as `slice-4-6-cancel-on-payment-decline/design.md` (no archive-date prefix), avoiding the brittle full-path issue retro 006 flagged when ADR 014 hard-coded a dated `archive/2026-05-31-…` path.
- **Asking the ceremony question up front cost one exchange and removed all downstream ambiguity.** The repo had genuinely split precedent (#28/#30/#32 with prompt/retro vs. #34 without); resolving it before any edit meant no rework and produced a rule worth keeping (below).

## What was harder / notable

- **The openspec CLI stamped the archive with the UTC date (`2026-06-01-…`), not the local date (`2026-05-31-…`) the prompt predicted.** Harmless — the CLI owns the naming — but the prompt's Files-touched line is off by one day. Prior archives (e.g., `2026-05-31-slice-4-3-…`) happened to land when local and UTC agreed. Don't predict the dated prefix in future prompts; name the change slug only.
- **The folder-README counts were already stale *before* this session** (prompts/retros said `docs/` 3 and `implementations/` 6; reality was 6 and 9). The staleness traces to PR #34 — the one tidy that skipped both the ceremony and the index refresh. One light tidy created debt three sessions carried.

## Methodology refinements

- **Ceremony rule, settled with the user:** a tidy that authors *spec content* (a workshop/narrative amendment, a spec Purpose) carries the full prompt/retro pair; only a tidy that is *purely mechanical* (file moves, counts) may run #34-light. This session's workshop amendment made it the former. Encode-candidate if it holds for one more tidy.
- **Index refreshes belong to the session that changes the indexed reality.** Population counts drifted because implementation sessions (correctly, per no-opportunistic-edits) don't touch folder READMEs, and the one tidy that should have caught up skipped it. The fix: every `tidy: docs` session refreshes the population counts as a standing line item, not an optional one.

## Outstanding / next-session inputs

- **Slice 4.7 — cancel on payment timeout.** The next implementation session (user-directed: a separate session). The only remaining order-cancellation path; needs Wolverine scheduling (`OrderPaymentTimeout`), the `OrdersAwaitingPayment*` inline projection, a terminal guard, and reuses 4.6's `ReleaseStock`/`ReleaseStockHandler` unchanged. No prompt exists yet — authoring `implementations/011` is that session's first step.
- **`StockCommitted` / commit-on-confirmation** — still unmodelled by design (Workshop § 8 future-ADR candidate); not invented here, must not be invented in 4.7.
- **CritterWatch (ADR 013)** — still deferred; blocked on the tier/feed/license question.
- **Design-return cadence**: this tidy banks the design-return credit after #35 (the 1st implementation PR of the budget). Budget resets — room for 2–3 implementation slices (4.7, then the 3.x cart slices) before the next mandatory interleave.

## Spec-delta — landed?

**Yes.** The named delta — Workshop 001's § 6 amendment (slices 2.3 + 4.6: `ReleaseStock` message-shape divergence) + Document History v1.2 — landed. The openspec side was closure-only as named: both `--strict`-validated deltas from #35 folded into the durable `order-lifecycle` and `stock-management` main specs via archive; no active changes remain; `validate --all --strict` green. The README/index edits were not spec-shaped and none were promoted into spec content.
