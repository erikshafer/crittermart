# Prompt: Docs 005 — Slice 4.1 doc follow-ups (archive, spec Purposes, README refresh)

**Kind**: `tidy: docs` — maintenance of existing artifacts after slice 4.1 (#29) merged; one PR
**Files touched**: this prompt; `openspec/changes/slice-4-1-place-order/**` → `openspec/changes/archive/2026-05-31-slice-4-1-place-order/**` (CLI move); `openspec/specs/order-lifecycle/spec.md` (new main spec — fill Purpose); `openspec/specs/shopping-cart/spec.md` (append checkout requirement via archive; fill the lingering slice-3.1 Purpose TBD); `README.md` (Orders BC-status row + Getting Started status note); `docs/retrospectives/docs/005-slice-4-1-doc-followups.md` (forthcoming)
**Mode**: solo tidy; no code changes
**Commit subject**: `tidy: docs — archive slice-4-1, fill spec Purposes, README refresh`

## Framing

The slice 4.1 feat PR (#29) deliberately left three doc follow-ups out of scope (no opportunistic edits), named in retro 007: archive the shipped change, fill the `shopping-cart` spec's `## Purpose` TBD (lingering from slice 3.1's archive), and refresh stale README rows. This session drains them. Mirrors the slice-3.1 doc-followups tidy (docs 004 / PR #28).

## Goal

`openspec archive slice-4-1-place-order` folds `order-lifecycle` into a durable main spec and appends the checkout requirement to `shopping-cart`. Both main specs get a meaningful `## Purpose` (replacing the archive's `TBD` placeholders). The README's Orders BC-status row and Getting Started status note reflect 4.1 place-order shipped. `openspec validate --all` stays green.

## Spec delta

No new spec content — this is the closure step for slice 4.1's spec delta: the change moves to `archive/`, `order-lifecycle` + `shopping-cart` become the durable main specs. Purpose sections are authored (not generated). No narrative/workshop amendment needed (4.1's narrative bump landed in #29; the workshop slice table already lists 4.7 as the timeout slice, so the 4.1 deferral needs no workshop edit).

## Orientation

1. **`docs/retrospectives/implementations/007-slice-4-1-place-order.md`** — the "Outstanding / next-session inputs" section names exactly these follow-ups.
2. **`docs/prompts/docs/004-slice-3-1-doc-followups.md` + retro** — the precedent tidy this mirrors (archive + README + Purpose pattern).
3. **`README.md`** §§ Bounded Contexts, Getting Started — the stale rows.

## Out of scope

- **No code, no test changes.** **No new slice content.**
- **No `docs/skills/` or DEBT edits** — none surfaced.
- **Slice 4.2 work** — next session.
