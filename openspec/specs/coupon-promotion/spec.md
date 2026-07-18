# coupon-promotion Specification

## Purpose

The `coupon-promotion` capability is CritterMart's flash-sale promotions increment: it defines percentage-discount coupons as event-sourced configuration (`DefineCoupon` → `CouponDefined { code, discountPercent, cap }` on a coupon stream keyed by a generated `couponId`, resolved through the inline `CouponView`), redeems them under a **global per-coupon redemption cap** at checkout, and releases a redemption when its order is cancelled. Its defining architectural move is CritterMart's first **Dynamic Consistency Boundary** (ADR 024): the cap is enforced not by a per-stream aggregate but by a store-scoped Marten DCB (`FetchForWritingByTags<CouponUsage>`) computed transactionally at checkout, so `CouponRedeemed` / `CouponRedemptionReleased` are tagged with a strong-typed `CouponId` and appended to the *order* stream in the same transaction as `OrderPlaced` / `OrderCancelled` — exactly `cap` redemptions survive under concurrent load, with `DcbConcurrencyException` driving a retry rather than an over-admit. The whole capability lives **inside the Orders Marten store** this round — a standalone Promotions service is the deferred long road, and the identical `CouponDefined` contract is what that future service would publish as Published Language; definitions are seed-issued through `POST /coupons`. It also teaches the **advisory-vs-authoritative** contrast: the inline `CouponUsageView` net redemption count and the read-only `GET /coupons/{code}/validate` cart-review query are advisory projections that may lag and never gate redemption — the DCB boundary read at checkout is the sole authority. Slices 6.1 (define), 6.3 (redeem under a cap), and 6.4 (release on cancel) form the DCB core (PR #144); slice 6.2 adds the advisory validate query and the W2/W3 storefront preview + discount line (PR #146).
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

The system SHALL provide a **read-only** advisory query that resolves a coupon code at cart review so the storefront can preview a discount before checkout, writing no event and creating no stream. When `GET /coupons/{code}/validate` is issued, the system SHALL resolve the `code` against `CouponView` and respond `200` with a `CouponValidation { code, status, discountPercent? }` where `status` is one of `valid`, `invalid`, or `exhausted`: `valid` (with the coupon's `discountPercent`) when a definition resolves and its `CouponUsageView` net redemption count is below `cap`; `exhausted` (no `discountPercent`) when a definition resolves but its net count has reached `cap`; and `invalid` (no `discountPercent`) when no definition resolves for the code. The query SHALL compute no dollar discount — the storefront prices `discountPercent` against the cart total it already holds. This query is **advisory by design**: its answer MAY be stale (the `CouponUsageView` net count is a projection, and a slot may free by cancellation between this check and checkout), so it SHALL NOT gate redemption. The authoritative cap check is only ever the DCB boundary read at checkout (see "Redeem a coupon under a global cap at checkout"); a `valid` answer here does not guarantee the checkout succeeds, and an `exhausted` answer here does not prevent a customer from carrying the code to a checkout that may still succeed if a slot has since freed.

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

### Requirement: Enforce one redemption per customer for a per-customer coupon at checkout

The system SHALL enforce, for a coupon defined with `oneRedemptionPerCustomer = true`, that a given customer redeems that coupon **at most once** (net of releases), using a second Dynamic Consistency Boundary in the Orders store keyed by a composite `(couponId × customerId)` tag — layered on top of, and committed in the same checkout transaction as, the global per-coupon cap. When `PlaceOrder` carries a `couponCode` that resolves through `CouponView` to a `CouponDefined` with `oneRedemptionPerCustomer = true`, the system SHALL open the composite boundary `FetchForWritingByTags<CustomerCouponUsage>(new EventTagQuery().Or<CouponCustomerTag>(CouponCustomerTag.For(couponId, customerId)))` alongside the global-cap boundary and, when the composite boundary's net redemption count for this `(coupon, customer)` pair is `1` or greater, SHALL reject the placement with a `409 CouponAlreadyRedeemedByCustomer` response, create no order stream, and append no event. The per-customer check SHALL be evaluated before the global-cap check so a customer who has already redeemed receives the accurate reason rather than `CouponExhausted`. When both boundaries admit (the global net count is below `cap` AND this customer's net count is `0`), the system SHALL append the tagged `CouponRedeemed` carrying **both** the strong-typed `CouponId` tag and the `CouponCustomerTag`, so both boundaries' optimistic-concurrency assertions are armed in the one `SaveChangesAsync`; a concurrent redemption invalidating **either** boundary SHALL throw `DcbConcurrencyException` and drive the existing reload-and-retry loop against fresh boundary reads. Each `(coupon, customer)` pair is an **independent** composite boundary, so concurrent redemptions by **different** customers of the same per-customer coupon SHALL NOT conflict on the per-customer boundary (they contend only on the shared global cap, if at all). The composite boundary's assertion is armed as a transactional backstop; a **single** customer's concurrent checkouts are in any case already serialized by the one-open-cart invariant (a customer has at most one open cart), so the per-customer invariant is enforced in practice by the cross-order **existence** check — a later order refused because an earlier one already redeemed — with the DCB assertion keeping that check sound under concurrency. When the definition's `oneRedemptionPerCustomer` is `false` (the default), no composite boundary is opened and this requirement imposes nothing: the redemption is governed solely by the global cap, unchanged, and one customer MAY redeem such a coupon more than once.

#### Scenario: A per-customer coupon admits a customer once

- **GIVEN** `CouponDefined { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }` and `customer-X` has not redeemed it
- **WHEN** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }`
- **THEN** the composite boundary finds net count `0` and the placement succeeds `201`, appending a `CouponRedeemed` tagged with both the `CouponId` and the `(FIRSTORDER × customer-X)` `CouponCustomerTag`

#### Scenario: A per-customer coupon rejects the same customer's second redemption

- **GIVEN** `CouponDefined { code: "FIRSTORDER", oneRedemptionPerCustomer: true }` which `customer-X` has already redeemed once (composite net count `1`)
- **WHEN** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }` again
- **THEN** the placement is rejected with a `409 CouponAlreadyRedeemedByCustomer` response
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

