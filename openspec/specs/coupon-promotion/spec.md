# coupon-promotion Specification

## Purpose

The `coupon-promotion` capability is CritterMart's flash-sale promotions increment: it defines percentage-discount coupons as event-sourced configuration (`DefineCoupon` → `CouponDefined { code, discountPercent, cap, oneRedemptionPerCustomer }` on a coupon stream keyed by a generated `couponId`, resolved through the inline `CouponView`), redeems them under a **global per-coupon redemption cap** at checkout, optionally enforces **one redemption per customer**, and releases a redemption when its order is cancelled. Its defining architectural move is **Dynamic Consistency Boundaries** (ADR 024): a cap is enforced not by a per-stream aggregate but by a store-scoped Marten DCB (`FetchForWritingByTags<CouponUsage>`) computed transactionally at checkout, so `CouponRedeemed` / `CouponRedemptionReleased` are tagged and appended to the *order* stream in the same transaction as `OrderPlaced` / `OrderCancelled` — exactly `cap` redemptions survive under concurrent load, with `DcbConcurrencyException` driving a retry rather than an over-admit.

**The capability now carries two DCBs, and the contrast between them is the point.** The first is a **count** boundary over a single `CouponId` tag — a global cap, "how many redemptions of this coupon?" The second (ADR 024 §38) is an **existence** boundary over a composite `(coupon × customer)` pair — "has this customer redeemed this coupon at all?" — opened only when the definition carries `oneRedemptionPerCustomer`. The composite is not two tags AND-ed: `EventTagQuery` has no such operator and a tag stores one scalar, so it is a **single tag whose value encodes the pair** (`"{couponId}|{customerId}"`), queried through the same single-tag path the first boundary proved. Both are read on the success path and armed by one doubly-tagged append, so a race invalidating **either** drives the same retry loop. The per-customer check runs **first**, so a customer who has already redeemed hears the honest personal reason rather than a coincidental `CouponExhausted`.

It also teaches the **advisory-vs-authoritative** contrast, now at both scopes. The inline `CouponUsageView` (per coupon) and `CustomerCouponUsageView` (per `(coupon × customer)` pair) are projections that may lag and **never** gate redemption; the read-only `GET /coupons/{code}/validate` cart-review query reads them to preview a verdict. The DCB boundary reads at checkout are the sole authority. Each advisory view has a never-persisted boundary twin computed transactionally at write time (`CouponUsage`, `CustomerCouponUsage`) — same arithmetic, different existence, and only the boundary ever decides. The validate query is **optionally authenticated**: an anonymous caller gets the global answer and is never `401`; an authenticated caller additionally gets `already_redeemed`, ordered ahead of `exhausted` so the preview agrees with the authority about *why*, not merely *whether*. That per-customer preview is **forward-only** — a redemption predating the events' `customerId` member is invisible to it — so it may **under-warn** and can never wrongly accuse, an asymmetry tolerable precisely because the read is advisory.

