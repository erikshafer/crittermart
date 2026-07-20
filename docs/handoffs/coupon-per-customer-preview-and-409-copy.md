# CritterMart — Handoff: parked slice-6.5 follow-ons (per-customer preview + tailored 409 copy)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-20.
> Direct successor to [`post-second-dcb-next-direction.md`](post-second-dcb-next-direction.md) — resolves its
> "Immediate next job" fork: the owner picked **option 2** (parked 6.5 follow-ons) over the shared-budget
> DCB, the doc-tidy, and the slice-6.2 visual verify. Those three remain on the table, unstarted, for later.
> Read the artifacts, not this doc, for detail:
> [Workshop 003 §6.5 / §8 item 6](../workshops/003-promotions-event-model.md),
> [Narrative 011 Moment 5 / "still leaves out"](../narratives/011-customer-redeems-coupon.md),
> [`coupon-promotion` spec](../../openspec/specs/coupon-promotion/spec.md),
> [retro implementations/041](../retrospectives/implementations/041-slice-6-5-per-customer-redemption-dcb.md).

## Where things stand

- **No code has been written yet.** This session did research/validation only — confirmed both parked items
  are still accurate and unbuilt (not stale, not quietly already shipped). Nothing has been implemented.
- **The two items**, per Workshop 003 §8 item 6:
  1. **Per-customer advisory preview** — the anonymous `GET /coupons/{code}/validate` query would need the
     caller's `sub` plus a per-customer advisory view, so a customer can see in advance whether *they*
     specifically have already redeemed a per-customer coupon.
  2. **Tailored 409 copy** — the `CouponAlreadyRedeemedByCustomer` refusal's message is currently a generic
     inline template, not customer-tailored.
- **Next step is the design layer, not code**: a Workshop 003 amendment + Narrative 011 amendment (siblings),
  then an OpenSpec proposal amending the `coupon-promotion` capability — **per the owner, explicitly not
  this session**. A fresh session picks this up.

## Immediate next job

