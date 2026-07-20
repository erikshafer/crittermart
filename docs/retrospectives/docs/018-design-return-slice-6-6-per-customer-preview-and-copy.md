# Retrospective: Docs 018 — Design-Return: Model slice 6.6 (per-customer advisory preview + tailored refusal copy)

**Prompt**: [`docs/prompts/docs/018-design-return-slice-6-6-per-customer-preview-and-copy.md`](../../prompts/docs/018-design-return-slice-6-6-per-customer-preview-and-copy.md)
**Date**: 2026-07-20
**Kind**: design-return (spec-content authoring — workshop + narrative amendments as siblings, then the OpenSpec proposal). **No code.**
**Deliverables**: Workshop 003 → **v1.4**; Narrative 011 → **v1.3**; OpenSpec change `slice-6-6-per-customer-preview-and-copy` (`coupon-promotion`: 2 MODIFIED + 1 ADDED, `--strict` clean); both index READMEs `docs/` 17→18; this retro.
**Predecessor**: [`docs/handoffs/coupon-per-customer-preview-and-409-copy.md`](../../handoffs/coupon-per-customer-preview-and-409-copy.md) — the research/validation session that confirmed both follow-ons unbuilt and deferred the design layer here.

## Outcome summary

Slice 6.5's two parked follow-ons are now modeled, narrated, and specified as **slice 6.6** — the storefront half of the per-customer coupon rule. The slice writes nothing (the only such slice in Workshop 003) and changes no invariant: the composite DCB append at checkout remains the sole authority. What it adds is a **read** (a per-customer advisory preview on the cart-review validate query) and a **sentence** (customer-facing copy for the `409 CouponAlreadyRedeemedByCustomer` refusal).

Both of the handoff's open questions were resolved with the owner via `AskUserQuestion` rather than inherited:

1. **One slice, not two.** Preview and copy are one promise told at two moments of one screen; the copy half alone has no command, no event, and no view, so splitting it produces a degenerate slice.
2. **Enrich the existing `/validate` route** as optionally-authenticated, over an `[Authorize]`-gated sibling route or a hard-`[Authorize]` cutover of a shipped anonymous endpoint. The anonymous contract stays pinned byte-for-byte; an authenticated caller gains one status, `already_redeemed`.

## What worked

- **The handoff's "Technical grounding" section paid for itself.** Four facts (the anonymous endpoint at `ValidateCoupon.cs:35-49`, `CustomerCouponUsage` being a throwaway boundary aggregate, the inline `409` copy site, and the spec headroom in both affected requirements) were carried forward verbatim and none needed re-deriving. The session's entire code-reading budget went to *one* question the handoff hadn't asked.
- **Reading the code rather than trusting the doc summary caught the design's load-bearing constraint** (below). The handoff correctly said "a preview needs a genuinely new persisted, queryable projection"; it did not know *why that projection could not be written yet*.
- **Sibling-first ordering held its shape.** Workshop (the slice and its GWTs) → narrative (the journey) → OpenSpec (the contract). Each later artifact had a settled vocabulary to draw on, and the proposal's "Why" was almost transcription by the time it was written.
- **The existing `CouponUsage` / `CouponUsageView` pair was a ready-made template** for the new `CustomerCouponUsage` / `CustomerCouponUsageView` pair — same arithmetic, different existence. The advisory-vs-authoritative distinction did not have to be re-argued, only re-applied to the personal boundary.

## What was harder than expected

- **The `(coupon × customer)` pair was not projectable.** Through slice 6.5 the pair existed **only** as a DCB tag. A tag is a write-side query mechanism; a Marten `MultiStreamProjection` groups by an **event member**. Neither `CouponRedeemed` nor `CouponRedemptionReleased` carries a `customerId` — so the persisted view a preview requires could not be built from the events as they stood. This is the session's central finding and it reshaped the slice: what looked like "add a projection and read it" became "amend two event contracts, then add a projection." It is recorded in Workshop 003 §4 as a block-quoted note rather than a footnote, because a future session reaching for a per-customer read model will hit exactly this wall.
- **The consequence had to be modeled honestly rather than waved through.** Adding `customerId` as an optional defaulted field (the established non-breaking evolution) leaves pre-6.6 redemptions unattributable, so the new view is **forward-only** and the preview can **under-warn**. The temptation was to leave that in prose; instead it became its own §6.6 GWT scenario and its own OpenSpec scenario, so it gets tested rather than discovered. The framing that made it acceptable: the error is **one-sided by construction** — the preview may fail to warn, but can never wrongly accuse — and a one-sided advisory failure degrades to exactly today's behavior.
- **Precedence turned out to be a real design decision, not a detail.** `already_redeemed` must outrank `exhausted`, mirroring checkout, because the two reasons send a Customer to *different remedies* ("try again later" vs. "try another code"). Getting that argument into the narrative rather than only the spec took a rewrite; it is the sentence that explains why the preview owes the Customer a *reason*, not just a verdict.

