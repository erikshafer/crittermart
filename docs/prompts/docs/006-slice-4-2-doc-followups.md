# Prompt: Docs 006 — Slice 4.2 doc follow-ups (ADR 014 published-language, archive, spec Purpose, README)

**Kind**: design-return — drains the doc/ADR follow-ups slice 4.2 (#31) deferred; one PR. Lead artifact is a new ADR, so the commit subject is `docs:`, not `tidy:` (per CLAUDE.md: new artifacts do not use `tidy:`).
**Files touched**: this prompt; `docs/decisions/014-published-language-contracts-project.md` (new ADR); `docs/decisions/README.md` (index row); `docs/rules/structural-constraints.md` (paired note + references + version bump + Document History); `openspec/changes/slice-4-2-reserve-stock/**` → `openspec/changes/archive/2026-05-31-slice-4-2-reserve-stock/**` (CLI move); `openspec/specs/order-lifecycle/spec.md` + `openspec/specs/stock-management/spec.md` (folded by archive); `openspec/specs/stock-management/spec.md` `## Purpose` (fill the lingering slice-2.1 TBD); `README.md` (BC-status rows, repo-structure `src/` note); `docs/retrospectives/docs/006-slice-4-2-doc-followups.md` (forthcoming)
**Mode**: solo design-return; no code changes
**Commit subject**: `docs: ADR 014 published-language Contracts (+ archive 4.2, spec Purpose, README)`

## Framing

The slice 4.2 feat PR (#31) deliberately left four follow-ups out of scope (no opportunistic edits), named in retro 008: the ADR for the published-language `CritterMart.Contracts` project (+ paired structural-constraints note), archiving the shipped change, filling the `stock-management` spec's `## Purpose` TBD (lingering from slice 2.1), and refreshing stale README rows. This session drains them as one design-return PR. The user resolved the packaging fork: one combined PR under a `docs:` subject (the ADR is the lead artifact; the `ADR + paired rule note in the same PR` constraint and the `ADRs don't use tidy:` convention are both honored). CritterWatch (ADR 013) remains deferred — blocked on the unresolved tier/feed/license question.

This is the design-return after the **2nd** Orders implementation PR (4.1 #29, 4.2 #31), satisfying the design-return cadence before slice 4.3.

## Goal

ADR 014 records the published-language-via-shared-project decision (rationale already in slice-4-2 `design.md` decision 4), paired with a `structural-constraints.md` note and version bump. `openspec archive slice-4-2-reserve-stock` folds the `order-lifecycle` (2 ADDED) and `stock-management` (1 MODIFIED + 1 ADDED) deltas into the durable main specs. The `stock-management` main spec gets a meaningful `## Purpose` (replacing the slice-2.1 archive's `TBD`). The README's Inventory/Orders BC-status rows and `src/` repo-structure note reflect 4.2 (cross-BC delivery shipped; `CritterMart.Contracts` + `CritterMart.CrossBc.Tests` exist). `openspec validate --all` stays green.

## Spec delta

A new ADR (014) lands and the structural-constraints rule file gains a paired published-language note — this is the session's spec-shaped delta. The openspec deltas are closed into durable main specs via archive (no new SHALL content authored here; the requirement stanzas were written and `--strict`-validated in #31). The `## Purpose` text is authored, not generated. No narrative/workshop amendment needed — 4.2's Narrative 004 → v1.2 bump and the design.md decision-2 workshop-faithfulness note both landed in #31.

## Orientation

1. **`docs/retrospectives/implementations/008-slice-4-2-reserve-stock.md`** — the "Outstanding / next-session inputs" section names exactly these follow-ups.
2. **`openspec/changes/slice-4-2-reserve-stock/design.md`** decision 4 — the parked rationale ADR 014 lifts into a durable cross-change record.
3. **`docs/prompts/docs/005-slice-4-1-doc-followups.md` + retro** — the precedent tidy this mirrors (archive + README + Purpose pattern).
4. **`docs/decisions/011-…md`** — house ADR format (Context / Decision / Consequences, terse prose, rejected alternatives folded in).
5. **`docs/context-map/README.md`** — the Orders↔Inventory Customer-Supplier relationship the published language serves.

## Out of scope

- **No code, no test changes.** **No new slice content.**
- **CritterWatch (ADR 013)** — stays deferred; not this session.
- **No `docs/skills/` or DEBT edits** — none surfaced by 4.2.
- **Slice 4.3 work** (authorize payment, stubbed) — next session.
