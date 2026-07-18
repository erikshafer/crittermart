# Prompt: Implementations 041 — Slice 6.5 One-Redemption-Per-Customer (CritterMart's second DCB — the composite `(coupon × customer)` tag)

**Kind**: per-slice implementation (Promotions DCB — the composite-tag second boundary, consolidated per [[feedback-consolidate-slice-prs]])
**Files touched**: `docs/prompts/implementations/041-slice-6-5-per-customer-redemption-dcb.md` (new, this file); `openspec/changes/slice-6-5-per-customer-redemption-dcb/{proposal,design,tasks}.md` + `specs/coupon-promotion/spec.md` (authored this session — **no date prefix**, the archive CLI stamps it) + `openspec validate --strict` green; `src/CritterMart.Orders/Promotions/{CouponCustomerTag.cs (new),CustomerCouponUsage.cs (new),CouponDefined.cs,CouponView.cs,CouponRedeemed.cs,CouponRelease.cs}`; `src/CritterMart.Orders/Features/{DefineCoupon.cs,PlaceOrder.cs}`; `src/CritterMart.Orders/Ordering/{Order.cs,StockReservationOutcomeHandlers.cs,PaymentHandlers.cs,PaymentTimeoutHandler.cs}`; `src/CritterMart.Orders/Program.cs` (second `RegisterTagType`); `src/CritterMart.Seeding/Program.cs` (`FIRSTORDER` demo coupon); `tests/CritterMart.Orders.Tests/{CouponTests.cs,OrdersAppFixture.cs,OrderProjectionTests.cs}`; `docs/workshops/003-promotions-event-model.md` (slice 6.5 + GWT + history v1.3); `docs/narratives/011-customer-redeems-coupon.md` (v1.2); `docs/demo-runbook.md` + `docs/demo-traffic.ps1`; `docs/prompts/README.md` + `docs/retrospectives/implementations/041-…` (at close)
**Mode**: solo implementation; the one modeling fork resolved with Erik at session start via `AskUserQuestion` with previews (per-customer as an opt-in flag on the definition — option 1). Full per-slice loop (OpenSpec change + prompt) authored **this** session, then code.
**Commit subject**: `feat: one-redemption-per-customer coupon (composite-tag DCB) — slice 6.5`

## Framing

