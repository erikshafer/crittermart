# Prompt: Event Modeling Workshop 003 — CritterMart Promotions (DCB-Protected Coupon Redemption)

**Kind**: pre-code design (workshop) — the first build-facing design step of the Promotions/DCB direction ADR 024 chose
**Files touched**: `docs/workshops/003-promotions-event-model.md` (new); `docs/workshops/001-crittermart-event-model.md` (amend — minimal: § 8 long-road parking-lot item 7 resolved pointer + v1.13 history row); `docs/workshops/README.md` (current-population bump); `docs/retrospectives/workshops/003-promotions-event-model.md` (new); `docs/retrospectives/README.md` (workshops count)
**Mode**: solo multi-persona — Facilitator, Domain Expert, Architect, Backend Developer, Frontend Developer, QA, Product Owner, UX. The artifact captures the *result* of that facilitation, not the transcript.

## Framing

ADR 024 (PR #131) chose the fourth post-round-one increment: coupon redemption at Orders checkout with a **global per-coupon redemption cap** enforced by **Marten DCB inside the Orders store**, Promotions contributing coupon **definitions only** (no fifth service this increment). The decision layer is fully shipped (ADR + context map + `structural-constraints.md` v1.9 + vision § Long road). Per CLAUDE.md, the per-slice loop is gated on an Event Modeling pass — this session is that pass. **Design only, no code.**

Four forks were resolved with Erik (AskUserQuestion with previews) at session start, before authoring:

1. **Workshop location** — a new **Workshop 003** owning the Promotions concept, with its redemption slices drawn in an Orders swim lane; Workshop 001 gets only a minimal resolved-pointer amendment (the Workshop 002 precedent: new concept = new workshop).
2. **Coupon entry point** — the coupon field lives on the **W2 Cart Review screen, UI-held**: a read-model query validates/prices the code, nothing is written until `PlaceOrder` carries it. One write point; the Cart aggregate is untouched.
3. **Definition birth** — a Seller-lane **`DefineCoupon` → `CouponDefined`** slice (CritterMart's first use of the Bruun **configuration-as-events** adjunct pattern), realized round one by the seeder; cap *N* is an event-sourced fact, and the Published-Language graduation path to a future standalone Promotions service stays intact.
4. **Cancellation semantics** — **release on cancel**: `OrderCancelled` on a redemption-carrying stream appends a compensating tagged `CouponRedemptionReleased`; the DCB boundary counts redemptions minus releases, mirroring Inventory's reserve/release symmetry as tag arithmetic.

## Goal

Produce `docs/workshops/003-promotions-event-model.md` (v1.0) — the Promotions event model: events, commands, views, swim lanes (Seller/Promotions, Customer, Orders-with-DCB), vertical slices 6.1–6.4 with reads-from/writes-to lists, and GWT scenarios including the **mandatory cap-breach → redemption-rejected path** and the **`DcbConcurrencyException` race**. Resolve Workshop 001's long-road parking-lot item 7 by pointer.

## Orientation

Read these before authoring:

1. **`docs/handoffs/promotions-dcb-workshop.md`** — the mission doc (this session consumes it).
2. **`docs/decisions/024-dcb-coupon-redemption-in-orders.md`** — the locked decisions (do NOT re-litigate) and the forward questions deliberately left to this pass.
3. **`docs/workshops/001-crittermart-event-model.md`** — format precedent; § 3 Place Order storyboard, § 4 Orders vocabulary, § 5 slice table (global slice numbering: 1.x–4.x taken; Workshop 002 took 5.x → Promotions takes **6.x**), § 6 GWT 4.1/4.4–4.7 (the checkout and cancellation slices redemption attaches to), § 8 parking-lot item 7.
4. **`docs/workshops/002-identity-event-model.md`** — the standalone-workshop precedent and section shape.
5. **`docs/skills/event-modeling/SKILL.md`** — phases, slice/GWT output discipline, adjunct patterns (configuration-as-events is used here for the first time).
6. **`marten-advanced-dynamic-consistency-boundary` skill** — DCB mechanics; defer to it, do not re-derive. Note: it titles DCB as Polecat; CritterMart uses the **Marten 9.11.0** path ADR 024 verified (`FetchForWritingByTags`, `EventTagQuery().Or<CouponId>(id)`, `DcbConcurrencyException`, opt-in `tags TEXT[]` + GIN on the Orders `mt_events`).
7. **`docs/workshops/README.md`** — output discipline + frontmatter `version:` rule.

## Out of scope

- **No code**, no `openspec` change, no schema work — the per-slice loop follows a fresh handoff after this PR merges.
- **No fifth Promotions service**, no cross-service redemption gate (ADR 024 deferred it).
- **No richer DCB variants** (one-redemption-per-customer composite tag, shared discount budget) — parking lot only.
- **No coupon lifecycle beyond definition** (expiry, disable, edit) and **no stacking** (one coupon per order) — parking lot.
- **No edits to Workshop 001 beyond the named minimal amendment**; no context-map edit (ADR 024's PR already resolved § Long road; no new BC ⇒ no map trigger).
- Do NOT touch the Wolverine pin (≤ 6.16.0) or any dependency.

## Output structure

Workshop 003 mirrors 001/002's sections: Frontmatter (v1.0), Scope, BC Summary (Promotions definitions-only + Orders as DCB host), Timeline/Storyboard (coupon journey around the existing Place Order sequence), Event Vocabulary (`CouponDefined`, `CouponRedeemed` [tagged], `CouponRedemptionReleased` [tagged]; the `CouponUsage` DCB boundary state named as NOT-a-persisted-view), Slice Table (6.1 define / 6.2 validate-at-cart-review / 6.3 redeem-at-checkout / 6.4 release-on-cancel), Wireframe amendment (W2 coupon field, W3 discount line — proportional per ADR 016), GWT Scenarios (happy + explicit failures incl. cap-breach and the DCB race), Read Models/Projections (`CouponView`, advisory `CouponUsageView`, `CouponUsage` boundary state), Open Questions/Parking Lot, Document History.

## Working pattern

Solo multi-persona. **The Architect voice owns the DCB boundary placement** (single write point at checkout; store-scoped consistency per ADR 024). **QA owns the mandatory failure paths**: cap-breach rejection, the concurrent-race `DcbConcurrencyException` → retry → reject scenario, and the released-slot-reusable edge. **Product Owner** keeps the richer variants and service graduation parked. **UX/Frontend** own the W2/W3 wireframe deltas and the advisory-vs-authoritative distinction (the cart-review check is advisory; checkout is the truth). One prompt = one session = one PR; author the retro before the PR.

## Spec delta

`docs/workshops/003-promotions-event-model.md` is created (v1.0): the Promotions event model — slices 6.1–6.4, three new Orders-store events (one definition, two tagged), the `CouponUsage` DCB boundary, and GWT scenarios including the cap-breach and `DcbConcurrencyException` race paths — the authoritative source the forthcoming OpenSpec proposal + narrative + implementation prompt will reference. Workshop 001 bumps to v1.13 resolving long-road parking-lot item 7 by pointer. `docs/workshops/` gains its third workshop; the design-return cadence counter resets.
