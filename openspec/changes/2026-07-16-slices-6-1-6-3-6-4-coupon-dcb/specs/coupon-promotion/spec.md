## ADDED Requirements

### Requirement: Define a coupon

The system SHALL allow a coupon to be defined as an event-sourced fact (configuration-as-events). When `DefineCoupon { code, discountPercent, cap }` is issued for a code that does not yet exist, the system SHALL create a new coupon stream keyed by a generated `couponId` and append a `CouponDefined { couponId, code, discountPercent, cap }` event, and SHALL maintain an inline `CouponView` read model resolving `code → { couponId, discountPercent, cap }`. The system SHALL reject a `DefineCoupon` whose `code` already has a definition with a `409` response and append no event, enforced by a partial-unique index on `CouponView.Code` (the open-cart uniqueness precedent — no uniqueness DCB while definitions are seed-issued). The system SHALL reject at validation, with no stream created, a `DefineCoupon` whose `cap` is less than `1` or whose `discountPercent` is outside `(0, 100]`. Definitions are seed-issued this round through a `POST /coupons` endpoint the seeder drives; the identical `CouponDefined` contract is what a future standalone Promotions service would publish as Published Language.

#### Scenario: Define a new coupon

- **GIVEN** no coupon stream exists for code `FLASH20`
- **WHEN** the seeder issues `DefineCoupon { code: "FLASH20", discountPercent: 20, cap: 3 }`
- **THEN** a new coupon stream keyed by a generated `couponId` appends `CouponDefined { couponId, code: "FLASH20", discountPercent: 20, cap: 3 }`
- **AND** the `CouponView` resolves `FLASH20` to `{ couponId, discountPercent: 20, cap: 3 }`

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

The system SHALL return a coupon redemption to the pool when the order that redeemed it is cancelled, so that a cancelled or failed order does not permanently burn a flash-sale slot. When an order stream that holds a `CouponRedeemed` is cancelled — by any reason (`stock_unavailable`, `payment_declined`, or `payment_timeout`) — the system SHALL append a `CouponRedemptionReleased { orderId, couponId }` tagged with the same `CouponId` to that order's stream, in the same transaction as the `OrderCancelled`. The coupon's net redemption count (redemptions minus releases) SHALL decrement accordingly, and the freed slot SHALL be reusable by a later redemption. When an order carries no `CouponRedeemed`, its cancellation SHALL append no release event. At most one release SHALL occur per redemption, inherited from the existing rule that `OrderCancelled` is terminal and appended once (Workshop 001 terminal-guard discipline) — the net count can never under-count below true usage.

#### Scenario: Cancellation releases the redemption

- **GIVEN** an order stream holding `OrderPlaced` and `CouponRedeemed { couponId }` (tagged), which subsequently cancels for any reason
- **WHEN** `OrderCancelled` is appended by the owning cancellation path
- **THEN** the same transaction appends `CouponRedemptionReleased { orderId, couponId }` tagged with the `CouponId`
- **AND** the coupon's net redemption count decrements by one

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
