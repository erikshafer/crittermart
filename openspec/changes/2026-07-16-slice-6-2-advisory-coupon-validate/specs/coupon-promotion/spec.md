## ADDED Requirements

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
