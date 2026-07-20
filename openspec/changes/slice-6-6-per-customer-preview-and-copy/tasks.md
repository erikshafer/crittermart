# Tasks — slice 6.6 per-customer coupon preview + tailored refusal copy

Sequenced so each numbered section leaves the suite green. Section 1 is a hard prerequisite of section 2 (design.md decision 1: a `MultiStreamProjection` routes by an event **member**, and the `(coupon × customer)` pair existed only as a DCB tag through slice 6.5).

## 1. Event contracts — the `customerId` amendment

- [x] 1.1 `Promotions/CouponRedeemed.cs`: add `string CustomerId = ""` as the **last** positional parameter (defaulted so pre-6.6 serialized events fold unattributed, and so existing construction sites keep compiling).
- [x] 1.2 `Promotions/CouponRedemptionReleased.cs`: add `string CustomerId = ""`, same shape.
- [x] 1.3 `Features/PlaceOrder.cs`: pass `customerId` (the local already held for the composite tag) into `new CouponRedeemed(...)` — **unconditionally**, not gated on `OneRedemptionPerCustomer` (design.md decision 1).
- [x] 1.4 `Promotions/CouponRelease.cs`: pass `customerId` into `new CouponRedemptionReleased(...)`. The helper signature and its three callers are unchanged — `customerId` is already a parameter, threaded for slice 6.5's composite tag.

## 2. The advisory per-customer view

- [x] 2.1 `Promotions/CustomerCouponUsageView.cs` (new): the document (`string Id`, `int NetCount`) + `static KeyFor(couponId, customerId) => $"{couponId}|{customerId}"` — the view's own canonical key construction, mirroring `CouponCustomerTag`'s value shape without depending on the tag type (design.md decision 2).
- [x] 2.2 Same file: `public partial class CustomerCouponUsageViewProjection : MultiStreamProjection<CustomerCouponUsageView, string>` routing both events by `KeyFor(e.CouponId, e.CustomerId)`, folding `CouponRedeemed` `+1` / `CouponRedemptionReleased` `−1`. **`partial` is load-bearing** (`docs/skills/marten-projection-conventions`, DEBT row 1).
- [x] 2.3 `Program.cs`: `opts.Projections.Add<CustomerCouponUsageViewProjection>(ProjectionLifecycle.Inline)` alongside `CouponUsageViewProjection`. **Inline, not async** — no daemon runs this round (Workshop 003 §8 item 2).

## 3. The validate query — optional auth + the fourth status

- [x] 3.1 `Features/ValidateCoupon.cs`: add `AlreadyRedeemed = "already_redeemed"` to `CouponValidationStatus`.
- [x] 3.2 Same file: the endpoint takes `ClaimsPrincipal user`. **No `[Authorize]`**, and it must never answer `401`. Read `sub` directly rather than via `user.CustomerId()` — that helper throws on an absent claim, which is correct behind `[Authorize]` and wrong here where anonymous is the normal case (design.md decision 3; comment the divergence at the call site).
- [x] 3.3 Same file: insert the `already_redeemed` branch **between** `invalid` and `exhausted`, gated on `coupon.OneRedemptionPerCustomer && sub is non-empty` — either gate failing skips the view load entirely, so a global-cap-only coupon and an anonymous caller cost exactly slice 6.2's queries. `IQuerySession` stays (the structural no-append guarantee).

## 4. The refusal copy

- [x] 4.1 `Features/PlaceOrder.cs`: the `CouponAlreadyRedeemedByCustomer` `detail` becomes the fixed sentence **"You've already used this coupon — remove it to continue, or try another."** The `409` and the title token are **unchanged** — existing tests asserting on the title must keep passing untouched.

## 5. Frontend

- [x] 5.1 `client/src/cart/couponSchema.ts`: add `"already_redeemed"` to `CouponStatusSchema`. Load-bearing — the enum is closed, so without it the new status fails zod parsing and surfaces as a fetch error rather than UI copy (design.md decision 5).
- [x] 5.2 `client/src/cart/CartPage.tsx`: a third branch in `CouponField`'s `errorMessage` ladder → *"You've already used this coupon."* The existing `role="alert"` wiring and the "only a `valid` answer holds" rule cover the rest.
- [x] 5.3 No token work — `fetchParsed` → `authHeaders(ctx)` already attaches the bearer when signed in (Convention 4, ADR 023). Verify, don't add.

## 6. Tests

- [x] 6.1 `CouponTests.cs`: a `ValidateCouponAsync` overload taking an optional `customerId` (bearer when supplied, anonymous when not).
- [x] 6.2 Integration — authenticated customer who has already redeemed a per-customer coupon → `already_redeemed`, no `discountPercent`.
- [x] 6.3 Integration — authenticated customer who has **not** redeemed → `valid` with `discountPercent`.
- [x] 6.4 Integration — **anonymous** caller of a per-customer coupon already redeemed by someone → `valid`, never `401`, never `already_redeemed` (the pinned slice-6.2 contract).
- [x] 6.5 Integration — a global-cap-only coupon redeemed once by the caller → `valid` (the view is never consulted).
- [x] 6.6 Integration — precedence: already-redeemed **and** globally exhausted → `already_redeemed`, **not** `exhausted`.
- [x] 6.7 Integration — a cancelled redemption restores the preview to `valid`.
- [x] 6.8 Integration — the reworded `409` detail at checkout, with the title token asserted unchanged.
- [x] 6.9 Pure-fold unit tests for `CustomerCouponUsageViewProjection`: net-of-releases arithmetic; two customers tracked independently.
- [x] 6.10 Frontend — `couponQueries.test.ts` / `CartPage.test.tsx`: the `already_redeemed` status parses and renders its copy without applying the coupon.

## 7. Docs + close-out

- [x] 7.1 Prompt + retro `docs/{prompts,retrospectives}/implementations/NNN-slice-6-6-…`.
- [x] 7.2 `openspec validate slice-6-6-per-customer-preview-and-copy --strict` passes; full `dotnet test` + client tests green.
- [x] 7.3 Out of scope, confirmed: no backfill (design.md decision 7), no anonymous policy badge, no sign-in nudge (design.md decision 6), no ADR, no version bumps.
