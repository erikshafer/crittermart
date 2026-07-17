# Tasks — slice 6.2 advisory coupon validate

## 1. Backend — the advisory validate query

- [x] 1.1 `Features/ValidateCoupon.cs`: read-only `GET /coupons/{code}/validate` (Wolverine.Http, `IQuerySession`). Resolve `code` against `CouponView`; when found, read `CouponUsageView` net count and compare to `cap`. Return `CouponValidation { code, status, discountPercent? }` — `valid` (+ `discountPercent`) below cap, `exhausted` at cap, `invalid` when no definition resolves. `200` in every case; write nothing.
- [x] 1.2 Alba integration tests (`CouponTests.cs` or a new `CouponValidationTests.cs`): `valid` happy path (definition + net count below cap), `invalid` (unknown code), `exhausted` (net count at cap). Assert no event/stream written.

## 2. Frontend W2 — cart-review coupon field

- [x] 2.1 `cart/couponSchema.ts` (new): Zod schema for the discriminated `CouponValidation` response (`status` enum `valid|invalid|exhausted`, optional `discountPercent`). `z.infer` type.
- [x] 2.2 `cart/couponQueries.ts` (new): the advisory query's fetch (`fetchCouponValidation`) + a `useMutation`- or on-demand-`useQuery`-based Apply trigger (Apply is a user-initiated fetch, not an auto-run query). Boundary-parse through the schema.
- [x] 2.3 `cart/CartPage.tsx`: coupon input + Apply; on `valid` render the discounted-total preview (`Subtotal / Discount (CODE) / Total`) pricing `discountPercent` against the existing integer-cent `totalCents`; on `invalid`/`exhausted` render the inline error copy. Hold the applied code + validation in UI state. Pass the applied code to `placeOrder.mutate`.
- [x] 2.4 `orders/placeOrderMutation.ts`: accept an optional `couponCode` variable and append it as `?couponCode=` to `POST /orders` (encodeURIComponent). No-arg call stays the unchanged no-coupon checkout.
- [x] 2.5 Vitest: the coupon-query boundary parse; the Apply → preview and Apply → inline-error flows; `placeOrderMutation` carrying `?couponCode=` (and omitting it when no code).

## 3. Frontend W3 — order-confirmation discount line

- [x] 3.1 `orders/orderSchema.ts`: add `subtotal` / `discount` (non-negative numbers) + `couponCode` (nullable string) to `OrderStatusViewSchema`. These are already returned by `EnrichedOrderView` (#144) but not yet parsed.
- [x] 3.2 `orders/OrderConfirmationPage.tsx`: when `discount > 0` (or `couponCode` present), render `Subtotal / Discount (CODE) / Total`; unchanged single-Total otherwise.
- [x] 3.3 Vitest: W3 renders the discount breakdown when a coupon is present, and the plain Total when not.

## 4. Tidy — stale header-transport comments

- [x] 4.1 Correct genuinely-stale `X-Customer-Id` transport comments to `Authorization: Bearer` (ADR 023) in `cart/{cartMutations,cartQueries,CartPage}.ts(x)`, `catalog/catalogQueries.ts`, `orders/{orderQueries,placeOrderMutation,MyOrdersPage}.ts(x)` + affected test `it(...)` descriptions. Leave correct historical references intact (`api/client.ts` already frames the header as retired).

## 5. Docs + verify

- [x] 5.1 Narrative 011 → v1.1: realize the "previewed discount" Moment (the forthcoming Moment made present); append to Document History.
- [x] 5.2 Workshop 003 Document History: record slice 6.2 landed.
- [x] 5.3 Full suite green: `dotnet test` (Orders) + client Vitest (`--exclude "**/e2e/**"`).
- [x] 5.4 Live-verify on the Aspire stack — **API-level** (all three validate states confirmed live: `valid`/`invalid`/`exhausted` including the flip at cap; 4th redemption `409`; redeemed order's enriched view carries `subtotal`/`discount`/`couponCode`). The **visual W2/W3 browser drive is deferred to post-merge** — the Claude-in-Chrome extension was not connected this session; UI rendering is covered by the 123 client Vitest tests.
- [x] 5.5 Retro `docs/retrospectives/implementations/040-…` with spec-delta closure; prompt `docs/prompts/implementations/040-…`.