The whole capability lives **inside the Orders Marten store** this round — a standalone Promotions service is the deferred long road, and the identical `CouponDefined` contract is what that future service would publish as Published Language; definitions are seed-issued through `POST /coupons`. Slices 6.1 (define), 6.3 (redeem under a cap), and 6.4 (release on cancel) form the DCB core (PR #144); slice 6.2 adds the advisory validate query and the W2/W3 storefront preview + discount line (PR #146); slice 6.5 adds the composite second DCB (PR #149); slice 6.6 adds the per-customer preview and the customer-facing refusal copy (PR #161), writing nothing at all.
## Requirements
### Requirement: Define a coupon

The system SHALL allow a coupon to be defined as an event-sourced fact (configuration-as-events). When `DefineCoupon { code, discountPercent, cap, oneRedemptionPerCustomer? }` is issued for a code that does not yet exist, the system SHALL create a new coupon stream keyed by a generated `couponId` and append a `CouponDefined { couponId, code, discountPercent, cap, oneRedemptionPerCustomer }` event, and SHALL maintain an inline `CouponView` read model resolving `code → { couponId, discountPercent, cap, oneRedemptionPerCustomer }`. The `oneRedemptionPerCustomer` flag is an optional policy that defaults to `false`; when omitted by the caller or absent from an already-persisted event, it SHALL fold as `false` (leaving every prior definition and the global-cap-only behavior unchanged). When `true`, it marks the coupon for the per-customer redemption boundary governed by the "Enforce one redemption per customer for a per-customer coupon at checkout" requirement. The system SHALL reject a `DefineCoupon` whose `code` already has a definition with a `409` response and append no event, enforced by a partial-unique index on `CouponView.Code` (the open-cart uniqueness precedent — no uniqueness DCB while definitions are seed-issued). The system SHALL reject at validation, with no stream created, a `DefineCoupon` whose `cap` is less than `1` or whose `discountPercent` is outside `(0, 100]`. Definitions are seed-issued this round through a `POST /coupons` endpoint the seeder drives; the identical `CouponDefined` contract is what a future standalone Promotions service would publish as Published Language.

#### Scenario: Define a new coupon

- **GIVEN** no coupon stream exists for code `FLASH20`
- **WHEN** the seeder issues `DefineCoupon { code: "FLASH20", discountPercent: 20, cap: 3 }`
- **THEN** a new coupon stream keyed by a generated `couponId` appends `CouponDefined { couponId, code: "FLASH20", discountPercent: 20, cap: 3, oneRedemptionPerCustomer: false }`
- **AND** the `CouponView` resolves `FLASH20` to `{ couponId, discountPercent: 20, cap: 3, oneRedemptionPerCustomer: false }`

#### Scenario: Define a per-customer coupon

- **GIVEN** no coupon stream exists for code `FIRSTORDER`
- **WHEN** the seeder issues `DefineCoupon { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }`
- **THEN** a new coupon stream appends `CouponDefined { couponId, code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }`
- **AND** the `CouponView` resolves `FIRSTORDER` with `oneRedemptionPerCustomer: true`

#### Scenario: Reject a duplicate code

- **GIVEN** a `CouponDefined { code: "FLASH20" }` already exists
- **WHEN** `DefineCoupon { code: "FLASH20", discountPercent: 15, cap: 5 }` is issued again
- **THEN** the command is rejected with a `409` response
- **AND** no second coupon stream is created and no event is appended

#### Scenario: Reject a nonsensical definition

- **GIVEN** any state
- **WHEN** `DefineCoupon` is issued with `cap: 0` (or a `discountPercent` of `0` or greater than `100`)
- **THEN** the command is rejected at validation with a `400` response
- **AND** no coupon stream is created

### Requirement: Redeem a coupon under a global cap at checkout

The system SHALL enforce a **global per-coupon redemption cap** at checkout using a Dynamic Consistency Boundary in the Orders store, such that a coupon is redeemed at most `cap` times across all orders, ever. When `PlaceOrder` carries a `couponCode` that resolves through `CouponView` to a `CouponDefined`, the system SHALL open the DCB boundary `FetchForWritingByTags<CouponUsage>(new EventTagQuery().Or<CouponId>(couponId))`, compute the boundary's net redemption count (redemptions minus releases across every tagged event), and — while that net count is less than `cap` — append a `CouponRedeemed { orderId, couponId, discount }` tagged with the strong-typed `CouponId` to the new order stream, in the same transaction as `OrderPlaced`, where `discount` is the order subtotal multiplied by `discountPercent / 100`. When a concurrent redemption lands a matching tagged event inside the boundary between the read and the commit, the losing commit SHALL throw `DcbConcurrencyException` and its entire transaction (the order stream included) SHALL roll back; the system SHALL retry the redemption against a fresh boundary read (DCB optimistic concurrency is cap-blind, so a bare check under-admits under load). A retried redemption still below the cap SHALL succeed; only a genuinely-full cap SHALL yield a `409 CouponExhausted`. In total exactly `cap` redemptions SHALL succeed, under sequential or concurrent load. When the net redemption count already equals `cap`, the system SHALL reject the placement with a `409 CouponExhausted` response, create no order stream, and append no event — the customer keeps their cart and decides. When the `couponCode` resolves to no definition, the system SHALL reject the placement with a `409 CouponInvalid` response and create no order stream. When `PlaceOrder` carries no `couponCode`, no boundary is opened and slice 4.1's behavior is unchanged (see `order-lifecycle`).

#### Scenario: Redeem a coupon below the cap

- **GIVEN** `CouponDefined { code: "FLASH20", discountPercent: 20, cap: 3 }` and two `CouponRedeemed` events tagged with its `CouponId` across other order streams (net count `2`), and the customer's open cart totals `40.00`
- **WHEN** the customer issues `PlaceOrder { customerId, couponCode: "FLASH20" }`
- **THEN** the boundary finds net count `2 < 3` and the new order stream appends `OrderPlaced { subtotal: 40.00, discount: 8.00, total: 32.00 }` and `CouponRedeemed { couponId, discount: 8.00 }` tagged with the `CouponId`, in the same transaction
- **AND** the placement succeeds with a `201` response

#### Scenario: Reject a redemption at the cap

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3 }` with a net redemption count of `3` (three tagged redemptions, no releases)
- **WHEN** the customer issues `PlaceOrder { couponCode: "FLASH20" }`
- **THEN** the placement is rejected with a `409 CouponExhausted` response
- **AND** no order stream is created and no event is appended

#### Scenario: A concurrent race resolves to exactly one surviving order

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3 }` with a net redemption count of `2`
- **WHEN** two customers issue `PlaceOrder { couponCode: "FLASH20" }` concurrently and both boundary reads see net count `2`
- **THEN** one transaction commits the third redemption and the other's commit throws `DcbConcurrencyException`, rolling its whole transaction back (no order stream)
- **AND** the losing placement retries against a fresh boundary read, finds the net count now at the cap, and is rejected with a `409 CouponExhausted`
- **AND** exactly one of the two racing orders exists afterward

