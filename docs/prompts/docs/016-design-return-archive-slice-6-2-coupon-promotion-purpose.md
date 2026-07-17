# Prompt: Docs 016 — Design-Return: Archive `slice-6-2-advisory-coupon-validate` + write the real `coupon-promotion` `## Purpose`

**Kind**: maintenance / docs surface (design-return cadence interleave — CLI-archive the one satisfied OpenSpec change, syncing its `+1 ADDED` requirement into the main spec and returning the workspace to **0 active changes**; then author the `coupon-promotion` capability's real `## Purpose` over the archive-stamped TBD)
**Files touched**: `openspec/` (CLI archive of **`2026-07-16-slice-6-2-advisory-coupon-validate`** → folds its +1 ADDED into `coupon-promotion` 4→5, moves the change to `openspec/changes/archive/2026-07-16-slice-6-2-advisory-coupon-validate/`); `openspec/specs/coupon-promotion/spec.md` (edit — replace the TBD `## Purpose` with a real one); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — `docs/` count 15→16 + population note); `docs/retrospectives/docs/016-design-return-archive-slice-6-2-coupon-promotion-purpose.md` (new)
**Mode**: solo synthesis — CLI-driven spec sync (the `openspec archive` convention, not manual file moves); a single spec-content edit (the Purpose); mechanical index reconciliation. **One genuine fork resolved at session start** (Erik chose "Full pair, one PR"): writing a real `## Purpose` is spec-content authoring, so per the tidy-ceremony rule this tidy carries the full prompt/retro pair rather than riding light like the #145 mechanical archive that left the TBD.
**Commit subject**: `tidy: design-return — archive slice-6.2 advisory-coupon-validate + write coupon-promotion Purpose (0 active changes)`

## Framing

This is the **design-return cadence interleave** owed after the two Promotions implementation PRs since the ADR-024 design PR: **#144** (slices 6.1/6.3/6.4, the DCB core) and **#146** (slice 6.2, the advisory cart-review preview + W2/W3 UI). Per CLAUDE.md § *Design-return cadence*, after every 2–3 implementation PRs the next PR must be a design-return; a third consecutive Promotions implementation would signal drift. The natural, already-owed candidate — named in the [`promotions-design-return` handoff](../../handoffs/promotions-design-return.md) — is the **post-merge archive tidy** for slice 6.2 (the coupon-dcb / customer-data precedent: the archive rides a *separate* post-merge PR, not the implementation PR).

Two threads converge into one PR:

1. **`2026-07-16-slice-6-2-advisory-coupon-validate` is satisfied and should be archived.** It validated `--strict` and shipped in #146; its `coupon-promotion` delta (the ADDED requirement *Validate and price a coupon at cart review*) belongs in the main spec. `openspec archive` folds it (4→5) and moves the change to `archive/`.
2. **The `coupon-promotion` `## Purpose` is still the archive-stamped TBD.** The #145 archive that first created `openspec/specs/coupon-promotion/spec.md` left `## Purpose` reading *"TBD - created by archiving change 2026-07-16-slices-6-1-6-3-6-4-coupon-dcb. Update Purpose after archive."* Now that the capability's requirement set is storefront-complete (five requirements: define → redeem-under-cap → release-on-cancel → advisory usage → advisory validate), this is the moment to write a real Purpose of the same caliber as its siblings (`order-lifecycle`, `stock-management`).

Per the **tidy-ceremony rule**, a tidy that authors spec content (a spec `## Purpose`) carries the full prompt/retro pair — this one does. The archive rides along.

## Goal

After this session, the canonical specs agree with shipped reality and the capability reads coherently to a fresh session-runner:

1. **OpenSpec `coupon-promotion` main spec** carries the ADDED requirement (4→**5**), the change is archived (`No active changes found`), and the spec validates `--strict`.
2. **`coupon-promotion` `## Purpose`** is a real, sibling-caliber paragraph — naming the coupon-definition streams + `CouponView`, the store-scoped Marten **DCB** cap enforcement (ADR 024, CritterMart's first), the tagged `CouponRedeemed`/`…Released` on the order stream, the advisory-vs-authoritative teaching contrast (`CouponUsageView` + the validate query never gate; the DCB read is the sole authority), the deferred standalone Promotions service, and the slice→PR map (6.1/6.3/6.4 → #144, 6.2 → #146).
3. **Index READMEs accurate** — `docs/` count 15→16 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, population note extended.

## Spec delta

The OpenSpec **`coupon-promotion`** main spec gains its **+1 ADDED requirement** (4→5, *Validate and price a coupon at cart review*) via `openspec archive`, and the change moves to `archive/`. Separately, the capability's **`## Purpose`** is authored from TBD to a real prose statement — a spec-content edit, the anchor that makes this a full-pair tidy. No workshop *slice* is added or removed; no narrative; no code, no tests; the index-count bump is mechanical. This reconciles the canonical spec with shipped code and gives the capability its missing Purpose — it does not alter the modeled scenario set.

## Orientation

Read in this order:

1. **CLAUDE.md** — § *Design-return cadence*, § *Tidy ceremony rule* (the "authors a spec `## Purpose`" clause is the reason for the pair), § *Spec-delta closure loop*.
2. **`docs/handoffs/promotions-design-return.md`** — this session's brief; names the archive + the TBD-Purpose opportunity.
3. **`docs/retrospectives/implementations/040-slice-6-2-advisory-coupon-validate.md`** — the #146 session record; confirms slice 6.2 shipped and the archive is a post-merge tidy.
4. **`openspec/specs/coupon-promotion/spec.md`** — the four pre-existing requirements + the archive-stamped TBD Purpose (the sibling Purposes in `order-lifecycle`/`stock-management`/`product-catalog` are the caliber to match).
5. **`docs/decisions/024-dcb-coupon-redemption-in-orders.md`** — ADR 024, the store-scoped-DCB decision the Purpose leads with.
6. **`docs/prompts/docs/015-design-return-archive-active-changes.md`** + its retro — the closest precedent for prompt/retro shape and the CLI-archive discipline.

## Working pattern

Run **`npx openspec archive 2026-07-16-slice-6-2-advisory-coupon-validate -y`** (CLI does the spec sync + the move; `-y` because the shell is non-interactive). The CLI unconditionally prepends today's date, double-prefixing the already-date-named change → **rename the archive dir** to the single-date sibling convention (`2026-07-16-slice-6-2-advisory-coupon-validate`). Then **write the real `## Purpose`** over the TBD line. Verify `openspec list` shows no active changes and `openspec validate coupon-promotion --specs --strict` passes with the 5th requirement present. Then **reconcile the index counts** (15→16). Then the **retro**. One branch (`tidy/archive-slice-6-2-coupon-promotion-purpose`), one PR, containing this prompt, the openspec archive + Purpose edits, the index edits, and the retro. Nothing else.

## Deliverable plan

1. **OpenSpec archive** — run `openspec archive` for the change, rename the double-dated archive dir to single-date, verify `openspec list` (no active changes) + `coupon-promotion` at 5 requirements + `--strict` validates.
2. **`coupon-promotion` `## Purpose`** — replace the TBD with a real sibling-caliber paragraph.
3. **Index READMEs** — `docs/prompts/README.md` + `docs/retrospectives/README.md`: `docs/` 15→16, population note extended with this design-return.
4. **Retro** (`docs/retrospectives/docs/016-design-return-archive-slice-6-2-coupon-promotion-purpose.md`) — seven-section format; the spec-delta line forward-confirms the archive synced (4→5) and the Purpose landed.

## Out of scope

- **No code, no tests, no new OpenSpec change, no narrative.** The reconciliation is spec-Purpose + a CLI sync of an *already-shipped* change, not a new proposal.
- **Do not touch the `customer-registry` TBD Purpose.** It is a second latent TBD surfaced this session, but it is not this session's named deliverable — no opportunistic edits. It is a candidate for a future content tidy (recorded in the retro's outstanding items).
- **Do not re-open resolved decisions.** The advisory-is-advisory design (the validate query never guards checkout), the store-scoped-DCB choice (ADR 024), and the deferred standalone Promotions service are locked; the Purpose *records* them, it does not re-litigate them.
- **No live boot.** A docs/spec tidy — verification is `openspec list`/`validate` + a re-read of the Purpose, not an Aspire stack. (The slice-6.2 visual W2/W3 browser-verify remains separately deferred per the handoff; it is not this tidy's job.)
