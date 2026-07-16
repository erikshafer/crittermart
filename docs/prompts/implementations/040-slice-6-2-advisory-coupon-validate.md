# Prompt: Implementations 040 — Slice 6.2 Advisory Cart-Review Coupon Validate (+ W2/W3 UI + stale-comment tidy)

**Kind**: per-slice implementation (Promotions storefront-UX half, consolidated per [[feedback-consolidate-slice-prs]])
**Files touched**: `docs/prompts/implementations/040-slice-6-2-advisory-coupon-validate.md` (new, this file); `openspec/changes/2026-07-16-slice-6-2-advisory-coupon-validate/{proposal.md,design.md,tasks.md,specs/coupon-promotion/spec.md}` (authored this session) + `openspec validate --strict` green; `src/CritterMart.Orders/Features/ValidateCoupon.cs` (new — `GET /coupons/{code}/validate`); `tests/CritterMart.Orders.Tests/CouponTests.cs` (+3 validate tests); `client/src/cart/{couponSchema.ts,couponQueries.ts}` (new) + `couponQueries.test.ts` (new); `client/src/cart/CartPage.tsx` (coupon field + preview) + `CartPage.test.tsx` (+4 coupon tests); `client/src/orders/{placeOrderMutation.ts (carry ?couponCode=),orderSchema.ts (add subtotal/discount/couponCode),OrderConfirmationPage.tsx (discount line)}` + their tests + the fixture backfill across `orders/{MyOrdersPage,OrderStatusPage,orderQueries,orderSchema,orderStatusJourney}.test.*`; the ~10 stale `X-Customer-Id` header-transport comment tidy in `client/src/{cart,catalog,orders}/*`; `docs/narratives/011-customer-redeems-coupon.md` (v1.0→v1.1); `docs/workshops/003-promotions-event-model.md` (Document History v1.2 — spec-delta closure); `docs/prompts/README.md` + `docs/retrospectives/README.md` (implementations 39→40); `docs/retrospectives/implementations/040-slice-6-2-advisory-coupon-validate.md` (at close)
**Mode**: solo implementation; the full per-slice loop (proposal + narrative bump + prompt) authored **this** session, then code — the trailing storefront-UX half of the Promotions increment PR #144 deferred
**Commit subject**: `feat: advisory cart-review coupon validate + W2/W3 storefront UI (Promotions slice 6.2)`

## Framing

PR #144 (slices 6.1/6.3/6.4) shipped the DCB core: a coupon can be defined, redeemed under a global cap, and released on cancellation, and the checkout append is the sole authority. What the storefront customer cannot yet do is **see the discount before committing**. This session lands slice **6.2** — the P1 storefront-UX half the DCB PR deliberately deferred: a **read-only advisory validation query** plus the **W2 cart-review coupon field** and the **W3 order-confirmation discount line**, per [Workshop 003 §5.1/§6.2](../../workshops/003-promotions-event-model.md) and [Narrative 011](../../narratives/011-customer-redeems-coupon.md)'s forthcoming preview.

**Locked at session start (with Erik):** the validate endpoint is **`GET /coupons/{code}/validate` → `{ code, status, discountPercent? }`** (a discriminated `valid`/`invalid`/`exhausted` status, client-side pricing) — chosen over a RESTful-resource read and a server-priced variant (AskUserQuestion with previews; design.md §1). Standing (do NOT re-open): Wolverine stays **6.19.0** (CritterWatch beta.4 coupling — [[critterwatch-wolverine-version-coupling]]); frontend units run with `--exclude "**/e2e/**"`.

**The advisory-vs-authoritative split is the teaching point, not a bug to fix.** The validate query reads `CouponView` + `CouponUsageView` and **writes nothing**; the checkout DCB append (slice 6.3, already built) stays the only authority. A `valid` preview can still lose the race at checkout (`409 CouponExhausted`); an `exhausted` preview does not stop the customer carrying the code. The query never guards (Workshop 003 §3).

## Goal

