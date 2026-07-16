# Prompt: Implementations 039 — Slices 6.1 + 6.3 + 6.4 Coupon Redemption under a DCB Cap (CritterMart's first Dynamic Consistency Boundary)

**Kind**: per-slice implementation (Promotions DCB core, consolidated per [[feedback-consolidate-slice-prs]])
**Files touched**: `docs/prompts/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md` (new, this file); `openspec/changes/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/{proposal.md,design.md,tasks.md,specs/coupon-promotion/spec.md,specs/order-lifecycle/spec.md}` (all authored this session) + `openspec validate --strict` green; `docs/narratives/011-customer-redeems-coupon.md` (new) + `docs/narratives/README.md` (count 10→11); `src/CritterMart.Orders/Promotions/*` (new — coupon events, views, DCB boundary aggregate, tag, rejection shapes); `src/CritterMart.Orders/Features/{DefineCoupon.cs (new),PlaceOrder.cs (modified)}`; `src/CritterMart.Orders/Ordering/{Order.cs,OrderPlaced.cs,OrderStatusView.cs,StockReservationOutcomeHandlers.cs,PaymentHandlers.cs,PaymentTimeoutHandler.cs}` (modified); `src/CritterMart.Orders/Program.cs` (modified — `EnableDcb`, tag wiring, inline projections, unique index); `src/CritterMart.Seeding/Program.cs` (modified — demo coupon set); `docs/demo-runbook.md` + `docs/demo-traffic.ps1` (modified); `tests/CritterMart.Orders.Tests/*` (new + additive); `docs/workshops/003-promotions-event-model.md` (Document History — spec-delta closure); `docs/prompts/README.md` (count 38→39); `docs/retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md` (at close)
**Mode**: solo implementation; the full per-slice loop (proposal + narrative + prompt) authored **this** session, then code — the design artifacts were not pre-authored in a separate design-return session
**Commit subject**: `feat: coupon redemption under a DCB cap (Promotions) — slices 6.1/6.3/6.4`

## Framing

CritterMart's coordination story shows cascading messages ([[feedback-cascading-over-pmvh]]), Process Manager via Handlers (ADR 007), and a convention `Wolverine.Saga` — but never a consistency boundary that **spans many streams and aligns with no aggregate**. This session lands CritterMart's **first Dynamic Consistency Boundary**: a global per-coupon redemption cap ("usable ≤ *N* times, ever") enforced by Marten DCB inside the Orders store, per [ADR 024](../../decisions/024-dcb-coupon-redemption-in-orders.md) and [Workshop 003](../../workshops/003-promotions-event-model.md). Three P0 slices: **6.1 define** (configuration-as-events), **6.3 redeem-with-DCB** (the cap, the breach, the race), **6.4 release-on-cancel**. Slice 6.2 (advisory cart-review query + W2/W3 React + stale-comment tidy) **trails as PR #2** — a session-start scoping call settled with Erik. The DCB demo stands on this PR's API alone.

**Locked at session start (with Erik):** P0 core now / 6.2 trails; `CouponUsageView` **inline** (no async daemon runs, so an async advisory view would sit empty). Locked by ADR 024 (do not re-litigate): global per-coupon cap; DCB inside the Orders store; tagged strong-typed `CouponId`; `FetchForWritingByTags` over `EventTagQuery().Or<CouponId>(id)`; `DcbConcurrencyException` on the race; Promotions definitions-only (no fifth service); opt-in `tags TEXT[]` + GIN on Orders' `mt_events` only.

**⚠️ Dependency flag — RESOLVED, do not re-open.** Wolverine stays **6.19.0** with CritterWatch **beta.4** (targets 6.18.0) as a deliberate, documented 1-minor lead ([[critterwatch-wolverine-version-coupling]]; `Directory.Packages.props` pin note). Do **not** bump Wolverine past 6.19.0. Marten resolved to **9.15.1** (ADR 024 verified 9.11.0; DCB is more first-class in later 9.x — re-confirmed, not re-decided).

**⚠️ Skill trap ([`docs/skills/DEBT.md`](../../skills/DEBT.md) row 3).** The `marten-advanced-dynamic-consistency-boundary` / `-cross-stream-operations` skills present DCB as **Polecat-only** with Polecat symbols (`[BoundaryModel]`, `IEventBoundary<T>`, `EventTagQuery.For(…)`). **Do not copy them.** Use the verified Marten 9.15.1 surface (design.md): `EnableDcb()`, `TagEvent`/`WithTag`, `new EventTagQuery().Or<CouponId>(id)`, `FetchForWritingByTags<CouponUsage>`, `DcbConcurrencyException`.