#### Scenario: Concurrent redemptions never exceed the cap

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3 }` with no redemptions yet
- **WHEN** six customers issue `PlaceOrder { couponCode: "FLASH20" }` concurrently
- **THEN** exactly three placements succeed with `201` and the other three are rejected with `409`
- **AND** the coupon's net redemption count is exactly `3` — the cap held under the burst

#### Scenario: Reject an unknown code at checkout

- **GIVEN** no definition resolves for the carried code `BOGUS`
- **WHEN** `PlaceOrder { couponCode: "BOGUS" }` is issued
- **THEN** the placement is rejected with a `409 CouponInvalid` response
- **AND** no order stream is created

### Requirement: Release a redemption when its order is cancelled

The system SHALL return a coupon redemption to the pool when the order that redeemed it is cancelled, so that a cancelled or failed order does not permanently burn a redemption slot. When an order stream that holds a `CouponRedeemed` is cancelled — by any reason (`stock_unavailable`, `payment_declined`, or `payment_timeout`) — the system SHALL append a `CouponRedemptionReleased { orderId, couponId }` to that order's stream, in the same transaction as the `OrderCancelled`, tagged with the same `CouponId` as the redemption. When the redeemed coupon was a per-customer coupon (`oneRedemptionPerCustomer = true`), the release SHALL **additionally** carry the composite `CouponCustomerTag` for that `(coupon, customer)` pair, so the per-customer boundary decrements and that customer may redeem the coupon again. The coupon's global net redemption count and — for a per-customer coupon — the per-customer net count (redemptions minus releases) SHALL decrement accordingly, and the freed slot SHALL be reusable by a later redemption. When an order carries no `CouponRedeemed`, its cancellation SHALL append no release event. At most one release SHALL occur per redemption, inherited from the existing rule that `OrderCancelled` is terminal and appended once (Workshop 001 terminal-guard discipline) — neither net count can ever under-count below true usage.

#### Scenario: Cancellation releases the redemption

- **GIVEN** an order stream holding `OrderPlaced` and `CouponRedeemed { couponId }` (tagged), which subsequently cancels for any reason
- **WHEN** `OrderCancelled` is appended by the owning cancellation path
- **THEN** the same transaction appends `CouponRedemptionReleased { orderId, couponId }` tagged with the `CouponId`
- **AND** the coupon's global net redemption count decrements by one

#### Scenario: Cancelling a per-customer redemption returns the customer's slot

- **GIVEN** `CouponDefined { code: "FIRSTORDER", oneRedemptionPerCustomer: true }` and an order by `customer-X` holding a `CouponRedeemed` tagged with both the `CouponId` and the `(FIRSTORDER × customer-X)` `CouponCustomerTag`, which subsequently cancels
- **WHEN** `OrderCancelled` is appended
- **THEN** the same transaction appends `CouponRedemptionReleased` tagged with **both** the `CouponId` and the same `CouponCustomerTag`
- **AND** the per-customer net count for `(FIRSTORDER × customer-X)` decrements to `0`, so `customer-X` may redeem `FIRSTORDER` again

#### Scenario: A released slot is reusable

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3 }` with three redemptions and one release (net count `2`)
- **WHEN** a new `PlaceOrder { couponCode: "FLASH20" }` is issued
- **THEN** the boundary finds net count `2 < 3` and the redemption succeeds
- **AND** the release genuinely returned capacity to the pool

