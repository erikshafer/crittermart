---
retrospective: 016
kind: docs
prompt: docs/prompts/docs/016-design-return-archive-slice-6-2-coupon-promotion-purpose.md
deliverable: openspec/changes/archive/2026-07-16-slice-6-2-advisory-coupon-validate/ (archived; coupon-promotion 4→5), openspec/specs/coupon-promotion/spec.md (real ## Purpose over the archive-stamped TBD), docs/prompts/README.md + docs/retrospectives/README.md (docs count 15→16)
date: 2026-07-17
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 016: Design-Return — Archive `slice-6-2-advisory-coupon-validate` + write the real `coupon-promotion` `## Purpose`

## Outcome summary

The **design-return cadence interleave** owed after the two Promotions implementation PRs since the ADR-024 design PR (#144 the DCB core, #146 the slice-6.2 advisory preview). One `tidy:` PR drains the last satisfied OpenSpec change into the main spec and gives the `coupon-promotion` capability the real Purpose the #145 archive left as a TBD.

- **`openspec archive 2026-07-16-slice-6-2-advisory-coupon-validate`** — folded its ADDED requirement (*Validate and price a coupon at cart review*) into `openspec/specs/coupon-promotion/spec.md` (**4 → 5**) and moved the change to `archive/`. The CLI double-prefixed the archive dir (it unconditionally prepends today's date to the already-date-named change → `2026-07-17-2026-07-16-…`); renamed to the single-date sibling convention `2026-07-16-slice-6-2-advisory-coupon-validate`.
- **`coupon-promotion` `## Purpose`** — replaced the archive-stamped *"TBD - created by archiving change 2026-07-16-slices-6-1-6-3-6-4-coupon-dcb…"* with a real, sibling-caliber paragraph: the coupon-definition streams + `CouponView`, CritterMart's first store-scoped Marten **DCB** cap enforcement (ADR 024), the tagged `CouponRedeemed`/`CouponRedemptionReleased` appended to the *order* stream, the **advisory-vs-authoritative** teaching contrast (`CouponUsageView` + the validate query never gate — the DCB read is the sole authority), the deferred standalone Promotions service, and the slice→PR map (6.1/6.3/6.4 → #144, 6.2 → #146).
- **Index READMEs** — `docs/` count 15 → 16 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, population note extended.

**Verification**: `openspec list` → **No active changes found**; `openspec validate coupon-promotion --specs --strict` → 6 passed / 0 failed, with the 5th requirement (*Validate and price a coupon at cart review*) confirmed present. No code, no tests, no live boot (a docs/spec tidy).

## What worked

- **The fork was worth surfacing rather than assuming.** The handoff framed the Purpose write as riding the mechanical archive tidy, but the tidy-ceremony rule explicitly names "a spec `## Purpose`" as spec-content authoring that carries the full pair. Calibrating against the *actual* sibling Purposes (dense, multi-clause paragraphs — not one-liners) confirmed the write was genuine content, and Erik chose "Full pair, one PR." Presenting the shape as a decision kept the PR honest to the encoded rule rather than quietly bending it.
- **`openspec archive` did the spec sync deterministically.** Letting the CLI fold the delta into the main spec (and move the change) is the tool-backed path — no hand-editing of `openspec/specs/*`. `-y` cleared the non-interactive-shell prompt; the archive reported `Task status: ✓ Complete` (all of the change's tasks were already ticked in #146).
- **Writing the Purpose from the shipped requirement set, not the proposal, kept it accurate.** With all five requirements in front of me, the Purpose could name the real read models (`CouponView`, `CouponUsageView`), the real DCB call shape (`FetchForWritingByTags<CouponUsage>`), and the real slice→PR provenance — a modeling-time proposal could not have.

## What was harder / notable

- **The `openspec archive` CLI is not idempotent about the date prefix.** It unconditionally prepends today's date, so a change dir that was *itself* date-named (`2026-07-16-slice-6-2-…`, unlike bare-named siblings such as `harden-add-to-cart-snapshot`) archives to a double-dated `2026-07-17-2026-07-16-…`. Corrected by renaming to the single-date convention. **Methodology note for future authoring: name active changes *without* a date prefix and let the archive stamp it once** — slice 6.2's active dir was the anomaly, not the archive.
- **A second latent TBD Purpose surfaced: `customer-registry`.** `openspec/specs/customer-registry/spec.md` still reads *"TBD - created by archiving change customer-registry…"* — the same archive-stamped placeholder pattern. Left untouched this session (out of scope, no opportunistic edits); recorded below as a ready content-tidy candidate.

## Methodology refinements

- **The "archive-stamped TBD Purpose" is a recurring debt the archive CLI *creates*, and draining it is legitimate design-return content.** When a capability's very first main spec is born by archiving a change, the CLI seeds a TBD Purpose. That TBD is invisible to `--strict` validation (it passes), so it lingers until a human writes it. This session establishes the pattern: **write the real Purpose in the design-return that archives the capability's storefront-completing slice**, when the full requirement set is finally visible. `customer-registry` is the next instance awaiting the same treatment.
- **A design-return tidy can be *both* mechanical and content-bearing in one PR — the ceremony level is set by the heaviest thing it writes.** This PR did a mechanical archive (light) *and* authored a Purpose (content). Per the tidy-ceremony rule the content half dictates: full pair. The mechanical archive rides along inside the pair, exactly as docs/015 rode two archives inside its workshop-amendment pair.

## Outstanding / next-session inputs

- **Cadence reset — the counter is clear.** This interleave satisfies the design-return owed after #144/#146; the **next PR is an open Promotions implementation pick** (the long-road slice) or the next BC. The OpenSpec workspace is at **0 active changes** — a clean baseline for the next proposal.
- **`customer-registry` TBD Purpose** — a ready content-tidy candidate (write its real Purpose over the archive-stamped TBD, same as this session did for `coupon-promotion`). Small, self-contained, a natural rider on the next design-return.
- **Deferred from #146 (unchanged, non-blocking):** the **visual W2/W3 browser-verify** — slice 6.2's live-verify was API-level only (all three validate states + the cap flip + W3 fields confirmed on the Aspire stack; the Claude-in-Chrome extension was unconnected, so the SPA drive + GIF are unproven in a real browser). UI rendering is covered by the 123 client Vitest tests. To close: connect the extension, boot the stack (demo-runbook Step 1 + 5d), drive `http://localhost:5273`.
- **Carry-forwards (unchanged, non-blocking):** two remote branches await delete/keep (`origin/feat/cart-identity-harmonization`, `origin/research/cw-telemetry-spike`); dependabot #132–139 re-triage; `UseDurableLocalQueues()` saga-timeout + `ReplenishTimeout` verification gaps; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) deferred. **POST-TALK:** delete the AppHost demo knobs (payment decline/delay, order timeout, replenish timeout). **Locked/standing:** Wolverine stays **6.19.0** ([[critterwatch-wolverine-version-coupling]]); Marten **9.15.1**; CritterWatch trial **expired 2026-07-10** (console blocked); advisory-is-advisory (the validate query never guards checkout); frontend units run with `--exclude "**/e2e/**"`.

## Spec-delta — landed?

**Named delta landed.** The prompt named: the **`coupon-promotion` 4→5** main-spec sync via `openspec archive` (the ADDED *Validate and price a coupon at cart review* requirement) **plus** the capability's real **`## Purpose`** over the TBD. Both landed: `openspec validate coupon-promotion --specs --strict` confirms the 5th requirement present and the spec valid; `openspec list` confirms **No active changes**; the archived change sits under `openspec/changes/archive/2026-07-16-slice-6-2-advisory-coupon-validate/`; and `openspec/specs/coupon-promotion/spec.md` now opens with a real Purpose. This is a **design-return reconciliation** closing the handoff-named post-merge archive + the #145-created TBD — four-step closure: **prompt named → session executed → this retro confirms → the main spec recorded.** This tidy authored spec content (the Purpose), so it carried the full prompt/retro pair per the tidy-ceremony rule, resolved as a genuine fork at session start.