The first DCB (slices 6.1/6.3/6.4, PR #144) taught a boundary that aligns with **one tag** — a global per-coupon cap, a *count* check over `CouponId`. This session lands CritterMart's **second DCB** and its first **composite tag**: **one redemption per customer**, an *existence* check over a `(coupon × customer)` pair — ADR 024 §38's "more dynamic boundary," Workshop 003 §8 item 6. It is the standout teaching follow-on because it shows the boundary shape being **chosen from configuration-as-events** (a `oneRedemptionPerCustomer` bit on `CouponDefined`) and **two DCB reads composing in one transaction**.

**Locked at session start (with Erik — the one genuine fork):** per-customer is an **opt-in policy on the definition** (option 1 of 3), not a universal law (option 2) and not a distinct coupon kind (option 3). `CouponDefined` gains `oneRedemptionPerCustomer` (default `false`); checkout opens the composite boundary only when set. FLASH20 stays a pure global-cap demo; a new `FIRSTORDER` demonstrates the composite.

**⚠️ Composite-tag API — VERIFIED, do not re-derive.** Reflected on the resolved `JasperFx.Events` **v2.27.0.0** (Marten 9.15.1): `EventTagQuery` has `For`/`Or`/`Or<TEvent,TTag>`/`AndEventsOfType` but **no two-different-tag `.And<>()`** (`AndEventsOfType` filters event *types*). The composite is a **single-scalar tag** — `record CouponCustomerTag(string Value)` with `For(couponId, customerId) => new($"{couponId}|{customerId}")` — structurally identical to the shipped `CouponId(string Value)`, registered and queried through the same proven path. **No new Marten API, no spike, no version bump.** The composite value matches no event property, so tag inference cannot manufacture it — it lands only by explicit `WithTag`.

**⚠️ Skill trap (unchanged from 039).** `marten-advanced-dynamic-consistency-boundary` is **Polecat-framed** (`[BoundaryModel]`, `EventTagQuery.For(…).AndEventsOfType().Or()`, `Load()` methods) — its `(course × student)` `AlreadySubscribed` example confirms the *shape* of a composite existence check, but **do not copy its symbols**. Use the Marten path the first DCB shipped: `RegisterTagType<T>(suffix).ForAggregate<A>()`, `[BoundaryAggregate]`, `FetchForWritingByTags<A>(new EventTagQuery().Or<T>(value))`, `evt.WithTag(...)`, `DcbConcurrencyException` → reload-and-retry.

**⚠️ Dependency flag — RESOLVED, do not re-open.** Wolverine stays **6.19.0** / CritterWatch **beta.4**; Marten **9.15.1**. DCB needs no version bump ([[critterwatch-wolverine-version-coupling]]).

## Goal

- **Definition:** `CouponDefined` + `CouponView` + `DefineCoupon` gain `oneRedemptionPerCustomer` (default `false`; old events fold `false`).
- **Composite tag + boundary:** `CouponCustomerTag` (single-scalar composite) + `CustomerCouponUsage` (id-less `[BoundaryAggregate]` counter); second `RegisterTagType(...).ForAggregate(...)` in `Program.cs`.
- **Checkout:** for a per-customer coupon, `RedeemWithDcbAsync` opens the composite boundary **before** the global-cap read → `409 CouponAlreadyRedeemedByCustomer` when net count `≥ 1`; on success tag `CouponRedeemed` with **both** tags and set `PerCustomer = true`; the existing retry loop covers a `DcbConcurrencyException` from either boundary (incl. the same-customer self-race). Non-per-customer path byte-for-byte unchanged.
- **Release:** `Order` folds `CouponPerCustomer`; `AppendCouponRelease` also carries the composite tag for a per-customer coupon so the customer's slot returns; the three cancel sites pass the customer id + flag.
- **Tests:** define-with-flag; same-customer second-redemption `409`; different customer succeeds; **same-customer concurrent double-submit → exactly one** (the concurrency proof); release returns the slot; a non-per-customer coupon lets one customer redeem twice; pure-fold unit tests. `ResetAllDataAsync` truncate generalized to `mt_event_tag_%`.
- Seeder `FIRSTORDER`; demo-runbook + demo-traffic per-customer beat; live-verify; `dotnet build`/`test` green.

## Spec delta

This session **authors and satisfies** an OpenSpec change against `coupon-promotion`: **MODIFIED** *Define a coupon* (+ `oneRedemptionPerCustomer`) and *Release a redemption when its order is cancelled* (+ the composite-tag decrement for per-customer coupons); **ADDED** *Enforce one redemption per customer for a per-customer coupon at checkout* (the composite boundary, `409 CouponAlreadyRedeemedByCustomer`, the self-race, the released-slot reuse). Authored in `openspec/changes/slice-6-5-per-customer-redemption-dcb/specs/` (no date prefix), `openspec validate --strict` green. Workshop 003 gains slice **6.5** + §6 GWT (modeled-and-satisfied this session — a same-PR draw+build); §8 item 6 flips to IMPLEMENTED. Narrative 011 → v1.2 carries the per-customer Moment.

## Orientation files

1. **`openspec/changes/slice-6-5-per-customer-redemption-dcb/design.md`** — the five decisions, especially decision 2 (verified single-scalar composite tag) and decision 3 (two boundary reads, one transaction, existing retry).
2. **`docs/decisions/024-dcb-coupon-redemption-in-orders.md` §38** — the named variant + locked Marten reasoning.
3. **`docs/retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md`** — the first DCB's mechanics to mirror (cap-blind retry; id-less-boundary test-cleanup wrinkle).
4. **`src/CritterMart.Orders/Features/PlaceOrder.cs`** — `RedeemWithDcbAsync`, the loop the composite read layers into.
5. **`src/CritterMart.Orders/Promotions/{CouponUsage,CouponId,CouponRelease}.cs`** — the single-tag boundary/tag/release the composite ones mirror.
6. **`src/CritterMart.Orders/Ordering/Order.cs`** + the three cancel-site handlers — the release fold + call sites to extend.
7. **`tests/CritterMart.Orders.Tests/{CouponTests.cs,OrdersAppFixture.cs}`** — the concurrency-test pattern + the SQL-truncate reset to generalize.

## Working pattern

1. Branch `feat/slice-6-5-per-customer-redemption-dcb` (created).
2. Design artifacts (this session): OpenSpec change + this prompt; `openspec validate --strict` green.
3. Definition flag → composite tag + boundary aggregate → `Program.cs` registration.
4. Checkout composite read + `409` + both-tags append; release both-tags + cancel-site plumbing.
5. Tests incl. the self-race burst; generalize `ResetAllDataAsync`.
6. Seeder + demo; `dotnet build`/`test`; live-verify on Aspire + drive the per-customer flow per [[feedback-live-verify-after-changes]] / [[feedback-drive-demo-flows]].
7. Close — Workshop 003 v1.3 + Narrative 011 v1.2 (spec-delta closure); retro 041; prompts README; PR (full URL as plain text — [[feedback-always-post-pr-url]]).

## Out of scope

- **Per-customer advisory preview** — the anonymous `GET /coupons/{code}/validate` holds no customer identity; previewing "you already used this" needs the caller's `sub` + a per-customer advisory view. Follow-on.
- **`perCustomerLimit > 1`** — the invariant is exactly "once"; a bool is its crispest form.
- **Shared discount budget** (the third DCB variant) and a **standalone Promotions service** — Workshop 003 §8 long road.
- **Dedicated frontend copy** for the new `409` — checkout already surfaces the ProblemDetails; a tailored message is a follow-on (verify the storefront degrades gracefully, do not build new UI).
- **Any Wolverine/Marten version change** — the pin holds.
