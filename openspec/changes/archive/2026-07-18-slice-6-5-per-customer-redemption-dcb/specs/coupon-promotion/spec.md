## MODIFIED Requirements

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

## ADDED Requirements

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
