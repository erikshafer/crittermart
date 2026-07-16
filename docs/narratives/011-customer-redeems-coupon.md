---
narrative: 011
title: The Customer Redeems a Flash Coupon
actor: Customer
status: draft
version: v1.0
slices: [6.1, 6.3, 6.4]
references:
  - docs/workshops/003-promotions-event-model.md (§ 3 storyboard, § 4 vocabulary, § 5 slices 6.1/6.3/6.4, § 6 GWT)
  - docs/decisions/024-dcb-coupon-redemption-in-orders.md (the DCB decision this journey realizes)
  - docs/narratives/004-customer-purchase.md (the purchasing journey this one wraps — Moment 2, Placing the order)
  - openspec/changes/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/ (capabilities coupon-promotion + order-lifecycle)
---

# Narrative 011 — The Customer Redeems a Flash Coupon

This is the same Customer as [Narrative 004](004-customer-purchase.md), now offered a discount. Their purchasing arc is unchanged — fill a cart, place an order, watch it get fulfilled — but a **coupon** now wraps the checkout step (Narrative 004's Moment 2). Where that journey's every write stayed comfortably inside one stream or reached politely across the broker, this one introduces something new to CritterMart: a rule that no *single* order can enforce on its own. A flash coupon is **usable at most N times, ever, across all orders** — and the only place that truth is knowable is *all the orders together*. This is the project's first **Dynamic Consistency Boundary** (DCB): a consistency boundary that spans many streams and aligns with **no aggregate** ([ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md)).

Everything here happens inside the **Orders** service. "Promotions" is a lane in the story, not a deployed service — the coupon's definition is seeded into the Orders store this round, and redemption rides real order streams. No new service, no broker hop, no new schema beyond one opt-in column.

## Journey scope

- **Slice 6.1 — Define a coupon.** Moment 1 covers the coupon coming into being: a Seller-lane definition (`DefineCoupon` → `CouponDefined`), CritterMart's first **configuration-as-events** — the cap *N* is an event-sourced domain fact with an audit trail, not a config row. Seed-realized this round.
- **Slice 6.3 — Redeem a coupon at checkout.** Moment 2 covers the Customer redeeming it, and the two failure shapes that make the cap real: the **cap-breach** (the last slot is gone) and the **race** (two shoppers reach for the same last slot). This is the DCB moment.
- **Slice 6.4 — Release a redemption on cancellation.** Moment 3 covers the slot that comes *back*: when a redeemed order cancels — for any reason — its redemption is returned to the pool, so a failed sale never permanently burns a flash-sale slot.

**Not yet in this journey:** the cart-review *preview* of the discount — typing a code and seeing the total drop *before* committing — is the advisory query (slice 6.2), which trails in the next increment. For now the code rides the checkout command directly, and the checkout is the one and only authority on whether it applies.

## Moment 1 — The flash coupon comes into being (Seller lane)

**Context.** The Seller wants a flash sale: *FLASH20*, 20% off, but strictly limited — only **three** of them, first-come-first-served, to create urgency. The scarcity is the whole point, so it must be enforced, not merely advertised.

**Interaction.** The coupon is defined once: `DefineCoupon { code: "FLASH20", discountPercent: 20, cap: 3 }`. Round one, this is issued by the **seeder** against a real `POST /coupons` endpoint — the same decoupled way the seeder publishes products and receives stock — so the demo boots with its coupons already live. A definition that makes no sense is refused at the door: a cap below 1, or a discount that isn't a sensible percentage, never becomes a coupon.

**System response.** Orders creates a new **coupon stream** and appends `CouponDefined { couponId, code: "FLASH20", discountPercent: 20, cap: 3 }`. An inline `CouponView` projects it, so the code resolves to `{ couponId, discountPercent: 20, cap: 3 }` for the checkout that will later look it up. Defining the same code twice is refused — the code is unique, guarded the same lightweight way the "one open cart per customer" rule is (a partial-unique index), which is plenty while definitions are seed-issued.

**Why the cap is an *event*, not a setting.** `CouponDefined` carries the cap as a fact on a stream, not a row in a config table. That is deliberate: the cap *N* is a domain decision with a history — who set it, to what, when — and it is the exact same `CouponDefined` contract a future standalone Promotions service would *publish* to Orders as Published Language. The only thing that would change is how the definition *travels* (a broker message instead of a local seed); its shape is already right. This is CritterMart's first use of **configuration-as-events**, completing the adjunct-pattern set the event-modeling skill has named since round one.

## Moment 2 — Redeeming the coupon at checkout (the DCB moment)

**Context.** The Customer has shaped a cart — say `$40.00` worth of critters (Narrative 004, Moment 1) — and has *FLASH20* in hand. They are on the cart-review screen and decide to buy, applying the code.

**Interaction.** The Customer taps **Place Order**, and this time the command carries the code: `PlaceOrder { customerId, couponCode: "FLASH20" }`. As always, the order's *contents* are not re-sent — they are whatever the open cart holds server-side. The code is the one new thing on the wire, and it is **optional**: a `PlaceOrder` with no code is Narrative 004's checkout, unchanged in every respect.

**System response.** Orders resolves the definition (`CouponView` → 20% off, cap 3) and then does the thing no ordinary order could: it opens a **consistency boundary over the coupon itself** — every redemption of *FLASH20* ever recorded, scattered across whatever order streams carry them — and reads the **net count**: how many times this coupon has been redeemed, minus how many have been released. If that count is below the cap, the checkout proceeds as a single atomic step on a brand-new Order stream:

- `OrderPlaced` is appended carrying the **priced** outcome — `subtotal: 40.00`, `discount: 8.00`, `total: 32.00` — the cart's snapshot lines frozen on exactly as before, now with the discount computed once and recorded;
- `CouponRedeemed { orderId, couponId, discount: 8.00 }` is appended to the *same* stream in the *same* transaction, **tagged** with the coupon's identity so the next boundary read can find it.

The order is now placed at the discounted total, and everything downstream — the stock reservation, the (stubbed) payment authorization, the confirmation — works against `$32.00` with no knowledge that a coupon was ever involved. `OrderStatusView` shows the breakdown: subtotal, discount, total, and the code.

**When the last one is gone.** If the boundary read finds the coupon **already at its cap** — three redemptions, none released — the checkout is **refused**: a `409 CouponExhausted`, and *no order is created at all*. The Customer keeps their cart intact and decides what to do — remove the code and buy at full price, or try another. The system never silently drops the discount and charges more; the choice stays with the shopper.

**When two shoppers reach for the same last slot.** Here is what a single stream could never guarantee. Suppose two Customers check out with *FLASH20* at the very same instant, with the count sitting at 2 — one slot left. **Both** boundary reads see "2, room for one more," and both proceed to redeem. They cannot both be right. When they commit, the event store lets exactly one win: the first commit lands the third redemption; the second, finding that a matching tagged event slipped inside its boundary after it read, is rejected with a **`DcbConcurrencyException`**. The losing checkout doesn't error out to the Customer — it **retries**, re-reads the boundary, now sees "3, at the cap," and turns into the same polite `CouponExhausted` refusal as above. **Exactly one of the two racing orders exists afterward.** The cap held under genuine concurrency — which is the entire reason a coupon like this needs a consistency boundary and not just a counter, and it is the talk's demo moment.

**Why no single order could do this.** Each order is its own stream; the cap is a fact about *all of them at once*. There is no aggregate whose version could guard it — the boundary aligns with the *coupon*, not with any one order. That mismatch, "the consistency boundary does not align with an aggregate," is precisely what DCB is for, and it is the pedagogical point of the whole increment.

## Moment 3 — The slot that comes back

**Context.** A discounted order doesn't always make it. It might be cancelled because stock ran short (Narrative 004, Moment 3), because payment was declined (Moment 5), or because it timed out in silence (Moment 6). When that happens, the coupon it redeemed should not stay spent — a flash sale of three should not be quietly reduced to two by an order that failed at payment.

**Interaction.** None from the Customer — this is the system keeping the count honest. As in Narrative 004's cancellations, the trigger is whatever already cancels the order.

**System response.** Every one of the three cancellation paths already appends `OrderCancelled` to the order's stream. Now, *when — and only when — that stream carries a `CouponRedeemed`*, the same step also appends a compensating `CouponRedemptionReleased { orderId, couponId }`, tagged with the same coupon identity, in the same transaction. The boundary counts redemptions **minus** releases, so the net count drops by one and the slot is genuinely returned: the very next shopper to try *FLASH20* finds room again. An order that carried no coupon cancels exactly as it always did — no release, nothing changed.

**Why it can only happen once.** The release rides the cancellation, and a cancellation happens exactly once — `OrderCancelled` is terminal and appended a single time (the discipline every Order handler already enforces). So a redemption can be released at most once; the net count can never drift *below* true usage. The reserve/release symmetry the Inventory story already teaches — set aside, then hand back — reappears here as pure tag arithmetic on the coupon.

## What the Customer does *not* yet see

- **No preview of the discount before checkout.** Typing *FLASH20* on the cart-review screen and watching the total drop to `$32.00` *before* committing — with an inline "this code isn't valid" or "no longer available" — is the advisory query (slice 6.2), and it trails in the next increment. For now the code is applied *at* checkout; the priced result appears on the confirmed order, not as a live preview.
- **No "you were beaten to it" until you try.** Because there is no advisory check yet, a Customer only learns a flash coupon is exhausted when their `Place Order` comes back with `CouponExhausted`. The checkout is the authority; the preview that would soften the surprise is 6.2's job.
- **No stacking, no per-customer limit.** One coupon per order is the rule, and the cap is global — one person could, in principle, redeem all three. A *one-per-customer* cap and a *shared discount budget* are the richer DCB variants ADR 024 names as natural next steps, not built here.

## Forthcoming Moments

- **The cart-review preview (slice 6.2).** The advisory validation query and the W2/W3 storefront affordances — apply a code, see the discounted total, get an inline "not valid" / "no longer available" — turning Moment 2's at-checkout application into a previewed one. The next increment.

Beyond that, the long road (Workshop 003 § 8): per-customer and shared-budget DCB variants, coupon lifecycle (expiry, disable, edit), and Promotions graduating to its own service — at which point Moment 1's `CouponDefined` crosses the broker as Published Language instead of being seeded locally, and nothing else about this journey changes.

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-07-16 | Initial commit. The Customer's flash-coupon journey per [Workshop 003](../workshops/003-promotions-event-model.md) / [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md): Moment 1 (slice 6.1, `DefineCoupon` → `CouponDefined`, configuration-as-events, seed-realized); Moment 2 (slice 6.3, redeem at checkout under the global cap — the DCB moment, including the cap-breach `CouponExhausted` refusal and the `DcbConcurrencyException` race resolving to exactly one surviving order); Moment 3 (slice 6.4, `CouponRedemptionReleased` on cancellation returning the slot). Wraps Narrative 004's Moment 2 (Place Order); the coupon code rides `PlaceOrder` optionally, leaving the no-coupon checkout byte-for-byte unchanged. The advisory cart-review preview (slice 6.2) noted as forthcoming. Realized in `openspec/changes/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/` (capabilities `coupon-promotion` + `order-lifecycle`). |
