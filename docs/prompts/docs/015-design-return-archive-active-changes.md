# Prompt: Docs 015 — Design-Return: Workshop § 6 Slice 3.1 Add-to-Cart Faithfulness Note + Archive `harden-add-to-cart-snapshot` & `list-my-orders`

**Kind**: maintenance / docs surface (design-return cadence interleave — author the long-fenced workshop slice-3.1 malformed-snapshot faithfulness note, then archive the two satisfied OpenSpec changes, syncing both `+1 ADDED` requirements into the main specs and returning the workspace to **0 active changes**)
**Files touched**: `docs/workshops/001-crittermart-event-model.md` (edit — § 6 slice 3.1 **v1.13** amendment block recording the #69 malformed-snapshot rejection); `openspec/` (CLI archive of **`harden-add-to-cart-snapshot`** → folds its +1 ADDED into `shopping-cart` 8→9, and **`list-my-orders`** → folds its +1 ADDED into `order-lifecycle` 9→10; both moved to `openspec/changes/archive/2026-06-17-*`, after ticking their `tasks.md` to reflect shipped work); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — `docs/` count 14→15 + population note); `docs/retrospectives/docs/015-design-return-archive-active-changes.md` (new)
**Mode**: solo synthesis — append-only on the frozen workshop § 6 timeline; CLI-driven spec sync (the `openspec archive` convention, not manual file moves); mechanical index reconciliation. **No genuine fork** — the cadence dictates the interleave, and its components were pre-named in retro 028 + retro 026 (the #69-fenced § 6.1 note) and the `next-pickup` memory.
**Commit subject**: `tidy: design-return — workshop § 6 slice 3.1 add-to-cart faithfulness note + archive harden-add-to-cart-snapshot & list-my-orders (0 active changes)`

## Framing

This is the **design-return cadence interleave** owed after the three implementations since the #68 design-return (#69 `AddToCart` hardening, #70 OTel teaching pass, #71 the "My Orders" list). Per CLAUDE.md § *Design-return cadence*, after every 2–3 implementation PRs the next PR must be a design-return; doing it now satisfies the cadence cleanly rather than riding it to the 4th-consecutive drift signal. With all four round-one BCs workshopped and the storefront spine + Gap #3 shipped, the cadence-satisfying move is a **`tidy:` design-return that drains the two active OpenSpec changes into the main specs and closes the one reverse-spec-delta the #69 slice honestly fenced** — the shape docs/013 and docs/014 took.

Three threads converge into one PR:

1. **The #69-fenced workshop § 6 slice-3.1 faithfulness note.** Retro 026 (harden) listed, as deferred task 5.2, an *"optional workshop § 6.1 slice 3.1 faithfulness note (malformed-input guard added beyond the modeled happy paths)."* Slice 3.1's § 6 GWT models only happy paths; #69 added a real failure path — an `AddToCart` with no usable product snapshot (absent / blank name / negative price) is rejected with `400` at the boundary, before any `Cart` stream starts. That failure path must be recorded on the workshop timeline.

2. **`harden-add-to-cart-snapshot` is satisfied and should be archived.** It validated `--strict` and shipped in #69; its `shopping-cart` delta (the ADDED requirement *Reject an add-to-cart command with no usable product snapshot*) belongs in the main spec. `openspec archive` folds it (8→9) and moves the change to `archive/`.

3. **`list-my-orders` is satisfied and should be archived.** It validated `--strict` and shipped in #71; its `order-lifecycle` delta (the ADDED requirement *List a customer's own orders*) belongs in the main spec. `openspec archive` folds it (9→10) and moves the change to `archive/`. Retro 028 named this as the standard post-merge tidy.

Per the **tidy-ceremony rule**, a tidy that authors spec content (the workshop amendment) carries the full prompt/retro pair — this one does. The two archives ride along.

## Goal

After this session, the canonical docs and specs agree with shipped reality:

1. **Workshop 001 § 6 slice 3.1** carries a **v1.13 amendment block** recording the #69 malformed-snapshot `400` rejection as a failure path beyond the modeled happy paths — distinct from the domain-state rejections (`CartItemNotPresent`, `NoOpenCart`), with the "snapshot is the cart's only product truth" rationale and the `Validate(AddToCart) → ProblemDetails` shape. The frozen § 6 GWT text and the existing v1.1 amendment are left intact (append-only).
2. **OpenSpec `shopping-cart` main spec** carries the ADDED requirement (8→**9**); **`order-lifecycle`** carries its ADDED requirement (9→**10**); both changes are archived (`No active changes found`); both specs validate `--strict`.
3. **Index READMEs accurate** — `docs/` count 14→15 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, population note extended.

## Spec delta

Workshop 001 gains a **v1.13 § 6 slice-3.1 amendment block** recording the #69 malformed-snapshot `400` rejection (Narrative 004 v1.9 already carries the Moment-1 note — the workshop is the straggler catching up, the same pattern as docs/013/014). The OpenSpec **`shopping-cart`** main spec gains its **+1 ADDED requirement** (8→9) and **`order-lifecycle`** gains its **+1 ADDED requirement** (9→10) via `openspec archive`, and both changes move to `archive/`. No workshop *slice* is added or removed; no code, no tests; the index-count bump is mechanical. This reconciles canonical docs/specs with shipped code — it does not alter the modeled scenario set.

## Orientation

Read in this order:

1. **CLAUDE.md** — § *Design-return cadence*, § *Tidy ceremony rule*, § *Spec-delta closure loop*, and the append-only amendment discipline.
2. **`docs/retrospectives/implementations/026-harden-add-to-cart-snapshot.md`** + **`028-list-my-orders.md`** — the #69 / #71 session records; 026 § *Deferred* fences the § 6.1 note (task 5.2), 028 § *Outstanding* names both archives + the due interleave.
3. **`docs/workshops/001-crittermart-event-model.md`** — § 6 slice 3.1 (~line 417): the two happy-path GWTs + the existing v1.1 amendment (the format precedent; the new v1.13 block appends after it); the § 5.1 v1.12 amendment is the most recent doc-version anchor (so this is v1.13).
4. **`openspec/changes/harden-add-to-cart-snapshot/specs/shopping-cart/spec.md`** + **`list-my-orders/specs/order-lifecycle/spec.md`** — the two deltas being folded; their `proposal.md`/`tasks.md` for the archive.
5. **`docs/prompts/docs/014-design-return-section-5-1-shipped.md`** + its retro — the closest precedent for prompt/retro shape and the CLI-archive discipline.

## Working pattern

Author the **workshop v1.13 amendment first** (the spec-content anchor that satisfies the cadence) — append-only after the frozen slice-3.1 v1.1 block, before the `### 3.2` heading. Then **tick both changes' `tasks.md`** (the work shipped in #69 / #71; the archive tasks are this tidy) and run **`npx openspec archive harden-add-to-cart-snapshot -y`** + **`npx openspec archive list-my-orders -y`** (CLI does the spec sync + the move; `-y` because the shell is non-interactive). Verify `openspec list` shows no active changes and `openspec list --specs` shows shopping-cart 9 + order-lifecycle 10. Then **reconcile the index counts against reality** (14→15). Then the **retro**. One branch (`tidy/design-return-archive-active-changes`), one PR, containing this prompt, the workshop + openspec archive edits, the index edits, and the retro. Nothing else.

## Deliverable plan

1. **Workshop 001 § 6 slice 3.1** — append the v1.13 amendment block (after the v1.1 block, before `### 3.2`) recording the #69 malformed-snapshot `400` rejection failure path.
2. **OpenSpec archive** — tick both `tasks.md`, run `openspec archive` for both, verify `openspec list` (no active changes) + counts (shopping-cart 9, order-lifecycle 10) + both validate.
3. **Index READMEs** — `docs/prompts/README.md` + `docs/retrospectives/README.md`: `docs/` 14→15, population note extended with this design-return + the two archives.
4. **Retro** (`docs/retrospectives/docs/015-design-return-archive-active-changes.md`) — seven-section format; the spec-delta line forward-confirms the named delta landed and closes the retro-026-fenced § 6.1 note + retro-028's archive.

## Out of scope

- **No code, no tests, no new OpenSpec change.** The reconciliation is docs/spec only; the archives are CLI syncs of *already-shipped* changes, not new proposals.
- **Do not rewrite the frozen § 6 GWT text or the v1.1 amendment block.** The workshop is append-only — slice 3.1's happy-path GWTs and the v1.1 amendment stay as the modeling-time record; the failure path is an appended v1.13 block.
- **Do not re-open resolved decisions.** The `Validate`-guard shape, the 400-not-500 behavior, and the three unusable-snapshot cases were resolved in #69 and locked in the shopping-cart delta + Narrative 004 v1.9; this session records them in the workshop + main spec, it does not re-litigate them.
- **Do not build the deferred surfaces.** The cart identity-transport harmonization, product detail (Gap #2), list pagination, and the OTel/browser visual pass are all out — the next implementation is an open pick *after* this design-return.
- **No live boot.** A docs/spec tidy — verification is `openspec list`/`--specs` + a re-read of the amendment, not an Aspire stack.
