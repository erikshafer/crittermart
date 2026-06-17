# Prompt: Docs 014 — Design-Return: Workshop § 5.1 v1.11 "Aspirational → Shipped" Flip + `enrich-order-status-view` Archive + Orders Path-Ref Sweep

**Kind**: maintenance / docs surface (design-return cadence interleave — flip the workshop's § 5.1 v1.10-fenced W4 placed-at line + per-reason cancel copy from *aspirational* to *bound* now that the enrich slice shipped, archive the satisfied OpenSpec change, and sweep three stale Orders verb-folder path refs)
**Files touched**: `docs/workshops/001-crittermart-event-model.md` (edit — § 5.1 **v1.11** amendment block + frontmatter `version`/`date`/`status` sync + § 9 Document History row); `openspec/` (CLI archive of `enrich-order-status-view` → folds the +1 ADDED requirement into `order-lifecycle` 8→9, moves the change to `openspec/changes/archive/2026-06-17-enrich-order-status-view/`, after ticking its `tasks.md` to reflect the shipped work); `client/src/orders/orderSchema.ts` (edit — 3 stale `…/Order/…` → `…/Ordering/…` comment path refs, the ADR 021 verb-folder rename); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — `docs/` count 13→14 + population note); `docs/retrospectives/docs/014-design-return-section-5-1-shipped.md` (new)
**Mode**: solo synthesis — append-only on the frozen workshop timeline; CLI-driven spec sync (the `openspec archive` convention, not a manual file move); mechanical comment + index reconciliation. Collaborative on the one genuine sub-fork: the § 5.1 flip *style*, resolved **Option A — new v1.11 append-only amendment** via AskUserQuestion with previews (declining the in-place-edit-of-v1.10 and the hybrid-pointer variants).
**Commit subject**: `tidy: design-return — flip workshop § 5.1 W4 to shipped (slice 025) + archive enrich-order-status-view + Orders path-ref sweep`

## Framing

This is the **design-return cadence interleave** owed after the two implementations since the #65 design-return (#66 Stock read/write split, #67 the `OrderStatusView` enrich). Per CLAUDE.md § *Design-return cadence*, after every 2–3 implementation PRs the next PR must be a design-return; doing it now satisfies the cadence cleanly rather than riding it to the 4th-consecutive drift signal. With all four round-one BCs workshopped and the storefront spine (W1→W4) shipped, the cadence-satisfying move is a **`tidy:` design-return that closes the one reverse-spec-delta the last slice honestly fenced** — exactly the shape docs/013 took.

Three threads converge into one PR:

1. **The § 5.1 v1.10 "aspirational" fence is now stale.** docs/013's v1.10 amendment fenced W4's `Placed … UTC` line and per-reason `cancelled` copy as *"aspirational, not bound, until a future 'enrich `OrderStatusView`' slice"* — the shipped `OrderStatusView` was then `{ id, customerId, status, lines, total }` with `OrderCancelled` folding to a bare `cancelled`. **Slice 025 (PR #67) shipped exactly that enrich slice**: `placedAt` (genesis `OrderPlaced` append metadata) + `cancelReason` (folding `OrderCancelled.Reason`). The fence must flip to *bound*. Retro 025 § *Spec-delta — landed?* named this flip as the one deliberately-deferred edge — *"the workshop § 5.1 flip is fenced post-merge"* — so this session closes that named tidy.

2. **The OpenSpec change is satisfied and should be archived.** `enrich-order-status-view` validated `--strict` and shipped in #67; its `order-lifecycle` delta (the single ADDED requirement *Surface placement time and cancellation reason in the order view*) belongs in the main spec. `openspec archive` folds it (8→9 requirements) and moves the change to `archive/`.

3. **Three stale path refs.** `client/src/orders/orderSchema.ts` carries three `src/CritterMart.Orders/Order/…` comment refs that predate the ADR 021 verb-folder rename to `Ordering/` (the on-disk truth; slice 025's own line-35 `CancelReason` comment already uses `Ordering/`). Slice 025 left them per the no-opportunistic-edits rule; this Orders-doc tidy is their natural home.

Per the **tidy-ceremony rule**, a tidy that authors spec content (the workshop amendment) carries the full prompt/retro pair — this one does. The archive and the comment sweep ride along.

## Goal

After this session, the canonical docs and specs agree with shipped reality:

1. **Workshop 001 § 5.1** carries a **v1.11 amendment block** recording that the v1.10-fenced placed-at + per-reason copy **shipped** (slice 025 / PR #67) and are now **bound** — `OrderStatusView` is the additive superset `{ id, customerId, status, lines, total, placedAt, cancelReason }`. The frozen § 5.1 wireframe text **and the v1.10 amendment block** are left intact (append-only); frontmatter `version` v1.10→v1.11, `date`→2026-06-17, `status` extended; a v1.11 § 9 Document History row added.
2. **OpenSpec `order-lifecycle` main spec** carries the ADDED requirement (8→**9**), the change is archived (`No active changes found`), and the spec validates `--strict`.
3. **`client/src/orders/orderSchema.ts`** comment refs point at `Ordering/`; the `client` build stays green.
4. **Index READMEs accurate** — `docs/` count 13→14 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, population note extended.

## Spec delta

Workshop 001 gains a **v1.11 § 5.1 amendment block + Document History row** flipping the v1.10-fenced W4 placed-at line + per-reason cancel copy from *aspirational* to **bound**, recording that slice 025 (PR #67) added `placedAt` + `cancelReason` to `OrderStatusView` (Narrative 005 v1.7 already carries the binding — the workshop is the lone straggler catching up). The OpenSpec **`order-lifecycle`** main spec gains its **+1 ADDED requirement** via `openspec archive` (8→9), and the change moves to `archive/`. No workshop *slice* is added or removed; no code, no tests; the comment-path sweep and index-count bump are mechanical. This reconciles canonical docs/specs with shipped code — it does not alter the modeled scenario set.

## Orientation

Read in this order:

1. **The session handoff** (`crittermart-handoff-post-w4-enrich.md`) and **CLAUDE.md** — § *Design-return cadence*, § *Tidy ceremony rule*, § *Spec-delta closure loop*, and the append-only amendment discipline.
2. **`docs/retrospectives/implementations/025-enrich-order-status-view.md`** — the #67 session record; its § *Spec-delta — landed?* fences this exact tidy, and § *Outstanding* lists the three threads.
3. **`docs/workshops/001-crittermart-event-model.md`** — § 5.1 v1.10 amendment (the **W4 bullet** to flip, ~line 304) + the frontmatter + § 9 Document History (the v1.10/v1.9 rows are the format precedent; v1.10 is the direct predecessor block).
4. **`docs/narratives/005-customer-storefront.md`** v1.7 — already records the W4 binding; confirms the workshop is the straggler.
5. **`openspec/changes/enrich-order-status-view/`** — the change to archive (proposal / `order-lifecycle` delta / design / tasks); `openspec/specs/order-lifecycle/spec.md` for the pre-archive count (8).
6. **`client/src/orders/orderSchema.ts`** — the three stale `Order/` refs (lines 6, 21, 48; line 35 is already `Ordering/`).
7. **`docs/prompts/docs/013-design-return-workshop-reconcile.md`** + its retro — the closest precedent for prompt/retro shape and the count-reconciliation discipline.

**Gotcha (cost time historically):** the openspec CLI is **GLOBAL — invoke `openspec`, NOT `npx openspec@latest`** (npx fails with "could not determine executable to run"). Let `openspec archive` do the spec sync; do not hand-edit `openspec/specs/order-lifecycle/spec.md`.

## Working pattern

Author the **workshop v1.11 amendment first** (the spec-content anchor that satisfies the cadence) — append-only after the frozen v1.10 block, frontmatter sync, § 9 row placed adjacent to v1.10 so the fence→shipped pair reads in sequence (leave the pre-existing v1.9/v1.10 ordering quirk untouched). Then **tick the change's `tasks.md`** (the work shipped in #67; task 5.4 is this tidy) and **`openspec archive enrich-order-status-view -y`** (CLI does the spec sync + the move; `-y` because the Bash shell is non-interactive). Then **sweep** the `orderSchema.ts` comment refs (`replace_all` on the `…/Order/` token — it cannot match the already-correct `…/Ordering/`). Then **reconcile the index counts against `ls`** (13→14), not the prior value. Then the **retro**. One branch (`tidy/design-return-section-5-1-shipped`), one PR, containing this prompt, the workshop + openspec + orderSchema + index edits, and the retro. Nothing else.

## Deliverable plan

1. **Workshop 001 § 5.1** — append the v1.11 amendment block (after the v1.10 block, before the `---`) flipping W4 placed-at + per-reason copy to bound; sync frontmatter `version`/`date`/`status`; add the v1.11 § 9 Document History row.
2. **OpenSpec archive** — tick `enrich-order-status-view/tasks.md` (10/10), run `openspec archive enrich-order-status-view -y`, verify `openspec validate order-lifecycle --type spec --strict` + `openspec list` (no active changes) + requirement count 9.
3. **`client/src/orders/orderSchema.ts`** — flip the 3 stale `Order/`→`Ordering/` comment refs; `npm --prefix client run build` green.
4. **Index READMEs** — `docs/prompts/README.md` + `docs/retrospectives/README.md`: `docs/` 13→14, population note extended with this design-return flip + archive.
5. **Retro** (`docs/retrospectives/docs/014-design-return-section-5-1-shipped.md`) — seven-section format; the spec-delta line forward-confirms the named delta landed and closes the retro-025-fenced tidy.

## Out of scope

- **No code, no tests, no new OpenSpec change.** The reconciliation is docs/spec only; the archive is a CLI sync of an *already-shipped* change, not a new proposal. No `client/` source change beyond the comment-ref sweep (comment-only — no logic, no schema shape change).
- **Do not rewrite the frozen § 5.1 wireframe text or the v1.10 amendment block.** The workshop is append-only — v1.10's W4 "aspirational" bullet stays as the modeling-time record; the flip is an appended v1.11 block (the resolved sub-fork; the in-place-edit and hybrid-pointer variants were declined).
- **Do not re-open resolved decisions.** The `placedAt`-from-metadata source and the additive-superset shape were resolved in slice 025 and locked in Narrative 005 v1.7 + the order-lifecycle delta; this session records them in the workshop + main spec, it does not re-litigate them.
- **Do not fix the v1.9/v1.10 § 9 ordering quirk.** The pre-existing physical ordering of the history rows is left as-is — reordering a frozen history table is an opportunistic edit out of this session's scope.
- **Do not build the deferred surfaces.** The `AddToCart` null-snapshot 500 hardening, the "My Orders" list (Gap #3), the cart identity-transport harmonization, and the OTel/browser visual pass are all out — the next implementation is an open pick *after* this design-return.
- **No live boot.** A docs/spec tidy — verification is `openspec validate` + `openspec list` + the `client` build + a re-read of the flipped § 5.1, not an Aspire stack.
