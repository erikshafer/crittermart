---
narrative: 011
title: The Customer Redeems a Flash Coupon
actor: Customer
status: draft
version: v1.2
slices: [6.1, 6.2, 6.3, 6.4, 6.5]
references:
  - docs/workshops/003-promotions-event-model.md (§ 3 storyboard, § 4 vocabulary, § 5 slices 6.1/6.3/6.4/6.5, § 6 GWT)
  - docs/decisions/024-dcb-coupon-redemption-in-orders.md (the DCB decision this journey realizes; §38 the per-customer variant)
  - docs/narratives/004-customer-purchase.md (the purchasing journey this one wraps — Moment 2, Placing the order)
  - openspec/changes/archive/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/ (slices 6.1/6.3/6.4, archived; capabilities coupon-promotion + order-lifecycle)
  - openspec/changes/archive/2026-07-16-slice-6-2-advisory-coupon-validate/ (slice 6.2; coupon-promotion MODIFIED — the advisory validate query)
  - openspec/changes/slice-6-5-per-customer-redemption-dcb/ (slice 6.5; coupon-promotion — the second DCB, one-redemption-per-customer)
---

# Narrative 011 — The Customer Redeems a Flash Coupon

This is the same Customer as [Narrative 004](004-customer-purchase.md), now offered a discount. Their purchasing arc is unchanged — fill a cart, place an order, watch it get fulfilled — but a **coupon** now wraps the checkout step (Narrative 004's Moment 2). Where that journey's every write stayed comfortably inside one stream or reached politely across the broker, this one introduces something new to CritterMart: a rule that no *single* order can enforce on its own. A flash coupon is **usable at most N times, ever, across all orders** — and the only place that truth is knowable is *all the orders together*. This is the project's first **Dynamic Consistency Boundary** (DCB): a consistency boundary that spans many streams and aligns with **no aggregate** ([ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md)).

Everything here happens inside the **Orders** service. "Promotions" is a lane in the story, not a deployed service — the coupon's definition is seeded into the Orders store this round, and redemption rides real order streams. No new service, no broker hop, no new schema beyond one opt-in column.

## Journey scope

- **Slice 6.1 — Define a coupon.** Moment 1 covers the coupon coming into being: a Seller-lane definition (`DefineCoupon` → `CouponDefined`), CritterMart's first **configuration-as-events** — the cap *N* is an event-sourced domain fact with an audit trail, not a config row. Seed-realized this round.
- **Slice 6.2 — Preview the discount at cart review.** Moment 2 covers the Customer *seeing* the discount before committing: typing a code on the cart-review screen fires an **advisory** validation query, the total drops, and an invalid or exhausted code answers inline. Nothing is written — the preview is a convenience that softens the surprise of the checkout race, never an authority.
- **Slice 6.3 — Redeem a coupon at checkout.** Moment 3 covers the Customer redeeming it, and the two failure shapes that make the cap real: the **cap-breach** (the last slot is gone) and the **race** (two shoppers reach for the same last slot). This is the DCB moment.
- **Slice 6.4 — Release a redemption on cancellation.** Moment 4 covers the slot that comes *back*: when a redeemed order cancels — for any reason — its redemption is returned to the pool, so a failed sale never permanently burns a flash-sale slot.
- **Slice 6.5 — One redemption per customer.** Moment 5 covers a *different kind* of coupon — a welcome offer meant to be used once **per person** — and what the Customer sees when they reach for it a second time: the same polite refusal the flash sale gives, but for a personal reason. CritterMart's **second** DCB, over a composite `(coupon × customer)` boundary (ADR 024 §38).

## Moment 1 — The flash coupon comes into being (Seller lane)

**Context.** The Seller wants a flash sale: *FLASH20*, 20% off, but strictly limited — only **three** of them, first-come-first-served, to create urgency. The scarcity is the whole point, so it must be enforced, not merely advertised.

**Interaction.** The coupon is defined once: `DefineCoupon { code: "FLASH20", discountPercent: 20, cap: 3 }`. Round one, this is issued by the **seeder** against a real `POST /coupons` endpoint — the same decoupled way the seeder publishes products and receives stock — so the demo boots with its coupons already live. A definition that makes no sense is refused at the door: a cap below 1, or a discount that isn't a sensible percentage, never becomes a coupon.

**System response.** Orders creates a new **coupon stream** and appends `CouponDefined { couponId, code: "FLASH20", discountPercent: 20, cap: 3 }`. An inline `CouponView` projects it, so the code resolves to `{ couponId, discountPercent: 20, cap: 3 }` for the checkout that will later look it up. Defining the same code twice is refused — the code is unique, guarded the same lightweight way the "one open cart per customer" rule is (a partial-unique index), which is plenty while definitions are seed-issued.

**Why the cap is an *event*, not a setting.** `CouponDefined` carries the cap as a fact on a stream, not a row in a config table. That is deliberate: the cap *N* is a domain decision with a history — who set it, to what, when — and it is the exact same `CouponDefined` contract a future standalone Promotions service would *publish* to Orders as Published Language. The only thing that would change is how the definition *travels* (a broker message instead of a local seed); its shape is already right. This is CritterMart's first use of **configuration-as-events**, completing the adjunct-pattern set the event-modeling skill has named since round one.

## Moment 2 — Previewing the discount at cart review (slice 6.2)

**Context.** The Customer has shaped a cart — say `$40.00` worth of critters (Narrative 004, Moment 1) — and has *FLASH20* in hand. Before they commit, they want to *see* what the code does. The cart-review screen (W2) now has a coupon field for exactly this.

**Interaction.** The Customer types `FLASH20` and taps **Apply**. This does *not* place the order and does *not* commit anything — it asks a single, read-only question: `GET /coupons/FLASH20/validate`.

**System response.** Orders resolves the code against the coupon definition (`CouponView`) and its advisory usage (`CouponUsageView`) and answers one of three things, and **writes nothing** doing so:

- **valid** — the code resolves and has room left: the answer carries the `discountPercent`, and the storefront prices the dollar amount *itself* against the cart total it already holds, dropping the summary to `Subtotal $40.00 / Discount (FLASH20) − $8.00 / Total $32.00`. The code is held in the screen's own state — a reload forgets it (accepted round-one behavior) — and rides checkout only when the Customer actually places the order.
- **invalid** — no such coupon: an inline *"This code isn't valid."*, and nothing is held.
- **exhausted** — the coupon resolves but its advisory count has reached the cap: an inline *"This coupon is no longer available."*

**Why the preview is only ever advisory.** The answer is a **projection read**, and a projection can lag; more to the point, a slot can free by a cancellation, or be claimed by another shopper, in the seconds between this check and checkout. So a `valid` preview is a *hope*, not a *promise*: the code still has to survive the checkout boundary, and an `exhausted` preview does not stop the Customer carrying the code to a checkout that might now admit it. This is the deliberate advisory-vs-authoritative split — the preview softens the surprise; the checkout is the only authority. It is the same truth Moment 3 makes vivid under a race.

## Moment 3 — Redeeming the coupon at checkout (the DCB moment)

**Context.** The Customer has previewed the discount (Moment 2) — or skipped straight past it — and has *FLASH20* in hand. They decide to buy.

**Interaction.** The Customer taps **Place Order**, and this time the command carries the code: `PlaceOrder { customerId, couponCode: "FLASH20" }`. As always, the order's *contents* are not re-sent — they are whatever the open cart holds server-side. The code is the one new thing on the wire, and it is **optional**: a `PlaceOrder` with no code is Narrative 004's checkout, unchanged in every respect.

**System response.** Orders resolves the definition (`CouponView` → 20% off, cap 3) and then does the thing no ordinary order could: it opens a **consistency boundary over the coupon itself** — every redemption of *FLASH20* ever recorded, scattered across whatever order streams carry them — and reads the **net count**: how many times this coupon has been redeemed, minus how many have been released. If that count is below the cap, the checkout proceeds as a single atomic step on a brand-new Order stream:

- `OrderPlaced` is appended carrying the **priced** outcome — `subtotal: 40.00`, `discount: 8.00`, `total: 32.00` — the cart's snapshot lines frozen on exactly as before, now with the discount computed once and recorded;
- `CouponRedeemed { orderId, couponId, discount: 8.00 }` is appended to the *same* stream in the *same* transaction, **tagged** with the coupon's identity so the next boundary read can find it.

The order is now placed at the discounted total, and everything downstream — the stock reservation, the (stubbed) payment authorization, the confirmation — works against `$32.00` with no knowledge that a coupon was ever involved. `OrderStatusView` shows the breakdown: subtotal, discount, total, and the code.

**When the last one is gone.** If the boundary read finds the coupon **already at its cap** — three redemptions, none released — the checkout is **refused**: a `409 CouponExhausted`, and *no order is created at all*. The Customer keeps their cart intact and decides what to do — remove the code and buy at full price, or try another. The system never silently drops the discount and charges more; the choice stays with the shopper.

**When two shoppers reach for the same last slot.** Here is what a single stream could never guarantee. Suppose two Customers check out with *FLASH20* at the very same instant, with the count sitting at 2 — one slot left. **Both** boundary reads see "2, room for one more," and both proceed to redeem. They cannot both be right. When they commit, the event store lets exactly one win: the first commit lands the third redemption; the second, finding that a matching tagged event slipped inside its boundary after it read, is rejected with a **`DcbConcurrencyException`**. The losing checkout doesn't error out to the Customer — it **retries**, re-reads the boundary, now sees "3, at the cap," and turns into the same polite `CouponExhausted` refusal as above. **Exactly one of the two racing orders exists afterward.** The cap held under genuine concurrency — which is the entire reason a coupon like this needs a consistency boundary and not just a counter, and it is the talk's demo moment.

**Why no single order could do this.** Each order is its own stream; the cap is a fact about *all of them at once*. There is no aggregate whose version could guard it — the boundary aligns with the *coupon*, not with any one order. That mismatch, "the consistency boundary does not align with an aggregate," is precisely what DCB is for, and it is the pedagogical point of the whole increment.

## Moment 4 — The slot that comes back

**Context.** A discounted order doesn't always make it. It might be cancelled because stock ran short (Narrative 004, Moment 3), because payment was declined (Moment 5), or because it timed out in silence (Moment 6). When that happens, the coupon it redeemed should not stay spent — a flash sale of three should not be quietly reduced to two by an order that failed at payment.

**Interaction.** None from the Customer — this is the system keeping the count honest. As in Narrative 004's cancellations, the trigger is whatever already cancels the order.

**System response.** Every one of the three cancellation paths already appends `OrderCancelled` to the order's stream. Now, *when — and only when — that stream carries a `CouponRedeemed`*, the same step also appends a compensating `CouponRedemptionReleased { orderId, couponId }`, tagged with the same coupon identity, in the same transaction. The boundary counts redemptions **minus** releases, so the net count drops by one and the slot is genuinely returned: the very next shopper to try *FLASH20* finds room again. An order that carried no coupon cancels exactly as it always did — no release, nothing changed.

**Why it can only happen once.** The release rides the cancellation, and a cancellation happens exactly once — `OrderCancelled` is terminal and appended a single time (the discipline every Order handler already enforces). So a redemption can be released at most once; the net count can never drift *below* true usage. The reserve/release symmetry the Inventory story already teaches — set aside, then hand back — reappears here as pure tag arithmetic on the coupon.

## Moment 5 — The coupon meant for one (slice 6.5)

**Context.** Not every coupon is a first-come scramble. Some are a *welcome*: "15% off your order — one to a customer." The Seller wants everyone to get it *once*, and no one to farm it. `FLASH20`'s cap counts redemptions against the *coupon*; this rule counts them against a *person*. It is a different shape of scarcity, and — like the flash cap — no single order can enforce it: the truth "has *this* Customer already used *this* coupon?" lives across *all of that Customer's orders at once*.

**Interaction.** The Seller defines it once, with one extra word of policy: `DefineCoupon { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }`. That last flag is the whole difference — it is an event-sourced *policy* on the definition (the same `CouponDefined` fact that already carries the cap now carries "once per customer"), so the checkout reads the coupon's own definition to decide how strictly to guard it. The Customer, for their part, redeems `FIRSTORDER` exactly as they redeemed `FLASH20` — the code rides `PlaceOrder`, nothing new on the wire.

**System response.** The first time the Customer redeems `FIRSTORDER`, checkout opens a **second** consistency boundary alongside the flash cap — but this one is drawn around the *pair* (this coupon, this Customer), gathering every redemption that pair has ever made across all the Customer's orders. It finds none, and the redemption proceeds: the `CouponRedeemed` is tagged with *both* the coupon's identity *and* the composite `(coupon × customer)` identity, in the one transaction, so both boundaries know about it forever after.

**When the same Customer comes back for a second helping.** They add another cart, apply `FIRSTORDER` again, and place. This time the `(coupon × customer)` boundary finds their earlier redemption and the checkout is **refused** — a `409`, *no order created*, the same shape of polite refusal the exhausted flash sale gives, but for a *personal* reason: they have already used this one. A *different* Customer, meanwhile, sails through: their `(FIRSTORDER × them)` pair is its own boundary, untouched, at zero — the coupon is one-per-customer, not one-in-total. And if that first Customer's redeeming order is ever cancelled, its release carries the composite tag too, so the pair returns to zero and they may use `FIRSTORDER` again — the slot was theirs to reclaim.

**Why this is the *second* DCB, and what makes it different.** The flash cap counted; this one *checks existence* — "has this pair redeemed at all?" — and its boundary aligns with a **pair**, not a single id: CritterMart's first *composite* consistency boundary. In practice a single Customer cannot even race themselves (they hold one open cart at a time, so their checkouts are already serialized), so what this boundary really guarantees is the honest, cross-order truth: *a later order refused because an earlier one already claimed the offer*. That is a rule about all of a Customer's orders at once — and, once again, there is no aggregate whose version could hold it. The boundary is the coupon-and-the-person, together.

## What the journey still leaves out

- **A valid preview is still not a promise.** Even now that the discount previews *before* checkout (Moment 2), the preview is advisory: a coupon that read `valid` on the cart-review screen can still come back `CouponExhausted` at **Place Order** if another shopper took the last slot in between. The preview softens the surprise; it never removes the checkout's authority. That is by design, not a gap (Workshop 003 §3).
- **No stacking; a shared budget still to come.** One coupon per order remains the rule. The *one-per-customer* cap is now built (Moment 5) — but a *shared discount budget* (a summed dollar value across streams rather than a count or an existence check, the third DCB variant ADR 024 names) is still a natural next step, not built here. And a *global* coupon stays global by choice: a `FLASH20`-style coupon (per-customer policy *off*) can still, in principle, be redeemed more than once by one determined shopper — that is the coupon's chosen policy, not an oversight.
- **No per-customer *preview*.** The cart-review preview (Moment 2) still checks only the *global* cap; it does not tell a Customer "you've already used this" before they try, because the preview is anonymous and the per-customer verdict is computed only at checkout. That is the same advisory-vs-authoritative split, applied even more strictly to the personal boundary — a per-customer preview is a parked follow-on (Workshop 003 §8 item 6).

## Forthcoming Moments

The storefront coupon journey now spans define (Moment 1), preview (Moment 2), redeem under a global cap (Moment 3), release on cancellation (Moment 4), and one-redemption-per-customer (Moment 5 — the second DCB, a composite `(coupon × customer)` boundary). What remains is the long road (Workshop 003 §8): the **shared-budget** DCB variant (a summed dollar value across streams — the third and last variant ADR 024 names), a per-customer *preview* affordance, coupon lifecycle (expiry, disable, edit), and Promotions graduating to its own service — at which point Moment 1's `CouponDefined` crosses the broker as Published Language instead of being seeded locally, and nothing else about this journey changes.

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-07-16 | Initial commit. The Customer's flash-coupon journey per [Workshop 003](../workshops/003-promotions-event-model.md) / [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md): Moment 1 (slice 6.1, `DefineCoupon` → `CouponDefined`, configuration-as-events, seed-realized); Moment 2 (slice 6.3, redeem at checkout under the global cap — the DCB moment, including the cap-breach `CouponExhausted` refusal and the `DcbConcurrencyException` race resolving to exactly one surviving order); Moment 3 (slice 6.4, `CouponRedemptionReleased` on cancellation returning the slot). Wraps Narrative 004's Moment 2 (Place Order); the coupon code rides `PlaceOrder` optionally, leaving the no-coupon checkout byte-for-byte unchanged. The advisory cart-review preview (slice 6.2) noted as forthcoming. Realized in `openspec/changes/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/` (capabilities `coupon-promotion` + `order-lifecycle`). |
| v1.1    | 2026-07-16 | **Slice 6.2 realized** (implementations/040, OpenSpec change `2026-07-16-slice-6-2-advisory-coupon-validate`; `coupon-promotion` MODIFIED — one ADDED requirement, the advisory validate query). The forthcoming preview is now a first-class **Moment 2** (Previewing the discount at cart review): the read-only `GET /coupons/{code}/validate` answers `valid` (+ `discountPercent`) / `invalid` / `exhausted`, the W2 field previews `Subtotal / Discount (CODE) / Total` priced client-side, and inline errors surface "not valid" / "no longer available". The former Moments 2 (redeem) and 3 (release) renumber to **3** and **4**. "What the journey still leaves out" retains the advisory caveat (a `valid` preview can still lose the checkout race — by design) and the stacking/per-customer-limit long road; the previously-forthcoming preview bullet is retired. The applied code rides checkout as the existing `?couponCode=` param (no checkout change); W3 binds the already-shipped `subtotal`/`discount`/`couponCode`. |
| v1.2    | 2026-07-17 | **Slice 6.5 realized** (implementations/041, OpenSpec change `slice-6-5-per-customer-redemption-dcb`; `coupon-promotion` MODIFIED *Define a coupon* + *Release a redemption* and ADDED *Enforce one redemption per customer at checkout*). A new **Moment 5** (The coupon meant for one): a `oneRedemptionPerCustomer` coupon (`FIRSTORDER`) redeemable at most once per Customer, enforced by CritterMart's **second** DCB — a composite `(coupon × customer)` boundary, an **existence** check. The Customer's second attempt is refused `409 CouponAlreadyRedeemedByCustomer` (no order created); a different Customer still succeeds (independent pair); a cancelled redemption returns *that* Customer's slot (the release carries the composite tag too). "What the journey still leaves out" retires the per-customer-limit line (now built), keeps the shared-budget variant + no-stacking, and adds a per-customer-*preview* caveat (the preview stays global-only and anonymous — the personal verdict is checkout-authoritative-only). The one modeling fork (per-customer as an opt-in definition policy vs. a universal law vs. a distinct kind) was settled with the owner; the composite-tag mechanic is a single-scalar tag verified against the resolved Marten 9.15.1 assembly (no new API). |
