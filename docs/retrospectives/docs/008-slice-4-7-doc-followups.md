# Retrospective: Docs 008 — Slice 4.7 doc follow-ups (archive, workshop § 4.7 amendment, README/index refresh)

**Prompt**: `docs/prompts/docs/008-slice-4-7-doc-followups.md`
**Outcome**: shipped — slice 4.7's post-merge follow-ups (named in retro 011's Outstanding section) drained in one `tidy: docs` PR. Full pipeline ceremony per the rule retro 007 settled (this tidy authors spec content — the workshop amendment); **the rule has now held for two consecutive tidies and is ready to encode.**
**Validation**: `openspec validate --all --strict` → 4 passed (order-lifecycle, product-catalog, shopping-cart, stock-management); no active changes remain.

## What shipped

- **Archived `slice-4-7-cancel-on-payment-timeout`** → `openspec/changes/archive/2026-06-01-slice-4-7-cancel-on-payment-timeout/`. The CLI folded both deltas into the durable `order-lifecycle` main spec: *Cancel an order on payment timeout* + *Track orders awaiting payment* — **6 → 8 requirements**. No active changes remain.
- **Workshop 001 → v1.3.** Amendment blockquote at § 6 slice 4.7 with two notes: (1) the v1.2 `ReleaseStock`-vs-`OrderCancelled`-event message-shape note extended to § 4.7's scenarios (they were authored before the v1.2 divergence was decided and still carried the literal wording); (2) the as-shipped release is **unconditional** — strictly stronger than the happy path's literal wording, and exactly what realizes the delayed-grant failure path. Document History v1.3 row records that **the Order lifecycle (4.1–4.7) is complete for round one**. Slice table (§ 5) left at model-level intent, per precedent.
- **Root README refresh.** Orders BC row → "Order lifecycle complete" with all five 4.x slices named + the `OrdersAwaitingPayment` Bruun todo-list; Getting Started status note → only cart edits/abandonment (3.2–3.4) remain.
- **Folder-README index refresh.** `docs/prompts/README.md` + `docs/retrospectives/README.md` population counts: `docs/` 7 → 8, `implementations/` 10 → 11. `docs/narratives/README.md` 004 row → v1.5, Moments 1–6, slice 4.7 added.

## What worked

- **The retro-named follow-up list was the whole work order — fourth time running.** Retro 011's Outstanding section named the archive, the § 4.7 amendment (including the precise wording gap), and the README refresh. The spec-delta closure loop continues to pay for itself; nothing was rediscovered.
- **The § 4.7 wording gap was caught by the implementing session, not this one.** Retro 011 noticed during implementation that § 4.7's GWT still carried the pre-4.6 message wording — so this tidy's amendment was specified before it was needed. The slice → retro → tidy relay handed over cleanly.
- **Naming the change slug only (no dated archive prefix) in the prompt** — retro 007's lesson — meant the prompt's Files-touched line stayed accurate when the CLI stamped `2026-06-01-…`.

## What was harder / notable

- **Retro 011 overstated one follow-up: "README test counts 42 → 51."** No README carries test counts (verified by grep before editing); the actual index work was BC rows + population counts. Harmless, but a reminder that retro Outstanding items should name *file + line-shaped* targets, not remembered impressions — the same discipline prompts use for Files-touched.
- **The § 4.7 amendment needed a second clause the v1.2 precedent didn't have.** The v1.2 amendment was purely message-shape; § 4.7's also had to record a *behavioral strengthening* (unconditional release vs. the happy path's implied conditional). Distinguishing "diverged" (shape) from "strengthened" (behavior) in the same blockquote keeps the workshop honest about which kind of drift each is.

## Methodology refinements

- **The ceremony rule has held twice and is ready to encode**: *a tidy that authors spec content (workshop/narrative amendment, spec Purpose) carries the full prompt/retro pair; a purely mechanical tidy (file moves, counts) may run light.* Retro 007 set the bar at "holds once more"; this session met it. Encoding it is a small `tidy: encode-` session (CLAUDE.md § Operating Disciplines or `docs/rules/`) — deliberately **not** done here (no opportunistic edits).
- **Retro Outstanding items should be file-shaped, not impression-shaped.** The "test counts" phantom item cost a verification grep. Cheap this time; the discipline is worth naming.

## Outstanding / next-session inputs

- **The Cart side is all that remains of the Orders BC**: 3.2 (remove item), 3.3 (change quantity), 3.4 (abandon cart on inactivity — the *other* Bruun temporal automation, which can mirror 4.7's schedule/guard/todo-list shape almost mechanically: `CartActivityTimeout` ↔ `OrderPaymentTimeout`, `CartsAwaitingActivity*` ↔ `OrdersAwaitingPayment*`, with the added wrinkle of *refresh-on-activity* and the `CartAbandonmentReport` **async projection teaser**, ADR 008). A handoff prompt for this work is authored after this PR (user-directed).
- **Encode the ceremony rule** — a future `tidy: encode-` session; the rule text is in this retro's Methodology refinements.
- **`StockCommitted` / commit-on-confirmation** — still unmodelled by design (Workshop § 8 future-ADR candidate).
- **CritterWatch (ADR 013)** — still deferred; blocked on the tier/feed/license question. Both live broker traffic and scheduled-message activity now exist for it to monitor.
- **Two new Marten patterns from 4.7** (instance-registered projections, `IEvent<T>` metadata folds) — skill-note candidates if a third use appears; 3.4's todo-list projection will likely be that third use.
- **Design-return cadence**: this tidy banks the design-return credit after #37 (the 1st implementation PR of the budget). Budget resets — room for 2–3 implementation slices (the 3.x cart slices) before the next mandatory interleave.

## Spec-delta — landed?

**Yes.** The named delta — Workshop 001's § 6 slice 4.7 amendment (message-shape extension + unconditional-release strengthening) + Document History v1.3 — landed. The openspec side was closure-only as named: both `--strict`-validated `order-lifecycle` deltas from #37 folded into the durable main spec via archive (6 → 8 requirements); no active changes remain; `validate --all --strict` green. The README/index edits were not spec-shaped and none were promoted into spec content.