**Build order:** 6.1 first (no DCB — unblocks the seeder), then a throwaway **DCB spike** (design.md decision 2 — how the tagged `CouponRedeemed` composes with `StartStream<Order>` in one `SaveChangesAsync`), then 6.3, then 6.4.

## Goal

- 6.1: `DefineCoupon` → `CouponDefined` on a coupon stream + inline `CouponView`; `POST /coupons` (seeder-driven); validation (`cap ≥ 1`, `discountPercent ∈ (0,100]`) → 400; duplicate code → 409 via partial-unique index.
- 6.3: `PlaceOrder` gains optional `couponCode`; the DCB path resolves the definition, opens `FetchForWritingByTags<CouponUsage>`, appends a tagged `CouponRedeemed` + a priced `OrderPlaced` in one transaction; **cap-breach → 409 `CouponExhausted` (no stream)**; unknown code → 409 `CouponInvalid`; **race → `DcbConcurrencyException` → one handler-local retry → exactly one surviving order**; no-coupon path byte-for-byte unchanged. `Order`/`OrderStatusView` gain `subtotal`/`discount`/`couponCode`; `CouponUsageView` inline.
- 6.4: the three cancellation sites (4.5/4.6/4.7) append a tagged `CouponRedemptionReleased` iff the order stream holds a `CouponRedeemed`; net count decrements; released slot reusable; at-most-one inherited from the terminal-once guard.
- Orders store opts into the DCB schema (`EnableDcb()`); all existing Orders tests stay green (confirm baseline at session start) + new unit + integration tests incl. cap-breach and race; `dotnet build` zero errors.
- Seeder defines the demo coupon set (`FLASH20` 20% cap 3 + `WELCOME10` 10% high cap); demo-runbook + demo-traffic updated; live-verified on the Aspire stack.

## Spec delta

This session **authors and satisfies** a new `coupon-promotion` capability (4 ADDED requirements: define / redeem-under-cap-DCB / release-on-cancel / advisory usage) and **modifies** `order-lifecycle` (2 MODIFIED requirements: *Place an order from the cart* gains optional `couponCode` + priced `OrderPlaced` + the tagged-`CouponRedeemed` composition; *Surface placement time and cancellation reason in the order view* gains `subtotal`/`discount`/`couponCode`). Authored in `openspec/changes/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/specs/`, `openspec validate --strict` green. Workshop 003 § 6 carries the GWT scenarios (modeled-not-built); this session satisfies them and records closure in Workshop 003's Document History. Narrative 011 is the human-readable companion; the OpenSpec change is the machine-readable contract.

## Orientation files

1. **`docs/workshops/003-promotions-event-model.md` §§ 4–6 + § 8 items 1–5** — the model and the open calls this session settles.
2. **`docs/decisions/024-dcb-coupon-redemption-in-orders.md`** — locked reasoning + verified Marten symbols.
3. **`openspec/changes/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/design.md`** — the implementation decisions, especially decision 2 (append mechanics — spike first) and decision 3 (handler-local retry seat).
4. **`docs/skills/DEBT.md` row 3** — the Polecat-symbol trap; use the Marten path only.
5. **`src/CritterMart.Orders/Features/PlaceOrder.cs`** — the slice-4.1 checkout the coupon path branches from; the multi-stream atomic write + cascades to preserve.
6. **`src/CritterMart.Orders/Ordering/{Order.cs,OrderPlaced.cs,OrderStatusView.cs}`** — the folds to extend with pricing + `CouponId?`.
7. **`src/CritterMart.Orders/Ordering/{StockReservationOutcomeHandlers.cs,PaymentHandlers.cs,PaymentTimeoutHandler.cs}`** — the three `OrderCancelled` append sites slice 6.4 extends (each already `FetchForWriting<Order>` + terminal-guard).
8. **`src/CritterMart.Orders/Program.cs`** — the `AddMarten` block (projections + the Cart partial-unique index precedent) where `EnableDcb()`, tag wiring, and the coupon projections/index go.
9. **`src/CritterMart.Seeding/Program.cs`** — the decoupled real-HTTP seed pattern to extend with `POST /coupons`.
10. **`tests/CritterMart.Orders.Tests/`** — existing Alba/Marten integration + pure-fold unit patterns.

