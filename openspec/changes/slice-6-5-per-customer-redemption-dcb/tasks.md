# Tasks — slice 6.5 one-redemption-per-customer (composite-tag second DCB)

## 1. Definition — the per-customer policy (configuration-as-events)

- [x] 1.1 `Promotions/CouponDefined.cs`: add `bool OneRedemptionPerCustomer = false` (defaulted so old serialized events fold as `false`).
- [x] 1.2 `Promotions/CouponView.cs`: add `bool OneRedemptionPerCustomer`; map it in `Create(CouponDefined)`.
- [x] 1.3 `Features/DefineCoupon.cs`: `DefineCoupon` command gains `bool OneRedemptionPerCustomer = false`; the endpoint threads it into `CouponDefined`. Validation unchanged.

## 2. The composite tag + boundary aggregate

- [x] 2.1 `Promotions/CouponCustomerTag.cs` (new): `record CouponCustomerTag(string Value)` with `static For(couponId, customerId) => new($"{couponId}|{customerId}")` — the single-scalar composite tag (verified shape; design.md decision 2).
- [x] 2.2 `Promotions/CustomerCouponUsage.cs` (new): id-less `[BoundaryAggregate]` mutable class, `int NetCount`, `Apply(CouponRedeemed) => NetCount++`, `Apply(CouponRedemptionReleased) => NetCount--` — the per-pair boundary state, mirroring `CouponUsage`.
- [x] 2.3 `Program.cs`: `opts.Events.RegisterTagType<CouponCustomerTag>("couponcustomer").ForAggregate<CustomerCouponUsage>()` alongside the existing `CouponId` registration.

## 3. Checkout — compose the second boundary (the DCB moment)

- [x] 3.1 `Promotions/CouponRedeemed.cs`: add `bool PerCustomer = false` (folded by `Order` for the release path).
- [x] 3.2 `Features/PlaceOrder.cs` (`RedeemWithDcbAsync`): when `coupon.OneRedemptionPerCustomer`, open the composite boundary before the global-cap read; reject `409 CouponAlreadyRedeemedByCustomer` when its net count `≥ 1`; on the success append, tag `CouponRedeemed` with **both** `CouponId` and `CouponCustomerTag.For(coupon.Id, customerId)` and set `PerCustomer = true`. The existing retry loop covers a `DcbConcurrencyException` from either boundary. Non-per-customer path byte-for-byte unchanged.

## 4. Release — return the customer's slot too

- [x] 4.1 `Ordering/Order.cs`: add `bool CouponPerCustomer = false`; `Apply(CouponRedeemed)` folds `CouponId` + `CouponPerCustomer`; `Apply(CouponRedemptionReleased)` clears both.
- [x] 4.2 `Promotions/CouponRelease.cs`: `AppendCouponRelease(session, orderId, couponId?, customerId?, perCustomer)` tags `CouponId` always and `CouponCustomerTag.For(couponId, customerId)` when `perCustomer`. No-op when `couponId is null`.
- [x] 4.3 `Ordering/{StockReservationOutcomeHandlers,PaymentHandlers,PaymentTimeoutHandler}.cs`: pass `stream.Aggregate.CouponId, stream.Aggregate.CustomerId, stream.Aggregate.CouponPerCustomer` to the release helper.

## 5. Tests

- [x] 5.1 `OrdersAppFixture.ResetAllDataAsync`: generalize the DCB-tag-table truncate from the literal `mt_event_tag_coupon` to `mt_event_tag_%` (catch `mt_event_tag_couponcustomer`).
- [x] 5.2 `CouponTests.cs` (+ a `DefineCouponAsync` overload carrying the flag): define-with-flag; same-customer second redemption (fresh cart) → `409 CouponAlreadyRedeemedByCustomer`; a different customer succeeds; **different customers concurrent → all succeed** (composite isolation — the reachable concurrency proof; a same-customer self-race is serialized by the one-open-cart invariant, design.md decision 3); cancelled per-customer redemption returns the customer's slot; a non-per-customer coupon lets one customer redeem twice.
- [x] 5.3 `OrderProjectionTests` (pure fold): `CouponRedeemed { PerCustomer: true }` sets `Order.CouponPerCustomer`; the release fold clears it.

## 6. Seeder + demo

- [x] 6.1 `src/CritterMart.Seeding/Program.cs`: `DemoCoupon` gains `OneRedemptionPerCustomer`; add `FIRSTORDER` (15% off, high cap, per-customer) to the demo set; the POST body carries `oneRedemptionPerCustomer`.
- [x] 6.2 `docs/demo-runbook.md` + `docs/demo-traffic.ps1`: a per-customer beat (redeem `FIRSTORDER`; the same customer's second attempt → `409`; a different customer still succeeds).

## 7. Docs + verify

- [x] 7.1 Workshop 003 → v1.3: new slice **6.5** in the slice table + §6 GWT + §4/§7 vocabulary (the composite tag + `CustomerCouponUsage`) + §8 item 6 marked IMPLEMENTED + Document History.
- [x] 7.2 Narrative 011 → v1.2: a per-customer Moment; retire the "no per-customer limit" leaves-out bullet; Document History.
- [x] 7.3 Full suite green: `dotnet test` (Orders) + `dotnet build` zero warnings. Client units unaffected (no frontend change this slice) but confirm with `--exclude "**/e2e/**"` if touched.
- [x] 7.4 **Integration-verified** end-to-end via Alba/Marten against real Postgres (Testcontainers): all six per-customer scenarios drive the actual HTTP checkout path + DCB boundary (127 Orders tests green). Full-**Aspire** live-drive (seeder `FIRSTORDER` + browser) offered to the owner as an additional confidence step, not blocking.
- [x] 7.5 Retro `docs/retrospectives/implementations/041-…` with spec-delta closure; prompt `docs/prompts/implementations/041-…`; READMEs; `openspec validate slice-6-5-per-customer-redemption-dcb --strict` green.
