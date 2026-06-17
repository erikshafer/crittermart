---
retrospective: 015
kind: docs
prompt: docs/prompts/docs/015-design-return-archive-active-changes.md
deliverable: docs/workshops/001-crittermart-event-model.md (§ 6 slice 3.1 v1.13 amendment), openspec/changes/archive/2026-06-17-harden-add-to-cart-snapshot/ + openspec/changes/archive/2026-06-17-list-my-orders/ (both archived; shopping-cart 8→9, order-lifecycle 9→10), docs/prompts/README.md + docs/retrospectives/README.md (docs count 14→15)
date: 2026-06-17
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 015: Design-Return — Workshop § 6 Slice 3.1 Faithfulness Note + Archive Two Active Changes

## Outcome summary

The **design-return cadence interleave** owed after three implementations since the #68 interleave (#69 `AddToCart` hardening, #70 OTel teaching pass, #71 the "My Orders" list). One PR drains the two reverse-spec-deltas the implementation runs had honestly fenced and returns the OpenSpec workspace to a clean baseline.

- **Workshop 001 § 6 slice 3.1 — v1.13 amendment** recording the #69 malformed-snapshot rejection: an `AddToCart` with no usable product snapshot (absent / blank name / negative price) is rejected with `400` at the boundary, before any `Cart` stream starts — a **failure path** beyond slice 3.1's two modeled happy paths, distinct from the cart's domain-state rejections (`CartItemNotPresent`, `NoOpenCart`). Append-only after the frozen GWTs + the existing v1.1 block.
- **`openspec archive harden-add-to-cart-snapshot`** — folded its ADDED requirement (*Reject an add-to-cart command with no usable product snapshot*) into `openspec/specs/shopping-cart/spec.md` (**8 → 9**) and moved the change to `archive/2026-06-17-harden-add-to-cart-snapshot/`.
- **`openspec archive list-my-orders`** — folded its ADDED requirement (*List a customer's own orders*) into `openspec/specs/order-lifecycle/spec.md` (**9 → 10**) and moved the change to `archive/2026-06-17-list-my-orders/`.
- **Index READMEs** — `docs/` count 14 → 15 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, population note extended.

**Verification**: `openspec list` → **No active changes found**; `openspec list --specs` → shopping-cart **9**, order-lifecycle **10** (product-catalog 4, stock-management 5 unchanged); both new requirement titles confirmed present in their main specs. No code, no tests, no live boot (a docs/spec tidy).

## What worked

- **Bundling the two archives + the fenced note into one cadence interleave was the natural shape.** Both implementation runs (#69, #71) had explicitly fenced their archive + workshop note as post-merge tidies; the cadence made the interleave *due* exactly when those tidies were ripe. One PR closed all three threads and returned the workspace to 0 active changes — a cleaner baseline than draining them one at a time.
- **`openspec archive` did the spec sync deterministically.** Letting the CLI fold each delta into the main spec (and move the change to `archive/`) is the tool-backed path — no hand-editing of `openspec/specs/*`, no risk of a divergent manual merge. The `-y` flag cleared the non-interactive-shell prompt; the warning that `list-my-orders` had "1 incomplete task" was correct and intended (the pagination non-goal, left unchecked).
- **Honest task-ticking before archiving.** Marking the genuinely-shipped tasks complete (and leaving only the real non-goal unchecked) means the archived `tasks.md` records reflect reality, not a frozen-at-authoring snapshot.

## What was harder / notable

- **"§ 6.1" was shorthand, not a literal heading.** The fenced note (retro 026 task 5.2) called it "workshop § 6.1 slice 3.1" — but § 6 is "GWT Scenarios" and slices are numbered by their slice number (3.1), not "6.1". The note lives under `### 3.1 Add item to cart` within § 6. Worth recording so a future reader doesn't hunt for a non-existent § 6.1 heading.
- **The workshop doc-version counter is global and monotonic, not per-section.** Amendments are tagged with the doc version at which they were added (v1.1, v1.4 in § 6; v1.10–v1.12 in § 5.1), so the next amendment anywhere is v1.13 — even though it lands in § 6, after the § 5.1 v1.12 added in #71. The version is the timeline position, not a section-local sequence.

## Methodology refinements

- **The "narrative leads, workshop lags by a tidy" pattern is now a settled three-time rhythm.** docs/013 (W3/W4 reconcile), docs/014 (§ 5.1 enrich flip), and now docs/015 (§ 6 slice-3.1 hardening) all follow it: the implementation slice binds the change in its narrative within its own PR, and the workshop catches up in the next design-return tidy. The workshop is the modeling-time record; the narrative + main spec are the shipped-truth record; the tidy reconciles them. This is the cadence working as designed, not drift.
- **An interleave can drain accumulated fenced tidies, not just author one amendment.** Prior interleaves did one workshop flip + one archive. This one did one amendment + *two* archives because two implementation runs had each fenced a post-merge sync. Bundling them is correct (they share the design-return theme and the tidy-ceremony level) and keeps the active-change count from drifting upward across implementation runs.

## Outstanding / next-session inputs

- **Cadence reset — the counter is clear.** This interleave resets it; the **next PR is an open implementation pick**. Candidates (all non-blocking): the **cart identity-transport harmonization** (4 cart commands, 3 transports — #71 reinforced the header-keyed direction for reads); product *detail* (Gap #2, currently list-rendered by design); the still-owed **OTel / in-browser visual pass** (the recurring verification debt — CLI/wire-verified to date, never a real browser render); list **pagination** (a named non-goal, only if the order count grows).
- **OpenSpec workspace is at 0 active changes** — a clean baseline for the next proposal. 19 changes now archived under `openspec/changes/archive/`.
- **Carry-forwards (unchanged, non-blocking):** no frontend CI job; the flaky `PaymentAuthorizationTests` Wolverine-shutdown race (`gh run rerun --failed`); NU1507 multi-source warning + `global.json` SDK pin drift; focus-ring shadcn enhancement; Docker container grouping; suppressed MessagePack CVE; **CritterWatch trial expires 2026-07-10**.

## Spec-delta — landed?

**Named delta landed.** The prompt named: a workshop **§ 6 slice-3.1 v1.13 amendment** (the #69 malformed-snapshot failure path) + the **`shopping-cart` 8→9** and **`order-lifecycle` 9→10** main-spec syncs via `openspec archive`. All landed: the workshop carries the v1.13 block; `openspec list --specs` confirms shopping-cart 9 + order-lifecycle 10; `openspec list` confirms **No active changes**; both archived changes sit under `openspec/changes/archive/2026-06-17-*`. This is a **design-return reconciliation** that closes the retro-026-fenced § 6 note and the retro-028-named `list-my-orders` archive — four-step closure for both: **prompt named → session executed → this retro confirms → the workshop + the main specs recorded.** This tidy authored spec content (the workshop amendment), so it carried the full prompt/retro pair per the tidy-ceremony rule.
