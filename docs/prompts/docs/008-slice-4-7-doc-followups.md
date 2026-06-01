# Prompt: Docs 008 — Slice 4.7 doc follow-ups (archive, workshop § 4.7 amendment, README/index refresh)

**Kind**: `tidy: docs` — drains the post-merge follow-ups slice 4.7 (#37) deferred; one PR. No new ADR, no new slice content; the lead artifact class is maintenance, so the commit subject is `tidy:` (per CLAUDE.md: new artifacts don't use `tidy:`, maintenance does).
**Files touched**: this prompt; `openspec/changes/slice-4-7-cancel-on-payment-timeout/**` → `openspec/changes/archive/` (CLI move — slug only, the CLI stamps the dated prefix per retro 007's lesson); `openspec/specs/order-lifecycle/spec.md` (folded by archive: 6 → 8 requirements); `docs/workshops/001-crittermart-event-model.md` (§ 6 slice 4.7 amendment + Document History → v1.3); `README.md` (Orders BC-status row, Getting Started status note); `docs/prompts/README.md` + `docs/retrospectives/README.md` (Current population counts); `docs/narratives/README.md` (narrative 004 row → v1.5); `docs/retrospectives/docs/008-slice-4-7-doc-followups.md` (forthcoming)
**Mode**: solo tidy; no code changes
**Commit subject**: `tidy: docs — archive slice-4-7, workshop v1.3 amendment, README/index refresh`

## Framing

The slice 4.7 feat PR (#37) deliberately left its doc follow-ups out of scope (no opportunistic edits), named in retro 011's Outstanding section: archiving the shipped change (which folds the two `order-lifecycle` ADDED requirements into the durable main spec), the Workshop 001 § 4.7 amendment (extending the v1.2 `ReleaseStock`-divergence note to § 4.7's own GWT scenarios — § 2.3/§ 4.6 were amended in v1.2, but § 4.7's clauses were still future then and kept the literal `OrderCancelled { orderId }` wording), and the README/index refresh. This session drains them as one `tidy: docs` PR.

Ceremony: full pipeline ceremony (this prompt + retro 008 ride the PR), per the rule retro 007 settled with the user — a tidy that authors *spec content* (here: the workshop amendment) carries the prompt/retro pair; only a purely mechanical tidy may run light. **This is the second consecutive tidy where that rule holds — retro 008 should record it as encodable.**

Cadence note: this tidy banks the design-return credit after #37 (the 1st implementation PR of the current budget). Keeping the archive/amendment loop tight to the slice that created it is the established rhythm.

## Goal

`openspec archive slice-4-7-cancel-on-payment-timeout` folds the two ADDED requirements into the durable `order-lifecycle` main spec (cancel an order on payment timeout; track orders awaiting payment — 6 → 8 requirements), leaving no active changes and `openspec validate --all --strict` green. Workshop 001 gains a § 6 slice 4.7 amendment note recording that the cross-BC release rides the `ReleaseStock` published-language command (per the v1.2 amendment's rationale, extended to § 4.7's scenarios) and that the projection/schedule clauses shipped as modeled, plus a Document History v1.3 row. The root README's Orders BC row and Getting Started status note reflect the **complete order lifecycle** (4.1–4.7 shipped; only cart edits/abandonment remain); the prompts/retrospectives folder READMEs' population counts reflect reality (`docs/` 7 → 8, `implementations/` 10 → 11); the narratives README's 004 row reflects v1.5 / Moments 1–6.

## Spec delta

The workshop amendment **is** the spec delta: Workshop 001 § 6 (slice 4.7) gains the divergence-extension note + Document History v1.3. The openspec side is closure-only — the SHALL deltas were authored and `--strict`-validated in #37; this session folds them into the durable main spec via archive. The README/index edits are not spec-shaped.

## Orientation

1. **`docs/retrospectives/implementations/011-slice-4-7-cancel-on-payment-timeout.md`** — the Outstanding section names exactly these follow-ups, including the § 4.7 wording gap.
2. **`docs/workshops/001-crittermart-event-model.md`** § 6 slice 4.7 (the GWT scenarios still reading "Orders publishes `OrderCancelled { orderId }`") + the v1.2 amendment blockquotes at § 2.3/§ 4.6 (the in-file precedent this extends) + § 9 Document History.
3. **`docs/prompts/docs/007-slice-4-6-doc-followups.md` + retro 007** — the direct precedent tidy this mirrors (same shape, prior slice).
4. **`openspec/specs/order-lifecycle/spec.md`** — the durable main spec the archive folds into (6 requirements before this session).

## Out of scope

- **No code, no test changes. No new slice content. No new ADR.**
- **The Cart-side slices (3.2/3.3/3.4)** — the next implementation session(s), separately; a handoff prompt for them is authored after this PR (user-directed), not inside it.
- **`StockCommitted` / commit-on-confirmation** — still unmodelled by design; do not invent.
- **CritterWatch (ADR 013)** — stays deferred (tier/feed/license unresolved).
- **No `docs/skills/` or DEBT edits** — retro 011 flagged two new Marten patterns (instance-registered projections, `IEvent<T>` folds) as skill-note candidates *if a third use appears*; that bar is not met.
- **No encoding of the ceremony rule into CLAUDE.md** — retro 008 records that it held twice and is ready; the encoding itself is a separate (likely `tidy: encode-`) session per the no-opportunistic-edits discipline.