#### Scenario: Cancellation without a coupon appends no release

- **GIVEN** an order stream with no `CouponRedeemed`
- **WHEN** the order cancels
- **THEN** no `CouponRedemptionReleased` event is appended
- **AND** slices 4.5–4.7 behave exactly as modeled without a coupon

### Requirement: Track advisory coupon usage

The system SHALL maintain an inline `CouponUsageView` read model holding, per `couponId`, the net redemption count (folding `CouponRedeemed` as `+1` and `CouponRedemptionReleased` as `-1`), grouped by the events' `couponId` member. This view is advisory — a projection that may lag — and is distinct from the never-persisted `CouponUsage` DCB boundary state computed transactionally at write time; the authoritative cap check is only ever the boundary read, never this view. The view SHALL be inline (immediately consistent), because no async projection daemon runs in this project.

#### Scenario: Usage reflects redemptions net of releases

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3 }`
- **WHEN** three orders redeem `FLASH20` and one of them is subsequently cancelled (releasing its redemption)
- **THEN** the `CouponUsageView` for that coupon shows a net redemption count of `2`

### Requirement: Validate and price a coupon at cart review

The system SHALL provide a **read-only** advisory query that resolves a coupon code at cart review so the storefront can preview a discount before checkout, writing no event and creating no stream. The query SHALL be **optionally authenticated**: it SHALL accept an unauthenticated request and SHALL NOT respond `401`.

When `GET /coupons/{code}/validate` is issued, the system SHALL resolve the `code` against `CouponView` and respond `200` with a `CouponValidation { code, status, discountPercent? }` where `status` is one of `invalid`, `already_redeemed`, `exhausted`, or `valid`, evaluated in **exactly that precedence order**:

1. `invalid` (no `discountPercent`) when no definition resolves for the code;
2. `already_redeemed` (no `discountPercent`) when a definition resolves with `oneRedemptionPerCustomer = true`, **and** the request carries an authenticated customer identity (the JWT `sub` claim, per ADR 023 — the sole customer trust boundary), **and** the `CustomerCouponUsageView` net redemption count for that `(couponId, customerId)` pair is `1` or greater;
3. `exhausted` (no `discountPercent`) when a definition resolves but its `CouponUsageView` net redemption count has reached `cap`;
4. `valid` (with the coupon's `discountPercent`) otherwise.

The `already_redeemed` check SHALL be evaluated **before** the `exhausted` check, mirroring the checkout ordering in "Enforce one redemption per customer for a per-customer coupon at checkout", so that a customer who has already redeemed a coupon that is *also* globally exhausted receives the accurate personal reason rather than the crowd reason — the two lead the customer to different remedies.

When the request carries **no** authenticated identity, the system SHALL evaluate only `invalid` / `exhausted` / `valid` and SHALL NOT return `already_redeemed`; the anonymous response SHALL be identical to the behavior that shipped in slice 6.2. When the resolved definition has `oneRedemptionPerCustomer = false`, the system SHALL NOT consult `CustomerCouponUsageView` and SHALL NOT return `already_redeemed`, regardless of authentication.

The query SHALL compute no dollar discount — the storefront prices `discountPercent` against the cart total it already holds. This query is **advisory by design** in every status it returns, including `already_redeemed`: its answer MAY be stale (both usage views are projections; a slot may free by cancellation, and a customer may redeem in a concurrent session, between this check and checkout), so it SHALL NOT gate redemption. The authoritative checks are only ever the DCB boundary reads at checkout; a `valid` answer here does not guarantee the checkout succeeds, and an `already_redeemed` or `exhausted` answer here does not prevent a customer from carrying the code to a checkout that re-decides.

#### Scenario: Validate a redeemable coupon

- **GIVEN** `CouponDefined { code: "FLASH20", discountPercent: 20, cap: 3 }` and a `CouponUsageView` net count of `2` (below the cap)
- **WHEN** `GET /coupons/FLASH20/validate` is issued
- **THEN** the response is `200` with `{ code: "FLASH20", status: "valid", discountPercent: 20 }`
- **AND** no event is written and no stream is created

#### Scenario: Report an unknown code as invalid

- **GIVEN** no coupon definition resolves for code `BOGUS`
- **WHEN** `GET /coupons/BOGUS/validate` is issued
- **THEN** the response is `200` with `{ code: "BOGUS", status: "invalid" }` and no `discountPercent`
- **AND** no event is written

#### Scenario: Report an advisorily-exhausted coupon

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3 }` and a `CouponUsageView` net count of `3` (at the cap)
- **WHEN** `GET /coupons/FLASH20/validate` is issued
- **THEN** the response is `200` with `{ code: "FLASH20", status: "exhausted" }` and no `discountPercent`
- **AND** the answer is advisory only — a slot freed by a later cancellation, or a lagging projection, does not make this query authoritative; checkout re-decides against the DCB boundary

