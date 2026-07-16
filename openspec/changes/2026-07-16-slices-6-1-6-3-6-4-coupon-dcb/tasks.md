# Tasks: slices-6-1-6-3-6-4-coupon-dcb (slices 6.1 / 6.3 / 6.4)

## Implementation #039 — branch `feat/slices-6-1-6-3-6-4-coupon-dcb`

### Slice 6.1 — Define a coupon (build first — no DCB, unblocks the seeder)

- [ ] Add `src/CritterMart.Orders/Promotions/CouponDefined.cs` — `record CouponDefined(string CouponId, string Code, int DiscountPercent, int Cap)`
- [ ] Add `src/CritterMart.Orders/Promotions/CouponView.cs` — inline snapshot, `Create(CouponDefined)`; fields `{ Id=couponId, Code, DiscountPercent, Cap }`
- [ ] Add `src/CritterMart.Orders/Promotions/CouponId.cs` — strong-typed tag wrapper for the DCB `EventTagQuery().Or<CouponId>(id)` (record or readonly struct, per what `WithTag`/`Or<T>` accept — confirm at slice 6.3 spike)
- [ ] Add `src/CritterMart.Orders/Features/DefineCoupon.cs` — `record DefineCoupon(string Code, int DiscountPercent, int Cap)` + `POST /coupons` Wolverine.Http endpoint; validate `Cap >= 1` and `DiscountPercent in (0,100]` (400); `StartStream` the coupon; duplicate code → 409 via the unique index
- [ ] Modify `src/CritterMart.Orders/Program.cs` — `Projections.Snapshot<CouponView>(SnapshotLifecycle.Inline)`; partial-unique index on `CouponView.Code`

### Slice 6.3 — Redeem coupon at checkout (DCB)

- [ ] **Spike first (throwaway):** integration test proving how the tagged `CouponRedeemed` composes with `StartStream<Order>` in one `SaveChangesAsync` (design.md decision 2 mechanic a vs b); assert same-stream, `DcbConcurrencyException` under forced race, no boundary when no coupon. Lock the mechanic in a code comment; delete the spike.
- [ ] Add `src/CritterMart.Orders/Promotions/CouponRedeemed.cs` — `record CouponRedeemed(string OrderId, string CouponId, decimal Discount)`
- [ ] Add `src/CritterMart.Orders/Promotions/CouponUsage.cs` — the DCB **boundary aggregate** (net count fold; live target, not a persisted snapshot)
- [ ] Add `src/CritterMart.Orders/Promotions/CouponUsageView.cs` — inline snapshot, keyed by `couponId`, `+1`/`-1` fold; the advisory view
- [ ] Add `src/CritterMart.Orders/Promotions/CouponExhausted.cs` / `CouponInvalid.cs` — rejection shapes (or `Results.Problem` inline)
- [ ] Modify `src/CritterMart.Orders/Ordering/OrderPlaced.cs` — add `Subtotal`, `Discount`, `Total` (`Total = Subtotal − Discount`)
- [ ] Modify `src/CritterMart.Orders/Ordering/Order.cs` — fold `Subtotal`/`Discount`/`Total` + `CouponId?` from `CouponRedeemed`
- [ ] Modify `src/CritterMart.Orders/Ordering/OrderStatusView.cs` — add `Subtotal`/`Discount`/`CouponCode?`; fold from `OrderPlaced`
- [ ] Modify `src/CritterMart.Orders/Features/PlaceOrder.cs` — optional `couponCode`; resolve `CouponView`; open `FetchForWritingByTags<CouponUsage>`; cap check; tagged `CouponRedeemed` in-transaction; handler-local one-retry on `DcbConcurrencyException`; breach → `409 CouponExhausted`; unknown → `409 CouponInvalid`; no-coupon path unchanged
- [ ] Modify `src/CritterMart.Orders/Program.cs` — `opts.Events.EnableDcb()`; tag `CouponRedeemed`/`CouponRedemptionReleased` (`TagEvent`/`WithTag`); register `CouponUsage` (live) + `Snapshot<CouponUsageView>(Inline)`

### Slice 6.4 — Release redemption on cancellation

- [ ] Add `src/CritterMart.Orders/Promotions/CouponRedemptionReleased.cs` — `record CouponRedemptionReleased(string OrderId, string CouponId)`
- [ ] Modify `src/CritterMart.Orders/Ordering/Order.cs` — fold `CouponRedemptionReleased` (clear `CouponId`)
- [ ] Modify `src/CritterMart.Orders/Ordering/StockReservationOutcomeHandlers.cs` (4.5), `PaymentHandlers.cs` (4.6 decline), `PaymentTimeoutHandler.cs` (4.7) — append tagged `CouponRedemptionReleased` in the same transaction as `OrderCancelled` iff `stream.Aggregate.CouponId is not null`

### Tests

- [ ] Unit: `Order`/`OrderStatusView` coupon folds (subtotal/discount/total, CouponId set+cleared); `CouponUsage` boundary net-count fold; `CouponView` fold
- [ ] Integration (Alba/Marten, Testcontainers): 6.1 happy / duplicate-code 409 / nonsensical 400; 6.3 happy-discounted / cap-breach 409 CouponExhausted (no stream) / concurrent-race exactly-one / unknown-code 409 CouponInvalid / no-coupon parity; 6.4 release-on-cancel / reused-slot / no-coupon-cancel no-op
- [ ] `dotnet build` zero errors; `dotnet test` — existing Orders tests stay green + new tests pass

### Seeder + demo

- [ ] Modify `src/CritterMart.Seeding/Program.cs` — `DefineCoupon` the demo set via `POST /coupons`: `FLASH20` (20%, cap 3 — the race) + `WELCOME10` (10%, high cap); idempotent (409 → skip)
- [ ] Modify `docs/demo-runbook.md` — coupon-redemption flow (place → discount → drive cap to breach → race → cancel-returns-slot)
- [ ] Modify `docs/demo-traffic.ps1` — optionally apply `WELCOME10` on a fraction of placements

### Artifacts

- [ ] `docs/narratives/011-customer-redeems-coupon.md` (v1.0) + `docs/narratives/README.md` count 10→11
- [ ] `docs/prompts/README.md` implementations count 38→39 (prompt 039 committed this PR)
- [ ] `docs/workshops/003-promotions-event-model.md` Document History — record slices 6.1/6.3/6.4 landed (spec-delta closure)
- [ ] `docs/retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md` (spec-delta closure: new `coupon-promotion` capability + `order-lifecycle` MODIFIED landed; 6.2 deferred)
- [ ] Live-verify on the Aspire stack + drive the coupon demo flow
- [ ] `openspec archive slices-6-1-6-3-6-4-coupon-dcb -y` — **post-merge tidy PR**, not this PR (customer-data precedent)
