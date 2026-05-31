---
retrospective: 004
kind: docs
prompt: docs/prompts/docs/004-slice-3-1-doc-followups.md
deliverable: docs/workshops/001-crittermart-event-model.md (edit — § 6.1 slice-3.1 GWT + § 9 Document History v1.1); README.md (edit — Wolverine 5+→6+ badge + tech-stack row, BC-table Status column, design-phase status note, Run-locally section, repo-structure src/ comment); openspec archive slice-3-1-add-to-cart (→ openspec/changes/archive/2026-05-31-slice-3-1-add-to-cart/ + openspec/specs/shopping-cart/spec.md created); docs/prompts/docs/004-... (frozen); docs/retrospectives/docs/004-... (this file)
date: 2026-05-30
mode: solo, user-directed — three retro-006 follow-ups bundled into one tidy PR per maintainer direction
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 004: Slice 3.1 Doc Follow-ups + README Refresh

## Outcome summary

Closed the three follow-ups that retrospective 006 deferred out of the slice 3.1 `feat:` PR (#25), in one `tidy: docs` PR:

1. **OpenSpec archive.** `openspec archive slice-3-1-add-to-cart -y` moved the change into `openspec/changes/archive/2026-05-31-slice-3-1-add-to-cart/` and synced its `shopping-cart` capability into the durable main spec at `openspec/specs/shopping-cart/spec.md`. `openspec validate --all` → 3 passed (`product-catalog`, `shopping-cart`, `stock-management`), 0 failed. `openspec/changes/` now holds only `archive/` — no active change pending.
2. **Workshop § 6.1 amendment.** Amended the two slice-3.1 GWT `Then` clauses to reflect what shipped — Cart stream keyed by a generated `cartId` (not `customerId`, which now rides as a field on `CartCreated`), open cart resolved by querying `CartView` on `customerId` behind the partial-unique `IsOpen` index — and added an explicit amendment note plus a v1.1 row in § 9 Document History. The `CartActivityTimeout` references in both clauses are now marked deferred to slice 3.4.
3. **README refresh.** Wolverine `5%2B`→`6%2B` (badge + tech-stack row, reflecting the Critter Stack 2026 line); the BC table's Status column moved off "Workshop complete; implementation forthcoming" to per-BC implementation state (Catalog 1.1–1.3, Inventory 2.1–2.2, Orders 3.1 in-progress); the Getting-Started status note rewritten from "design phase / `src/` intentionally empty" to "per-slice implementation phase" with the actual five `src/` projects named; the Run-locally section de-conditionalized (real `--launch-profile http` command + dashboard URL); the repo-structure `src/` comment updated from "Forthcoming" to the actual project list.

The PR carries no code change.

## What worked

- **Archiving first paid off.** Running the archive before authoring the workshop amendment meant the amendment could cite the durable `openspec/specs/shopping-cart/spec.md` path rather than a soon-to-move active-change path. Sequence mattered.
- **The model-vs-implementation line stayed clean.** The amendment lives in § 6.1 (GWT scenarios — "what slice 3.1 shipped") and § 9 (history), while the § 5 slice table is left at model-level intent ("the complete round-one design, where add-to-cart conceptually refreshes the activity timeout"). The v1.1 Document History row states this split explicitly so a future reader doesn't mistake the untouched table for an oversight.
- **The post-edit grep sweep confirmed completeness** (the discipline retro 003 recommended). After the README edits, a sweep for `forthcoming|design phase|5+|intended workflow|intentionally empty` returned a single hit — the intentional "Order journey (4.x) forthcoming" in the Orders Status cell. High confidence the refresh is complete.

## What was harder than expected

- **Scoping the README refresh.** "Refresh stale rows" could have ballooned. The staleness all traced to one root fact — the project moved from design-phase-empty-`src/` to implementation-underway — so the edits form one coherent refresh (version, BC status, status note, run section, structure comment) rather than opportunistic drift. The Wolverine version bump to 6+ is the one that's a moving target; left as `6+` (not pinned to 6.1) to match the badge's major-version convention and avoid re-staling on the next patch.
- **The archive folder dated 2026-05-31, not 2026-05-30.** The openspec CLI names archive folders in UTC; the session's local date is 2026-05-30. Authored doc dates (workshop v1.1, this retro) use 2026-05-30 per the project's currentDate convention; the CLI-controlled folder name is left as-is (not worth fighting the tool).

## Methodology refinements that emerged

1. **A deferred doc-half closes cleanly as a `tidy:` PR that also banks design-return credit.** Retro 006 correctly kept these out of the `feat:` PR; bundling all three retro-006 doc follow-ups here both closes the spec-delta doc-half and serves the design-return cadence before 4.x — one PR, two disciplines satisfied.
2. **Cite the durable spec path, not the change path, in upstream artifacts.** The workshop amendment points at `openspec/specs/shopping-cart/spec.md` (survives archiving) rather than the change folder (moves on archive). Worth making a default when an artifact references an OpenSpec capability that's about to be archived.

## Outstanding items / next-session inputs

1. **Slice 4.1 (place order)** is the next session — checkout flips `CartView.IsOpen` (the slice-3.1 index predicate already anticipates it) and starts the Order stream. Then **4.2 (cross-BC reserve stock over RabbitMQ)**, the demo centerpiece, which now has its Orders prerequisite and lights up CritterWatch (ADR 013).
2. **Pre-existing § 6.1/4.5 cross-reference drift** — slice 4.5's GWT (workshop line ~382) says "open question §9" but the open-questions section is § 8 (§ 9 is Document History); the same stale ref existed on the slice-3.1 line and was fixed in passing as part of this amendment. The 4.5 occurrence was left untouched (outside named scope) — a future docs sweep can fix it.
3. **Encode the one-capability-per-aggregate convention** remains deferred until the Order capability lands (per the standing carry-forward) — `shopping-cart` is now the second confirmed instance (Cart), with Order's capability arriving in 4.x.

## Spec-delta — landed?

**Yes.** Workshop § 6.1 amended (cartId keying + `CartActivityTimeout` deferral) and recorded as v1.1 in § 9 Document History; `shopping-cart` promoted to a durable main spec via archive (`validate --all` green); README refreshed to the implementation phase. The § 5 slice table was intentionally left at model-level intent, stated as such in the v1.1 row. No new ADR — this is a state-of-fact documentation update, not an architectural decision.

## Process notes

- One `tidy: docs` PR on branch `tidy/docs-slice-3-1-followups`, branched before committing. No code touched.
- Three retro-006 follow-ups bundled per maintainer direction (the user asked for one PR covering all three) — a deliberate, recorded bundling at the PR level; the single commit carries all deliverables.