#### Scenario: Warn an authenticated customer who has already redeemed

- **GIVEN** `CouponDefined { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }` and a `CustomerCouponUsageView` net count of `1` for the `(FIRSTORDER × customer-X)` pair
- **WHEN** `customer-X` issues `GET /coupons/FIRSTORDER/validate` with a valid bearer token
- **THEN** the response is `200` with `{ code: "FIRSTORDER", status: "already_redeemed" }` and no `discountPercent`
- **AND** no event is written and no stream is created

#### Scenario: An authenticated customer who has not redeemed sees the discount

- **GIVEN** `CouponDefined { code: "FIRSTORDER", discountPercent: 15, oneRedemptionPerCustomer: true }` and no `CustomerCouponUsageView` document for the `(FIRSTORDER × customer-Y)` pair
- **WHEN** `customer-Y` issues `GET /coupons/FIRSTORDER/validate` with a valid bearer token
- **THEN** the response is `200` with `{ code: "FIRSTORDER", status: "valid", discountPercent: 15 }`

#### Scenario: An anonymous caller gets the unchanged global answer

- **GIVEN** `CouponDefined { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }` already redeemed once by `customer-X`
- **WHEN** `GET /coupons/FIRSTORDER/validate` is issued with **no** bearer token
- **THEN** the response is `200` with `{ code: "FIRSTORDER", status: "valid", discountPercent: 15 }` — the slice-6.2 answer, unchanged
- **AND** the response is never `401` and never `already_redeemed` — the query holds no identity, so it makes no personal claim

