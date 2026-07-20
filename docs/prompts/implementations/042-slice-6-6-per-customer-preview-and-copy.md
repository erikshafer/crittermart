# Prompt: Implementations 042 — Slice 6.6 Per-Customer Coupon Preview + Tailored Refusal Copy

**Kind**: per-slice implementation (Promotions — the storefront half of the composite-tag second DCB)
**Files touched**: `docs/prompts/implementations/042-slice-6-6-per-customer-preview-and-copy.md` (new, this file); `openspec/changes/slice-6-6-per-customer-preview-and-copy/{design,tasks}.md` (authored **this** session — the proposal + spec delta landed in PR #160) + `openspec validate --strict` green; `src/CritterMart.Orders/Promotions/{CustomerCouponUsageView.cs (new),CouponRedeemed.cs,CouponRedemptionReleased.cs,CouponRelease.cs}`; `src/CritterMart.Orders/Features/{ValidateCoupon.cs,PlaceOrder.cs}`; `src/CritterMart.Orders/Program.cs` (third inline multi-stream projection); `tests/CritterMart.Orders.Tests/{CouponTests.cs,CustomerCouponUsageViewProjectionTests.cs (new)}`; `client/src/cart/{couponSchema.ts,CartPage.tsx,couponQueries.test.ts,CartPage.test.tsx}`; `docs/workshops/003-promotions-event-model.md` (v1.5 — status + §8 item 6 + history); `docs/narratives/011-customer-redeems-coupon.md` (v1.4); `docs/retrospectives/implementations/042-…` (at close)
**Mode**: solo implementation. **No modeling forks remain** — both were settled with Erik in the design session (PR #160, retro docs/018) and are locked. `design.md` + `tasks.md` are authored first, then code.
**Commit subject**: `feat: per-customer coupon preview + tailored refusal copy — slice 6.6`

## Framing

Slice 6.5 shipped CritterMart's second DCB: a composite `(coupon × customer)` boundary enforcing one-redemption-per-customer at checkout. It works, and it announces itself at the worst possible moment — a Customer applies `FIRSTORDER`, watches their total drop 15%, taps **Place Order**, and only then learns they used the offer months ago, in a sentence that reads like a database constraint.

This session lands the storefront half: the cart-review preview learns to speak personally, and the refusal learns to sound human. **The gap is customer-experience, not correctness.** The invariant does not move an inch — the composite DCB append at checkout remains the sole authority, and the preview stays advisory *even when it is personally accurate*.

**Slice 6.6 is the only slice in Workshop 003 that writes nothing at all.** No command, no new event, no new DCB, no new tag type. Its structural additions are a persisted view and one member on two existing events.

**⚠️ The load-bearing constraint — established, do not re-derive.** Through 6.5 the `(coupon × customer)` pair lived **only** as a `CouponCustomerTag`. A DCB tag is a **write-side query mechanism, not a Marten projection grouping key** — a `MultiStreamProjection` routes by an **event member** (`Identity<CouponRedeemed>(e => e.CouponId)`, the shipped `CouponUsageView` convention). So the per-customer view cannot be projected from the events as 6.5 left them, and the `customerId` amendment must land **before** the projection. Workshop 003 §4 carries the full note.

**⚠️ Locked, do not re-open.** One slice, not two. The preview **enriches** the shipped `GET /coupons/{code}/validate` as **optionally authenticated** rather than forking a sibling route or hard-`[Authorize]`ing a shipped anonymous endpoint. Wolverine **6.19.0** / Marten **9.15.1** — no version change.

## Goal

- **Events:** `CouponRedeemed` + `CouponRedemptionReleased` gain an optional defaulted `customerId` (old serialized events fold without it). Populated at every append site — the value is already threaded there for 6.5's composite tag, so this is plumbing, not sourcing.
- **View:** `CustomerCouponUsageView` — an **inline** `MultiStreamProjection` keyed `"{couponId}|{customerId}"`, folding redeem `+1` / release `−1`. `partial` is load-bearing. Registered in `Program.cs`. Distinct from the never-persisted `CustomerCouponUsage` boundary aggregate — same arithmetic, different existence.
- **Query:** `ValidateCoupon` becomes optionally authenticated (**no `[Authorize]`, never `401`**) and gains a fourth status `already_redeemed`, gated on the definition's `oneRedemptionPerCustomer` **and** an authenticated caller. Ladder: `invalid` → `already_redeemed` → `exhausted` → `valid`, mirroring checkout exactly.
- **Copy:** the `409` `detail` becomes *"You've already used this coupon — remove it to continue, or try another."* Status code and `CouponAlreadyRedeemedByCustomer` **title token unchanged**.
- **Frontend:** the W2 `already_redeemed` state; verify the bearer token already rides the validate call.
- **Tests:** the six preview scenarios + the reworded refusal + pure-fold projection units. Full suite green.
- **The two open UI calls** (Workshop 003 §8 item 6) decided in `design.md` — silence is not an option.

## Spec delta

This session **satisfies** the already-landed OpenSpec change `slice-6-6-per-customer-preview-and-copy` (`coupon-promotion`: 2 MODIFIED + 1 ADDED) and **authors its missing `design.md` + `tasks.md`** per CLAUDE.md §4a. Workshop 003 → **v1.5**: the status line and §8 item 6 flip from MODELED to IMPLEMENTED, and item 6's two named open questions are **answered** rather than left hanging. Narrative 011 → **v1.4**: Moment 6 flips from *modeled, not yet built* to running, and the "leaves out" bullet sharpens from *not built* to the forward-only under-warn asymmetry a Customer would actually notice. No new ADR (ADR 024 governs the enforcement mechanic, untouched here).

## Orientation files

1. **`docs/handoffs/slice-6-6-implementation.md`** — the technical grounding + watch-outs; carries the validated current-state facts so they need no re-deriving.
2. **`openspec/changes/slice-6-6-per-customer-preview-and-copy/proposal.md`** + `specs/coupon-promotion/spec.md` — the authoritative contract, including the four scenarios that look like bugs but are spec.
3. **`docs/workshops/003-promotions-event-model.md` §4 / §6.6 / §7** — the tags-are-not-groupable note, the nine GWTs, the view.
4. **`src/CritterMart.Orders/Promotions/CouponUsageView.cs`** — the multi-stream inline projection convention the new one mirrors exactly.
5. **`src/CritterMart.Orders/Features/ValidateCoupon.cs`** — the slice-6.2 query to enrich.
6. **`src/CritterMart.Orders/Auth/CustomerIdentity.cs`** — the auth pattern, and the reason it is *not* reused verbatim here (it throws on an absent claim).
7. **`client/src/api/client.ts`** (`authHeaders`) + `client/src/cart/couponSchema.ts` — the token already rides; the closed enum is what must widen.

## Working pattern

1. Author `design.md` + `tasks.md` in the existing change directory. **First act of the session** — `openspec list` reporting "No tasks" is expected, not an omission to hunt.
2. Implement in the handoff's five-step order, each step leaving the suite green: events → projection → query → copy → frontend.
3. Tests alongside; full `dotnet test` + client `vitest run`; `openspec validate --strict`.
4. Flip the workshop + narrative status; retro; PR.

## Out of scope

- **No backfill** of pre-6.6 redemptions. The forward-only limitation is **spec'd behavior with its own scenario**, not a bug — implement it as written. A backfill would be a new slice with its own ADR conversation.
- **No anonymous policy badge, no sign-in nudge** — decide and record, do not build (both would require the pinned-unchanged anonymous response to gain a field).
- **No change to any DCB, tag type, or checkout decision.** The preview must never become load-bearing; no "optimization" skips a boundary read because the preview already answered.
- No `perCustomerLimit > 1` generalization. No shared-discount-budget DCB (ADR 024's third variant). No coupon lifecycle. No standalone Promotions service.
- No Wolverine/Marten version change. No ADR. No opportunistic edits outside the named files — the deferred doc-tidy, the slice-6.2 browser-verify, the dependabot re-triage, and the two stale remote branches all stay deferred.