- **Backend:** `GET /coupons/{code}/validate` (`IQuerySession`, read-only) resolving the code → `valid` (+ `discountPercent`, net count below cap) / `exhausted` (net count at cap) / `invalid` (no definition); `200` always; writes nothing. +3 Alba integration tests (valid/invalid/exhausted, asserting no write).
- **W2:** `CartPage` coupon field + Apply → advisory query; `valid` previews `Subtotal / Discount (CODE) / Total` priced client-side in integer cents against the live cart total; `invalid`/`exhausted` render inline errors. Code held in UI state (reload forgets — §8 item 11). Rides checkout as `?couponCode=` (`placeOrderMutation` gains the optional variable). Vitest coverage.
- **W3:** `OrderConfirmationPage` renders `Subtotal / Discount (CODE) / Total` when `discount > 0`, plain Total otherwise. Pure binding — add `subtotal`/`discount`/`couponCode` to `OrderStatusViewSchema` (already returned by `EnrichedOrderView` since #144, unparsed). Backfill the order test fixtures for the now-required fields. Vitest coverage.
- **Tidy:** the ~10 stale `X-Customer-Id` header-transport comments → `Authorization: Bearer` (ADR 023); leave the correct historical note in `api/client.ts`.
- All tests green (`dotnet test` Orders + client Vitest `--exclude "**/e2e/**"`); tsc + vite build clean; live-verify on the Aspire stack.

## Spec delta

This session **authors and satisfies** one ADDED requirement on the `coupon-promotion` capability — *Validate and price a coupon at cart review* (the advisory read). The W2/W3 UI binding is journey behavior Narrative 011 carries, not a new SHALL. Authored in `openspec/changes/2026-07-16-slice-6-2-advisory-coupon-validate/specs/coupon-promotion/spec.md`, `openspec validate --strict` green. Workshop 003 §6.2 carries the GWT scenarios; this session satisfies them and records closure in Workshop 003's Document History (v1.2). Narrative 011 bumps v1.0→v1.1 (the previewed-discount Moment realized — a new Moment 2, redeem/release renumber to 3/4).

## Orientation files

1. **`docs/workshops/003-promotions-event-model.md` §5.1 (W2/W3 wireframes) + §6.2 (GWT)** — the model this session binds.
2. **`docs/narratives/011-customer-redeems-coupon.md`** — the "forthcoming" preview to realize.
3. **`openspec/changes/archive/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/specs/coupon-promotion/spec.md`** — the shipped capability the advisory query reads (`CouponView`, `CouponUsageView`).
4. **`src/CritterMart.Orders/Features/{DefineCoupon.cs,PlaceOrder.cs}`** — the code-resolve pattern (`Query<CouponView>().FirstOrDefault(c => c.Code == …)`) + the `?couponCode=` transport.
5. **`src/CritterMart.Orders/Promotions/{CouponView.cs,CouponUsageView.cs}`** — the two read models the query reads.
6. **`src/CritterMart.Orders/Ordering/EnrichedOrderView.cs`** — the W3 fields already on the wire.
7. **`client/src/cart/{CartPage.tsx,cartQueries.ts,cartSchema.ts}`** — the W2 screen + its query/schema idiom to mirror for coupons.
8. **`client/src/orders/{OrderConfirmationPage.tsx,orderSchema.ts,placeOrderMutation.ts}`** — W3 + the mutation to extend.

## Working pattern

Proposal + narrative bump first (design authoring), then the backend endpoint + tests (confirms the contract), then W3 (schema bump + binding), then W2 (the larger piece: query layer → field → checkout wiring), then the comment tidy, then full tests + live-verify + retro. Consolidated single PR.

## Out of scope

- **No checkout change** — slice 6.3's DCB path is untouched; the query is advisory only.
- **No cart-persistent coupon** — reload-forgets is accepted (the `CouponApplied` cart event is Workshop 003 §8 item 11's deferred alternative).
- **No richer DCB variants, coupon lifecycle, or standalone Promotions service** — Workshop 003 §8 long road.
- **No Wolverine bump past 6.19.0**; no CritterWatch console exercise (trial expired 2026-07-10).