## Methodology refinements that emerged

- **A handoff that validates current state against code — not docs — is worth its length.** This one was authored by a session that wrote no artifacts at all, and it made this session roughly a third shorter. Worth repeating before any design session whose subject shipped several PRs ago.
- **"Resolve, don't inherit" scoping questions belong in the handoff, unanswered.** The one-slice-or-two fork was explicitly flagged as the next session's call, with the tradeoff sketched but no verdict. That is the right shape: enough context to decide quickly, no pre-commitment to relitigate.
- **A parked follow-on should record what it does *not* know.** Slice 6.5's parking note said the preview "would need the caller's identity + a per-customer advisory view" — accurate, and it still missed the event-shape blocker. A parking note that had asked "and is that view projectable from the events as they stand?" would have surfaced the constraint a session earlier. Not a process change, but a good question to ask when parking any read-model follow-on.

## Outstanding items / next-session inputs

- **Slice 6.6 implementation session.** The change carries `proposal.md` + the spec delta only; `design.md` and `tasks.md` ride the implementation session per CLAUDE.md §4a. `openspec list` shows the change with "No tasks" — expected, not an omission.
- **Two UI calls deliberately left to that session** (Workshop 003 §8 item 6): whether to badge the `oneRedemptionPerCustomer` policy for anonymous shoppers, and whether to nudge a signed-out shopper to sign in for a sharper answer. Both have the data already and no model consequence.
- **Design-return cadence** is satisfied by this PR — it is the "next narrative / next workshop pass" branch, following PR #149.
- **Unchanged carry-forwards** from the handoff, none touched here: the shared-budget DCB (third ADR 024 variant), the doc-tidy (context-map auth-cutover staleness), the slice-6.2 visual browser-verify, the slice-6.5 live Aspire verify, dependabot #132-139 re-triage, the two remote branches, and the stale local `feat/slice-6-5-per-customer-redemption-dcb`. **POST-TALK:** delete the AppHost demo knobs.

## Spec delta — landed?

**Yes, in full.** The prompt named: one new workshop slice with GWTs, one §5.1 wireframe delta, one new §7 read model, a §4 vocabulary amendment, one new narrative Moment, and a `coupon-promotion` delta of +1 ADDED / +2 MODIFIED.

All landed. **Workshop 003 v1.4**: slice 6.6 table row, §1 in-scope bullet, §4 event-vocabulary amendment (`customerId` on both redemption events, with the tags-are-not-groupable note), §5 slice-count and pattern-citation updates, §5.1 W2 already-used affordance + reworded `409` detail, a nine-scenario §6.6 GWT set, §7 `CustomerCouponUsageView`, §8 item 6 retired-and-forwarded, §9 history row. **Narrative 011 v1.3**: journey-scope bullet, Moment 6, three replacement leaves-out bullets, Forthcoming Moments update, history row. **OpenSpec** `slice-6-6-per-customer-preview-and-copy`: *Validate and price a coupon at cart review* MODIFIED (+6 scenarios), *Enforce one redemption per customer* MODIFIED (copy delta only, mechanic and every existing scenario untouched), *Track advisory per-customer coupon usage* ADDED (4 scenarios); `--strict` clean.

One item landed **beyond** the named delta: the §4 forward-only consequence became a modeled GWT scenario in both the workshop and the spec, rather than the prose caveat the prompt anticipated. Scope was not otherwise exceeded — no code, no ADR, no `design.md`/`tasks.md`.
