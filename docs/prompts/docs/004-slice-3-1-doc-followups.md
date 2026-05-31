# Prompt: Docs 004 — Slice 3.1 Doc Follow-ups + README Refresh

**Kind**: maintenance / docs surface (housekeeping — closing the doc-half of slice 3.1's spec delta + a README staleness refresh)
**Files touched**: `docs/workshops/001-crittermart-event-model.md` (edit — § 6.1 slice-3.1 GWT scenarios + § 9 Document History); `README.md` (edit — Wolverine version, BC-table Status column, status note, Run-locally section, repo-structure comment); `openspec/changes/slice-3-1-add-to-cart/` → `openspec/changes/archive/` + `openspec/specs/shopping-cart/spec.md` (CLI archive); `docs/prompts/docs/004-...` (this file, frozen); `docs/retrospectives/docs/004-...` (new)
**Mode**: solo, user-directed — three named follow-ups bundled into one `tidy:` PR per maintainer direction.
**Commit subject**: `tidy: docs — slice 3.1 workshop amendment, README refresh, archive shopping-cart`

## Framing

Slice 3.1 (add-to-cart) shipped in PR #25. Retrospective 006 confirmed the code-half of the spec delta landed but explicitly **deferred** two doc follow-ups out of the `feat:` PR to honor the no-opportunistic-edits discipline: (1) amending Workshop § 6.1's literal slice-3.1 wording, which still implied `customerId` stream keying and a slice-3.1 `CartActivityTimeout`; and (2) refreshing stale README rows. A third housekeeping item — `openspec archive slice-3-1-add-to-cart` — was also named as a post-merge step. This session closes all three in one PR, which doubles as a **design-return interleave** for the accumulating Orders implementation work before the 4.x slices begin.

## Goal

After this session: the Workshop's slice-3.1 GWT scenarios match what shipped (`cartId` keying, `CartActivityTimeout` deferred to 3.4); the README reflects the implementation phase rather than a design-phase empty `src/`; and `shopping-cart` is a durable main spec with the slice-3.1 change archived.

## Spec delta

- **Workshop § 6.1 (slice 3.1 GWT)** gains an amendment: Cart stream keyed by a generated `cartId` (not `customerId`); open cart resolved via a partial-unique `CartView` index on `customerId`; `CartActivityTimeout` deferred to slice 3.4. Recorded in the workshop's § 9 Document History as v1.1. The § 5 slice table is left at model-level intent intentionally.
- **`shopping-cart`** capability promoted from active change to main spec via `openspec archive`.
- No new ADR; no code change.

## Orientation

1. `docs/retrospectives/implementations/006-slice-3-1-add-to-cart.md` — *Outstanding items* names all three follow-ups; the canonical scope source.
2. `docs/workshops/001-crittermart-event-model.md` § 6.1 (slice 3.1) + § 8 (open questions) + § 9 (Document History).
3. `README.md` — BC table, Tech Stack, Getting Started, Repository Structure.
4. `openspec/changes/slice-3-1-add-to-cart/` — the change to archive.

## Working pattern

1. Archive the change first (`openspec archive slice-3-1-add-to-cart -y`) so the workshop amendment can reference the durable `openspec/specs/shopping-cart/spec.md` path; `openspec validate --all` after.
2. Amend § 6.1 + add the § 9 v1.1 row.
3. Refresh the README; grep-sweep for residual staleness (`forthcoming`, `design phase`, `5+`, `intended workflow`).
4. Build to confirm the doc-only PR breaks nothing (no code touched, so a quick `dotnet build` is a sanity check, not a requirement).

## Out of scope

- The § 5 slice table's `refresh CartActivityTimeout` cells (model-level intent — leave).
- The pre-existing "see open question in §9" mis-reference on slice 4.5 (line ~382) — not part of the named scope; leave for a future sweep.
- Any `src/` code change.
- Slice 4.x work (next session).