## Working pattern

1. **Feature branch** `feat/slices-6-1-6-3-6-4-coupon-dcb` (already created).
2. **Design artifacts** (this session): proposal + two spec deltas + design.md + tasks.md; `openspec validate --strict` green; narrative 011; this prompt.
3. **Slice 6.1** — coupon events/view/tag, `DefineCoupon` endpoint, `Program.cs` projection + unique index; unit + integration (happy/dup/nonsensical).
4. **DCB spike (throwaway)** — prove the append composition (design.md decision 2) against Testcontainers Postgres; lock the mechanic in a comment; delete the spike.
5. **Slice 6.3** — `EnableDcb()` + tag wiring; `CouponUsage` boundary aggregate + `CouponUsageView`; `PlaceOrder` DCB branch (cap check, priced append, breach/unknown rejections, handler-local retry); priced `OrderPlaced`/`Order`/`OrderStatusView`; integration (happy-discounted/breach/race/unknown/no-coupon-parity).
6. **Slice 6.4** — `CouponRedemptionReleased`; `Order` release fold; conditional append at the three cancel sites; integration (release/reuse/no-coupon-no-op).
7. **Seeder + demo** — demo coupon set; demo-runbook coupon flow; demo-traffic.
8. **Verify** — `dotnet build`/`dotnet test` green; live-verify on Aspire + drive the coupon demo (place with FLASH20 → discount → breach the cap → show the race → cancel returns the slot) per [[feedback-live-verify-after-changes]] / [[feedback-drive-demo-flows]].
9. **Close** — Workshop 003 Document History; retro 039; prompts README 38→39; PR.

## Deliverable plan (in order)

| File | Status |
|---|---|
| `openspec/changes/2026-07-16-…/{proposal,design,tasks}.md` + `specs/{coupon-promotion,order-lifecycle}/spec.md` | new (this session) |
| `docs/narratives/011-customer-redeems-coupon.md` + README 10→11 | new (this session) |
| `src/CritterMart.Orders/Promotions/{CouponDefined,CouponView,CouponId,CouponRedeemed,CouponUsage,CouponUsageView,CouponRedemptionReleased,CouponExhausted,CouponInvalid}.cs` | new |
| `src/CritterMart.Orders/Features/DefineCoupon.cs` | new (`POST /coupons`) |
| `src/CritterMart.Orders/Features/PlaceOrder.cs` | modify (optional `couponCode`, DCB path) |
| `src/CritterMart.Orders/Ordering/{Order,OrderPlaced,OrderStatusView}.cs` | modify (pricing + coupon folds) |
| `src/CritterMart.Orders/Ordering/{StockReservationOutcomeHandlers,PaymentHandlers,PaymentTimeoutHandler}.cs` | modify (conditional release) |
| `src/CritterMart.Orders/Program.cs` | modify (`EnableDcb`, tag wiring, projections, index) |
| `src/CritterMart.Seeding/Program.cs` | modify (demo coupon set) |
| `docs/demo-runbook.md`, `docs/demo-traffic.ps1` | modify |
| `tests/CritterMart.Orders.Tests/*` | new + additive |
| `docs/workshops/003-promotions-event-model.md` | modify (Document History — closure) |
| `docs/retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md` + prompts README 38→39 | new (at close) |

## Out of scope

- **Slice 6.2** (advisory validation query `GET /coupons/{code}/validate` + W2 coupon field + W3 discount line + the ~10 stale `client/src` header-transport comment tidy) — **PR #2**, the following session.
- **Richer DCB variants** (one-redemption-per-customer composite tag; shared discount budget) — Workshop 003 §8 long road.
- **Coupon lifecycle** (expiry, disable/enable, edit) and **stacking** — additive later rounds.
- **A standalone Promotions service** — the deferred Customer-Supplier gate (ADR 024); would change the context map.
- **Any Wolverine/Marten version change** — the pin holds (see the dependency flag).
- **New cross-BC `CritterMart.Contracts` type or broker message** — redemption is intra-Orders by design; if a reason to cross the boundary appears, stop and raise it ([[feedback-flag-deferred-state-on-completion]]).