Author the workshop amendment and narrative amendment as siblings (per this repo's per-slice loop), then the
OpenSpec proposal, then stop — implementation is a later prompt/session, not this one.

**Open scoping question the next session must resolve, not inherit an answer to:** one slice or two? Item 1
(preview) is the heavier lift — a new auth-gated endpoint behavior *and* a new persisted read model. Item 2
(copy) is a small edit to an existing handler. They could land as one workshop slice (e.g. 6.6) or split
across two. Decide this when drafting the workshop delta, not before.

## Technical grounding — carry these forward (do not re-derive)

Validated against the actual code this session (not assumed from docs):

- **`GET /coupons/{code}/validate`** — `src/CritterMart.Orders/Features/ValidateCoupon.cs:35-49`. Anonymous,
  no `[Authorize]`, handler takes no `ClaimsPrincipal`/customer id. Reads only `CouponView` (definition) and
  the **global** `CouponUsageView` (net count) — no per-customer state read at all today.
- **No shortcut read model exists.** The obvious candidate, `CustomerCouponUsage`
  (`src/CritterMart.Orders/Promotions/CustomerCouponUsage.cs`), is a **throwaway DCB boundary aggregate**
  (`[BoundaryAggregate]`, id-less) — its own header comment states it is "NEVER persisted, NEVER queried by
  the UI." A preview needs a genuinely new **persisted, queryable** projection — e.g. a
  `CustomerCouponUsageView` folding `CouponRedeemed`/`CouponRedemptionReleased` keyed by
  `(couponId, customerId)`.
- **Shape it as existence, not count.** Retro 041's model correction: the per-customer verdict is an
  *existence* fact ("has this customer, across any past order, redeemed this coupon"), not a count — don't
  reuse the count-shaped pattern from the global `CouponUsageView`.
- **Auth pattern to mirror**: `PlaceOrder.cs` already pulls the customer id via the `CustomerIdentity` helper
  (`src/CritterMart.Orders/Auth/CustomerIdentity.cs`, `user.CustomerId()`). The preview endpoint should
  reuse the same pattern, gated by `[Authorize]`.
- **The 409 copy today** — `src/CritterMart.Orders/Features/PlaceOrder.cs:179-189`, inline
  `Results.Problem(title: "CouponAlreadyRedeemedByCustomer", detail: $"Coupon '{coupon.Code}' may be
  redeemed only once per customer, and you have already redeemed it.", statusCode: 409)`. Same bare inline
  pattern as sibling `CouponInvalid`/`CouponExhausted` branches — no ProblemDetails middleware/exception
  mapping layer exists to hook a richer message into; any copy change is a literal edit at this call site
  (or a new one, if the two follow-ons split across slices).
- **Spec headroom**: `openspec/specs/coupon-promotion/spec.md` — the validate requirement (lines 121-144)
  has no customer-identity wording anywhere (confirms global-only is the current *spec'd* behavior, so
  adding customer-awareness is a real spec delta, not a bug fix). The 409 requirement (lines 146-180) pins
  only the status code and title token, not the detail copy — so a copy change doesn't contradict the
  existing spec, it just isn't addressed by it yet.
- **ADR 024 says nothing about either item** — it only covers the DCB *enforcement* mechanic (the count and
  existence checks at checkout). These follow-ons live purely at the workshop/narrative/retro layer; no ADR
  amendment is expected unless the new read-model design surfaces a decision worth recording.
- **Retro 041 frames both as scope discipline, not bugs or blockers** — slice 6.5's mandate was the
  checkout-time DCB mechanic; preview/copy were deliberately punted as separate storefront-UX work. No
  technical obstacle forced the deferral.

## Suggested skills for the next session

- **`event-modeling`** (local skill) — for the Workshop 003 slice amendment + GWT authoring.
- **`marten-projection-conventions`** (local skill) — the new `CustomerCouponUsageView` is a fresh Marten
  projection; use this for the inline-snapshot-projection conventions this repo has settled on.
- **`openspec-propose`** — to author the OpenSpec change amending `coupon-promotion` (no date prefix; the
  archive CLI stamps it on archive).
- **`domain-modeling`** — if naming/vocabulary for the new read model needs sharpening before it's spec'd.

## Files to read first

- `docs/workshops/003-promotions-event-model.md` (§6.5, §8 item 6)
- `docs/narratives/011-customer-redeems-coupon.md` (Moment 5, "What the journey still leaves out")
- `openspec/specs/coupon-promotion/spec.md` (validate requirement + per-customer enforcement requirement)
- `docs/retrospectives/implementations/041-slice-6-5-per-customer-redemption-dcb.md`
- `src/CritterMart.Orders/Features/ValidateCoupon.cs`
- `src/CritterMart.Orders/Features/PlaceOrder.cs` (lines ~179-197, the DCB retry loop's 409 branches)
- `src/CritterMart.Orders/Promotions/CustomerCouponUsage.cs`
- `src/CritterMart.Orders/Auth/CustomerIdentity.cs`

## Deferred / carry-forwards (unchanged from prior handoff, non-blocking)

- The other three options from the prior fork, not chosen this round: the **shared discount budget** DCB
  (third variant, ADR 024), the **doc-tidy** (context-map auth-cutover staleness + `customer-registry` TBD
  `## Purpose`), and **slice-6.2 visual browser-verify** (deferred #146).
- Live-verify on the full Aspire stack for slice 6.5 was offered, not yet run.
- Two remote branches await delete/keep; dependabot #132-139 re-triage; `UseDurableLocalQueues()` /
  `ReplenishTimeout` verification gaps; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) deferred.
  **POST-TALK:** delete the AppHost demo knobs.
- Stale local branch `feat/slice-6-5-per-customer-redemption-dcb` — safe to delete (merged-and-squashed).

## Locked / standing (do NOT re-litigate)

- **Wolverine stays 6.19.0**; Marten **9.15.1**. CritterWatch trial expired 2026-07-10 (console blocked).
- **Advisory stays advisory** — a per-customer preview, once built, is still advisory-only; the checkout-time
  DCB append remains the sole authority. Don't let the preview become load-bearing.
- **PR hygiene:** post the full PR URL as plain visible text; no Claude commit trailer.
- **OpenSpec:** name active changes **without** a date prefix — the archive CLI stamps the date on archive.
- **Design-return cadence:** slice 6.5 already partially satisfied a design-return (workshop + narrative
  amended in the same PR). Whether this next slice needs its own dedicated design-return or counts toward
  the 2-3 implementation-PR budget is a call for whoever runs the *implementation* session — out of scope
  for the workshop/OpenSpec session this handoff targets.