#### Scenario: A global-cap-only coupon never reports already-redeemed

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3, oneRedemptionPerCustomer: false }` which `customer-X` has redeemed once (below the cap)
- **WHEN** `customer-X` issues `GET /coupons/FLASH20/validate` with a valid bearer token
- **THEN** the response is `200` with `{ code: "FLASH20", status: "valid", discountPercent: 20 }`
- **AND** `CustomerCouponUsageView` is not consulted — redeeming such a coupon again is its chosen policy, not a condition to warn about

#### Scenario: The personal reason outranks the crowd reason

- **GIVEN** `CouponDefined { code: "FIRSTORDER", cap: 2, oneRedemptionPerCustomer: true }` whose `CouponUsageView` net count has reached `cap`, **and** a `CustomerCouponUsageView` net count of `1` for the `(FIRSTORDER × customer-X)` pair
- **WHEN** `customer-X` issues `GET /coupons/FIRSTORDER/validate` with a valid bearer token
- **THEN** the response is `200` with `status: "already_redeemed"` — **not** `exhausted`
- **AND** the precedence matches the checkout ordering, so the preview and the authority agree about *why* the coupon is unusable, not merely *whether*

#### Scenario: A cancelled redemption restores the preview

- **GIVEN** `customer-X` has redeemed per-customer `FIRSTORDER`, so the query answers `already_redeemed`, and their redeeming order subsequently cancels — appending a `CouponRedemptionReleased` that decrements the pair to a net count of `0`
- **WHEN** `customer-X` issues `GET /coupons/FIRSTORDER/validate` with a valid bearer token
- **THEN** the response is `200` with `{ status: "valid", discountPercent: 15 }`
- **AND** the advisory view and the DCB boundary agree, because both fold the same redemption and release events — they differ in *when*, not in *what*

#### Scenario: A valid preview is still not a promise

- **GIVEN** `customer-X` has not redeemed per-customer `FIRSTORDER`, and the query answers `valid`
- **WHEN** `customer-X` redeems `FIRSTORDER` in a concurrent session and then places the previewed order
- **THEN** the checkout is rejected with a `409 CouponAlreadyRedeemedByCustomer` despite the `valid` preview
- **AND** the composite DCB boundary remains the sole authority — the preview is advisory even when it is personally accurate

### Requirement: Enforce one redemption per customer for a per-customer coupon at checkout

The system SHALL enforce, for a coupon defined with `oneRedemptionPerCustomer = true`, that a given customer redeems that coupon **at most once** (net of releases), using a second Dynamic Consistency Boundary in the Orders store keyed by a composite `(couponId × customerId)` tag — layered on top of, and committed in the same checkout transaction as, the global per-coupon cap. When `PlaceOrder` carries a `couponCode` that resolves through `CouponView` to a `CouponDefined` with `oneRedemptionPerCustomer = true`, the system SHALL open the composite boundary `FetchForWritingByTags<CustomerCouponUsage>(new EventTagQuery().Or<CouponCustomerTag>(CouponCustomerTag.For(couponId, customerId)))` alongside the global-cap boundary and, when the composite boundary's net redemption count for this `(coupon, customer)` pair is `1` or greater, SHALL reject the placement with a `409 CouponAlreadyRedeemedByCustomer` response, create no order stream, and append no event.

The rejection's ProblemDetails `detail` SHALL be customer-facing copy that states the personal reason and returns the decision to the shopper — **"You've already used this coupon — remove it to continue, or try another."** — parallel in shape to the `CouponExhausted` refusal. The `409` status code and the `CouponAlreadyRedeemedByCustomer` title token SHALL remain unchanged, so no machine-readable contract depends on the wording.

The per-customer check SHALL be evaluated before the global-cap check so a customer who has already redeemed receives the accurate reason rather than `CouponExhausted`. When both boundaries admit (the global net count is below `cap` AND this customer's net count is `0`), the system SHALL append the tagged `CouponRedeemed` carrying **both** the strong-typed `CouponId` tag and the `CouponCustomerTag`, so both boundaries' optimistic-concurrency assertions are armed in the one `SaveChangesAsync`; a concurrent redemption invalidating **either** boundary SHALL throw `DcbConcurrencyException` and drive the existing reload-and-retry loop against fresh boundary reads. Each `(coupon, customer)` pair is an **independent** composite boundary, so concurrent redemptions by **different** customers of the same per-customer coupon SHALL NOT conflict on the per-customer boundary (they contend only on the shared global cap, if at all). The composite boundary's assertion is armed as a transactional backstop; a **single** customer's concurrent checkouts are in any case already serialized by the one-open-cart invariant (a customer has at most one open cart), so the per-customer invariant is enforced in practice by the cross-order **existence** check — a later order refused because an earlier one already redeemed — with the DCB assertion keeping that check sound under concurrency. When the definition's `oneRedemptionPerCustomer` is `false` (the default), no composite boundary is opened and this requirement imposes nothing: the redemption is governed solely by the global cap, unchanged, and one customer MAY redeem such a coupon more than once.

#### Scenario: A per-customer coupon admits a customer once

- **GIVEN** `CouponDefined { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }` and `customer-X` has not redeemed it
- **WHEN** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }`
- **THEN** the composite boundary finds net count `0` and the placement succeeds `201`, appending a `CouponRedeemed` tagged with both the `CouponId` and the `(FIRSTORDER × customer-X)` `CouponCustomerTag`

