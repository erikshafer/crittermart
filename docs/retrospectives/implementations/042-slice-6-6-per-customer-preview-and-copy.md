# Retrospective: Implementations 042 — Slice 6.6 Per-Customer Coupon Preview + Tailored Refusal Copy

**Prompt**: [`docs/prompts/implementations/042-slice-6-6-per-customer-preview-and-copy.md`](../../prompts/implementations/042-slice-6-6-per-customer-preview-and-copy.md)
**Date**: 2026-07-20
**Kind**: per-slice implementation (Orders BC — Promotions lane)
**OpenSpec change**: `slice-6-6-per-customer-preview-and-copy` (`coupon-promotion`: 2 MODIFIED + 1 ADDED) — `design.md` + `tasks.md` authored this session; `openspec validate --strict` green
**Outcome**: shipped as planned. **No amendment to the model was required.**

## Outcome summary

Slice 6.6 landed exactly as drawn in PR #160. Both redemption events gained an optional defaulted `customerId`; `CustomerCouponUsageView` shipped as CritterMart's third multi-stream projection (inline, keyed `"{couponId}|{customerId}"`); `GET /coupons/{code}/validate` became optionally authenticated with a fourth status; and the `409` refusal was reworded with its status code and title token untouched.

**Tests: 206 backend passing** (137 Orders — 7 new integration + 3 new pure-fold — plus Catalog 9, Inventory 31, Identity 26, CrossBc 3), **125 client unit tests passing** (+2 new). The single failing client *file* is `e2e/seeder.spec.ts`, a Playwright spec vitest collects and cannot run; verified pre-existing by stashing the change and re-running (123 passing before, 125 after, same one failure). `tsc --noEmit` clean.

This closes Workshop 003 entirely — all six slices built.

## What worked

