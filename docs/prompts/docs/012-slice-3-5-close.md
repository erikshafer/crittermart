# Prompt: Docs 012 — Slice 3.5 Close (Archive + Workshop §6 Amendment)

**Kind**: maintenance / docs surface (post-merge close of slice 3.5 — fold the shipped read endpoint back into the canonical spec + workshop)
**Files touched**: `openspec/changes/slice-3-5-view-open-cart/` → `openspec/changes/archive/2026-06-15-slice-3-5-view-open-cart/` (archive move, CLI-driven); `openspec/specs/shopping-cart/spec.md` (delta folded in by the CLI — 7→8 requirements); `docs/workshops/001-crittermart-event-model.md` (edit — § 6 slice 3.5 v1.9 amendment + Document History row); `docs/narratives/README.md` (edit — 005 index row → v1.1); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — count bumps); `docs/retrospectives/docs/012-slice-3-5-close.md` (new)
**Mode**: solo synthesis — run the openspec archive, then reconcile the workshop's one open modeling question (identity transport) against shipped reality and record the divergences as a faithfulness amendment. Append-only on the workshop body; the modeled GWT text is preserved.
**Commit subject**: `tidy: docs — close slice 3.5 (archive openspec change, amend workshop §6, bump READMEs)`

## Framing

Slice 3.5 (`GET /carts/mine`) shipped in **PR #50** (`76ab034`), squash-merged. A squash merge does not run `openspec archive`, so the change `slice-3-5-view-open-cart` was left **active (7/8 tasks)** on `main` with its delta un-folded — the canonical `openspec/specs/shopping-cart/spec.md` still showed 7 requirements while the code satisfied 8. This is the owed post-merge close.

It is also a *spec-records* step in the spec-delta closure loop: the workshop modeled slice 3.5 (v1.8) leaving one question open — "query-param vs. header is the slice's OpenSpec/implementation call" — and modeled only two GWTs (happy + no-open-cart). The implementation resolved the transport to the **`X-Customer-Id` header** and added a **third GWT** (missing/blank identity → `400`). Both divergences were staged as faithfulness notes in the change's `design.md`; this session lifts them into the workshop where the canonical model lives.

Per the **tidy-ceremony rule**, a tidy that authors spec content (a workshop amendment) carries the full prompt/retro pair — this one does.

## Goal

After this session, the canonical artifacts match shipped reality:

1. The openspec change is **archived** — its delta folded into `openspec/specs/shopping-cart/spec.md` (the 8th requirement, "Read the Customer's open cart"), and `openspec validate --specs` is green.
2. **Workshop 001 § 6** carries a v1.9 amendment block under slice 3.5 recording the two faithfulness divergences (header transport resolved; `400` guard added) plus the implementation shape and "no new event/command/projection/index", with a v1.9 Document History row.
3. The **index READMEs are accurate** — narratives README 005 → v1.1 (partly built); prompts/retros README counts reflect the 005 narrative pair, the 015 impl pair, and the lagging round-one-close (011) + this slice-3.5-close (012) docs pairs.

## Spec delta

Workshop 001 gains a v1.9 § 6 amendment + Document History row resolving slice 3.5's one open modeling question (identity transport → header) and recording the added `400` GWT. The `shopping-cart` capability gains its 8th requirement via the archive fold (no *new* modeling — the requirement was authored in the now-archived change; archiving promotes it to the main spec). No workshop *slice* is added or removed; no other capability changes.

## Orientation

Read in this order:

1. **The session handoff** (`crittermart-handoff-2026-06-15-frontend-bootstrap.md`) and **CLAUDE.md** — the tidy-ceremony rule, the spec-delta closure loop, the append-only amendment discipline.
2. **`openspec/changes/slice-3-5-view-open-cart/design.md`** — the two faithfulness notes (the source of the amendment); **`tasks.md`** — confirms 7/8 and what shipped.
3. **`docs/workshops/001-crittermart-event-model.md`** — § 6 slice 3.5 (the modeled GWTs, lines ~464–476) and § 9 Document History (v1.0–v1.8); the v1.5 amendment block is the format precedent.
4. **`docs/prompts/docs/011-round-one-close-reconciliation.md`** — the closest tidy/docs precedent for prompt/retro shape.

## Out of scope

- **No `client/` scaffolding, no Aspire `AddViteApp`, no W2 screen, no CORS-origin injection.** Those are the separate frontend-bootstrap PR. This tidy is docs-only.
- **Do not rewrite the modeled GWT text.** The workshop is append-only — the modeled "query-param vs. header is the slice's call" line stays as the modeling-time record; the resolution is an appended amendment block.
- **Do not re-open resolved questions.** The header transport was a user-resolved fork before slice 3.5 was authored; this session records it, it does not re-litigate it.
- **No edits to code, tests, or the archived change's frozen artifacts** beyond the CLI-driven archive move + spec fold.

## Deliverable plan

1. `npx openspec archive slice-3-5-view-open-cart --yes` — moves the change to `archive/` and folds the delta into `shopping-cart/spec.md`. Run `openspec validate --specs` (4 passed).
2. Workshop 001 — append the v1.9 § 6 amendment block under slice 3.5 + the v1.9 Document History row.
3. Narratives README — 005 row → v1.1 / "partly built".
4. Prompts + retrospectives READMEs — bump `narratives` (3→4), `implementations` (14→15), `docs` (10→12, picking up the lagging 011 + this 012).
5. Retro (`docs/retrospectives/docs/012-slice-3-5-close.md`) — seven-section format; the spec-delta line forward-confirms the named delta landed.

## Working pattern

Archive first (it generates the spec fold the amendment references), then the workshop amendment, then the README reconciliation, then the retro. One branch (`tidy/close-slice-3-5`), one PR, containing this prompt, the archive move + spec fold, the workshop + README edits, and the retro. Nothing else. The frontend-bootstrap PR stacks on this branch so the two diffs do not contend on the README counts.
