# Retrospective: Docs 019 — Archive slice 6.6 + refresh the `coupon-promotion` Purpose

**Prompt**: [`docs/prompts/docs/019-archive-slice-6-6-and-refresh-coupon-promotion-purpose.md`](../../prompts/docs/019-archive-slice-6-6-and-refresh-coupon-promotion-purpose.md)
**Date**: 2026-07-20
**Kind**: tidy — post-merge OpenSpec archive + spec content (full prompt/retro pair per the tidy ceremony rule)
**Outcome**: shipped as planned.

## Outcome summary

`slice-6-6-per-customer-preview-and-copy` archived to `openspec/changes/archive/2026-07-20-slice-6-6-per-customer-preview-and-copy/`. The CLI folded **+1 ADDED, ~2 MODIFIED** into `openspec/specs/coupon-promotion/spec.md` — exactly the shape the proposal declared — taking the capability **6→7 requirements**. Workspace back to **0 active changes**; `openspec validate --all --strict` green across all six specs.

The capability's `## Purpose` was rewritten from a slices-6.1–6.4 description to one covering the capability as it now stands. No requirement text was authored or edited by hand.

## What worked

- **Checking the fold against the proposal's *declared* counts, rather than eyeballing the diff.** The proposal said +1 ADDED / 2 MODIFIED; the CLI reported `+ 1 added, ~ 2 modified`. Matching those two numbers is a cheap, specific check that the change that shipped is the change that was reviewed. Eyeballing a 60-line spec diff would have been slower and less conclusive.
- **Verifying content, not just structure.** Beyond the counts: `already_redeemed` appears 10× in the main spec, the reworded `409` copy is present, and the old mechanical sentence (*"may be redeemed only once per customer, and you have already redeemed it"*) returns **zero** matches. Structural validation alone would not have caught a fold that dropped the copy change — the requirement would still parse.
- **The ceremony rule made the light-vs-full call mechanically, once the Purpose was inspected.** No agonizing: CLAUDE.md names "a spec `## Purpose`" explicitly as spec content. The rule's value is that it converts a judgment call into a lookup — *provided* someone actually opens the Purpose and reads it.

## What was harder than expected

- **Nothing was hard. One thing was overdue, and that is the finding.** The Purpose was stale by **two** slices, not one. The 6.5 archive (PR #150) folded its requirement deltas correctly and left the Purpose untouched — so the capability's front door had been describing a one-DCB capability while the spec below it described two, since 2026-07-18. Nobody was wrong at any single step; the drift accrued because **nothing in the archive flow prompts a Purpose check.** That is a process gap, not an oversight by a prior session.
- **The staleness was materially misleading, not merely dated.** It called the checkout DCB read "the sole authority" — singular — when there are now two boundaries, and listed `CouponDefined { code, discountPercent, cap }` without `oneRedemptionPerCustomer`. A reader orienting from the Purpose would have formed a wrong model of the capability's central architectural claim, which is exactly the thing the Purpose exists to convey.

## Methodology refinements that emerged

1. **The archive flow needs a Purpose check, and it belongs in the flow — not in a session-runner's memory.** This is the second consecutive coupon-promotion archive where the Purpose needed attention, and the first where nobody noticed until a slice later. The recurring shape is: *run the CLI → check the Purpose against the shipped slice inventory → let that answer decide light-vs-full ceremony*. Erik chose to **encode this as its own `tidy: encode-` session** rather than fold it in here — correct, since encoding a convention is itself spec content and would have doubled this session's scope. **Logged as a next-session input below.**
2. **A capability's slice inventory is the cheapest staleness detector available.** The Purpose ends with an explicit list ("slices 6.1/6.3/6.4 … 6.2 … 6.5 … 6.6"). Comparing that list against the archive directory takes seconds and catches exactly the drift that accrued here. Whatever the encoded checklist ends up saying, this comparison should be in it — it is a mechanical check with no judgment required.
3. **"Verify the fold, don't assume it" earned its place in the prompt.** The instinct after a clean CLI exit is to move on. Writing the verification into the prompt's Goal as a named deliverable — with the specific assertions to make — meant it happened before the hand-editing started, when a discrepancy would still have been cheap to act on.
4. **Confirmed again: separating CLI-folded requirement text from session-authored prose keeps the review honest.** The prompt's out-of-scope line ("no requirement edits by hand — if the fold looks wrong, that is a finding for the retro, not a silent hand-fix") means a reviewer can trust that every SHALL in the diff came from an already-reviewed change, and that the only human-authored words are in the Purpose. Worth keeping as standing archive-session language.

## Outstanding items / next-session inputs

- **Encode the archive-tidy shape** (`tidy: encode-archive-purpose-check` or similar) — the recurring three-step flow above, plus the slice-inventory comparison as its mechanical staleness detector. **Decided with Erik this session; explicitly deferred to its own session.** This is the highest-value carry-forward here: it converts a gap that has now bitten once into a checklist.
- **Formatting nit, deliberately not fixed:** the archive CLI emits `## Requirements` with no blank line after the Purpose paragraph. It regenerates on every archive, so hand-fixing it is churn. If it ever matters, it is an upstream CLI issue, not a repo one.
- **Cadence:** this tidy is the design-return-shaped interleave after implementation PR #161. The 2–3 budget against Orders resets.
- **Workshop 003 is fully built** (all six slices). The next Promotions work is §8's long road: the **shared discount budget** DCB (third variant, ADR 024), coupon lifecycle (expiry/disable/edit), and Promotions graduating to its own service. None started.
- **Unchanged carry-forwards:** the context-map auth-cutover doc-tidy; slice-6.2 visual browser-verify (deferred #146); live-verify on the full Aspire stack for 6.5/6.6 (offered, never run); two remote branches awaiting delete/keep; dependabot #132–139 re-triage; `UseDurableLocalQueues()` / `ReplenishTimeout` verification gaps; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16). **POST-TALK:** delete the AppHost demo knobs.

## Spec-delta — landed?

**Yes.** The prompt named two things: fold slice 6.6's deltas into `coupon-promotion` via the CLI, and rewrite the capability's `## Purpose` to cover the capability as it now stands.

Both landed. The fold matched the proposal's declared shape exactly (+1 ADDED, ~2 MODIFIED; 6→7 requirements), verified structurally (`--strict` green, 0 active changes) **and** by content (the new status and copy present, the superseded sentence absent). The Purpose now describes: two DCBs and the count-vs-existence contrast between them; the composite single-scalar tag and why it is not two tags AND-ed; advisory-vs-authoritative at both scopes, each view with its never-persisted boundary twin; the optionally-authenticated validate query, its precedence ordering, and its forward-only under-warn asymmetry; and a slice inventory extended through 6.6. No requirement text was hand-authored — the prompt's out-of-scope line held.