#### Scenario: A per-customer coupon rejects the same customer's second redemption

- **GIVEN** `CouponDefined { code: "FIRSTORDER", oneRedemptionPerCustomer: true }` which `customer-X` has already redeemed once (composite net count `1`)
- **WHEN** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }` again
- **THEN** the placement is rejected with a `409` response whose title is `CouponAlreadyRedeemedByCustomer`
- **AND** the response detail reads "You've already used this coupon — remove it to continue, or try another."
- **AND** no order stream is created and no event is appended

#### Scenario: A per-customer coupon still admits a different customer

- **GIVEN** `CouponDefined { code: "FIRSTORDER", oneRedemptionPerCustomer: true }` which `customer-X` has already redeemed once
- **WHEN** `customer-Y` (who has not redeemed it) issues `PlaceOrder { couponCode: "FIRSTORDER" }`
- **THEN** the placement succeeds `201` — the `(FIRSTORDER × customer-Y)` composite boundary is a distinct pair at net count `0`

#### Scenario: Concurrent redemptions by different customers all succeed

- **GIVEN** `CouponDefined { code: "FIRSTORDER", cap: 100000, oneRedemptionPerCustomer: true }` and several distinct customers, none of whom has redeemed it
- **WHEN** those customers issue `PlaceOrder { couponCode: "FIRSTORDER" }` concurrently
- **THEN** every placement succeeds `201` — each `(FIRSTORDER × customer)` pair is an independent composite boundary, so they do not conflict on the per-customer boundary
- **AND** each customer's per-customer net count is `1` (the global cap is far from reached)

#### Scenario: A non-per-customer coupon lets one customer redeem more than once

- **GIVEN** `CouponDefined { code: "FLASH20", cap: 3, oneRedemptionPerCustomer: false }` which `customer-X` has already redeemed once (below the global cap)
- **WHEN** `customer-X` issues `PlaceOrder { couponCode: "FLASH20" }` again
- **THEN** no composite boundary is opened and, the global cap permitting, the placement succeeds `201` — the per-customer invariant does not apply

### Requirement: Track advisory per-customer coupon usage

The system SHALL maintain an inline `CustomerCouponUsageView` read model holding, per `(couponId, customerId)` pair, the net redemption count for that pair — folding `CouponRedeemed` as `+1` and `CouponRedemptionReleased` as `-1` — so that the cart-review validate query can preview a per-customer verdict before checkout. The view SHALL be keyed by the composite identity `"{couponId}|{customerId}"`, mirroring the `CouponCustomerTag` value shape, and SHALL be **inline** (immediately consistent), because no async projection daemon runs in this project and an async advisory view could not serve the affordance it exists for.

To make that projection possible, `CouponRedeemed` and `CouponRedemptionReleased` SHALL each carry a `customerId` **event member** identifying the customer whose redemption the event records. This is required because a Marten `MultiStreamProjection` routes events to a document by an event member, whereas the `CouponCustomerTag` that already encodes the pair is a **write-side query mechanism and not a projection grouping key** — the pair is therefore not projectable from the events as they stood after slice 6.5. The field SHALL be optional with a default, so that already-persisted events fold without it and no existing behavior changes (the same non-breaking event evolution as `oneRedemptionPerCustomer` and `perCustomer`).

This view is **advisory** — a projection, and distinct from the never-persisted `CustomerCouponUsage` DCB boundary state computed transactionally at checkout. Same arithmetic, different existence: the authoritative per-customer check is only ever the composite boundary read, never this view, and this view SHALL NOT gate redemption.

The view SHALL be **forward-only**: a redemption appended before the `customerId` member existed cannot be attributed to a customer and SHALL be absent from this view, while remaining fully visible to the composite DCB boundary (which reads tags, present since slice 6.5). The resulting error is therefore **one-sided by construction** — the preview MAY fail to warn a customer who has in fact already redeemed, and SHALL NOT report `already_redeemed` for a customer who has not. A customer whose only redemption predates the field SHALL see the same preview and the same checkout refusal they see today, which is an acceptable degradation precisely because this read is advisory.

#### Scenario: Per-customer usage reflects redemptions net of releases

- **GIVEN** `CouponDefined { code: "FIRSTORDER", oneRedemptionPerCustomer: true }`
- **WHEN** `customer-X` redeems `FIRSTORDER` and that order is subsequently cancelled, releasing the redemption
- **THEN** the `CustomerCouponUsageView` for the `(FIRSTORDER × customer-X)` pair shows a net count of `0`

#### Scenario: Each pair is tracked independently

- **GIVEN** `CouponDefined { code: "FIRSTORDER", oneRedemptionPerCustomer: true }`
- **WHEN** `customer-X` and `customer-Y` each redeem `FIRSTORDER` once
- **THEN** the view holds two documents, `(FIRSTORDER × customer-X)` and `(FIRSTORDER × customer-Y)`, each with a net count of `1`
- **AND** neither pair's count is affected by the other's redemption

#### Scenario: The advisory view never gates redemption

- **GIVEN** a `CustomerCouponUsageView` net count of `0` for the `(FIRSTORDER × customer-X)` pair
- **WHEN** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }` and the composite DCB boundary finds a net count of `1`
- **THEN** the placement is rejected with `409 CouponAlreadyRedeemedByCustomer` — the boundary read wins over the view
- **AND** the view is never consulted at checkout

#### Scenario: A redemption predating the customerId member is invisible to the view

- **GIVEN** `customer-X`'s only redemption of per-customer `FIRSTORDER` was appended before the `customerId` event member existed, so no `CustomerCouponUsageView` document exists for the pair, while the event still carries its `CouponCustomerTag`
- **WHEN** `customer-X` issues `GET /coupons/FIRSTORDER/validate` with a valid bearer token and then places the order
- **THEN** the query answers `status: "valid"` — the preview under-warns
- **AND** the checkout is nonetheless rejected with `409 CouponAlreadyRedeemedByCustomer`, because the composite boundary reads the tag and still sees the redemption

