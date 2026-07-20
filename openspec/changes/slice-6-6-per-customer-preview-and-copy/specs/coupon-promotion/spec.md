## MODIFIED Requirements

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

## ADDED Requirements

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