- **The handoff's "do not re-derive" section paid for itself.** Every technical fact it carried — the `customerId`-before-projection ordering, the `partial` requirement, the composite key shape, the auth pattern, the four spec'd-behaviors-that-look-like-bugs — was accurate and load-bearing. Zero re-derivation, zero surprises in the areas it covered. The design/implementation handoff split (retro docs/018's innovation) is validated: a design session that ends by writing down what the *next* session must not rediscover is worth the extra paragraph.
- **The five-step sequence held with every step green.** Events → projection → query → copy → frontend was the right dependency order, and the "hard prerequisite" framing of step 1 (not merely "do this first") made the ordering self-explaining in `tasks.md`.
- **Spec'd-but-counterintuitive behaviors were cheap to implement *because* they were spec'd.** The anonymous-caller-still-sees-`valid` and the forward-only-under-warn cases would each have read as a bug mid-implementation. Having a written scenario for both meant they were written as *tests* on first pass rather than discovered, questioned, and re-litigated. This is the strongest argument yet for the spec layer paying rent.
- **Mirroring an existing type wholesale.** `CustomerCouponUsageView` is `CouponUsageView` with one identity change. Writing it as a deliberate structural mirror — same file layout, same comment shape, same registration site — made it reviewable at a glance and left the *interesting* difference (member-vs-tag routing) as the only thing a reader has to think about.

## What was harder than expected

- **Nothing was harder. Two things were *easier*, and that is worth recording as a mis-estimate.**
  - **The frontend was half the work the proposal budgeted.** The proposal's Impact listed "send the bearer token on the validate call when signed in" as client work. It was already done — `fetchParsed` → `authHeaders(ctx)` has attached the bearer to *every* call since the ADR 023 cutover (Convention 4). The real client delta was two lines: widening the closed `CouponStatusSchema` enum and adding a copy branch. **The one genuinely load-bearing client change was the enum** — it is closed by design (a zod `z.enum` guarding the boundary), so without widening it the new status would fail parsing and surface as *"We couldn't check that code"* rather than the new copy. A generic proposal-Impact line ("frontend: render the new state") would have hidden that the enum, not the copy, was the thing that could break.
  - **`AppendCouponRelease` needed no signature change.** It has taken `customerId` since 6.5 for the composite tag; only the event construction inside it moved. A prior slice threading a value for one purpose made the next slice's use of it free.
- **One genuine judgment call the artifacts did not pre-answer:** whether to set `CustomerId` on *every* redemption or only per-customer ones. Chose unconditional (design.md decision 1) — who redeemed is a plain fact about the event, and gating it would create a *second* forward-only cliff if a coupon's `oneRedemptionPerCustomer` flag were ever flipped on later. Policy-scoping belongs at the query, where it is one `if`, not baked into history.

## Methodology refinements that emerged

1. **A proposal's Impact section is an inventory, not an estimate — and the implementation session should expect to *shrink* it.** Two of this change's listed impacts (the bearer token; `AppendCouponRelease`'s signature) were already satisfied by prior slices. The design session cannot always know that without reading the code at implementation depth. **Refinement:** when an implementation session finds an Impact line already satisfied, record it in the retro rather than silently skipping — it is evidence about where the design/implementation seam actually sits, and it stops the next reader assuming the work was overlooked.
2. **When a shipped helper *almost* fits, the reason it does not is worth a code comment, not just a design note.** `user.CustomerId()` throws on an absent `sub` — correct behind `[Authorize]`, wrong for an optionally-authenticated route where anonymous is the *normal* case. That divergence is a magnet for a well-meaning future "cleanup," so the reason lives at the call site in `ValidateCoupon.cs`, not only in `design.md`. **Candidate encoding:** a general convention that *deliberate* divergences from a shared helper are commented where they occur.
3. **"Decide it or defer it explicitly — silence is not an option" worked, and the decision came out sharper than expected.** Both deferred UI questions (an anonymous per-customer badge; a sign-in nudge) turned out to be **declined by spec constraint**, not taste: each requires the anonymous validate response to carry the policy flag, and the spec pins that response *identical to slice 6.2*. Forcing an explicit answer surfaced a structural reason that a "we'll skip it for now" would have left buried. **Refinement:** when a design session parks a UI call as "no model consequence," the implementation session should test that claim against the spec before accepting it — here the claim was half true (no *model* consequence, but a real *contract* consequence).
4. **An unattributed-bucket is preferable to a routing filter.** Pre-6.6 events fold into a `"{couponId}|"` document no query ever constructs. Filtering them out of the projection was considered and rejected: it buys nothing and creates a *second* place the forward-only rule is stated, free to drift out of step with the query that enforces it. **Principle:** prefer one enforcement point plus a harmless artifact over two enforcement points that must agree.

## Outstanding items / next-session inputs

- **Archive the OpenSpec change** — `slice-6-6-per-customer-preview-and-copy` → `openspec/changes/archive/` (the CLI stamps the date). Per the established convention this is a **separate later PR**, not this one.
- **Cadence:** this is implementation PR **#1** of a fresh 2–3 budget against Orders (PR #160 was the design-return interleave). No design-return owed before the next one.
- **Workshop 003 is fully built.** The next Promotions work is §8's long road: the **shared discount budget** DCB (third variant, ADR 024), coupon lifecycle (expiry/disable/edit), and Promotions graduating to its own service. None started.
- **Unchanged carry-forwards:** the doc-tidy (context-map auth-cutover staleness); slice-6.2 visual browser-verify (deferred #146); live-verify on the full Aspire stack for 6.5/6.6 (offered, never run); two remote branches awaiting delete/keep; dependabot #132–139 re-triage; `UseDurableLocalQueues()` / `ReplenishTimeout` verification gaps; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16). **POST-TALK:** delete the AppHost demo knobs.
- **Not a defect, do not "fix":** the preview under-warns for redemptions predating the `customerId` member. Spec'd, tested, and one-sided by construction.

## Spec-delta — landed?

**Yes.** The prompt named: satisfy the landed OpenSpec change, author its missing `design.md` + `tasks.md`, flip Workshop 003 to IMPLEMENTED with §8 item 6's two open questions **answered**, and bump Narrative 011's Moment 6 from *modeled* to running.

All four landed. `design.md` (7 decisions) + `tasks.md` (27 tasks, all checked) were the session's first act; `openspec validate --strict` is green. Workshop 003 → **v1.5** (status line, §8 item 6 flipped to IMPLEMENTED with both UI questions answered and declined-with-reasons, Document History row). Narrative 011 → **v1.4** (frontmatter, reference line, Moment 6 heading, the "leaves out" bullet rewritten from *modeled-not-built* to the forward-only under-warn asymmetry, Document History row). The OpenSpec change itself is unamended — the model as drawn survived contact with the code, which is the outcome the design session was aiming for.
