# CritterMart — Handoff: Promotions slice 6.2 (advisory cart-review coupon UI)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-16.
> Direct successor to [`promotions-per-slice-loop.md`](promotions-per-slice-loop.md), now **consumed**: the
> DCB P0 core (slices 6.1/6.3/6.4) shipped as **PR #144** (`main` @ the #144 squash-merge) and its openspec
> change was archived in the **PR #145** tidy. This handoff commissions the trailing half of the increment.
> Read the artifacts rather than this doc for detail: [Workshop 003 v1.1 §§ 5.1/6.2](../workshops/003-promotions-event-model.md),
> [Narrative 011 "What the Customer does not yet see"](../narratives/011-customer-redeems-coupon.md),
> [retro 039](../retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md), and the archived spec
> `openspec/changes/archive/2026-07-16-slices-6-1-6-3-6-4-coupon-dcb/`.

## Mission for the next session

**Slice 6.2 — validate & price a coupon at cart review (the advisory query + storefront UI), plus the stale-comment tidy.**
This is the P1 storefront-UX half the 6.1/6.3/6.4 PR deliberately deferred. One consolidated PR
([[feedback-consolidate-slice-prs]]): OpenSpec proposal + narrative bump + implementation. Three parts:

1. **Backend — the advisory validation query (slice 6.2).** A read-only `GET /coupons/{code}/validate` (name TBD
   at proposal time) that resolves a code against `CouponView` (+ `CouponUsageView` for the "no longer available"
   affordance) and answers `{ valid, discountPercent }` or an invalid/exhausted signal. **Writes nothing** — the
   checkout append (slice 6.3) remains the sole authority; this is the deliberate advisory-vs-authoritative split
   (Workshop 003 § 3, § 6.2). `CouponUsageView` is already inline + queryable; `CouponView` already resolves by code.
2. **Frontend — W2 coupon field + W3 discount line (Workshop 003 § 5.1 wireframe deltas).**
   - **W2 cart review** (`client/src/cart/CartPage.tsx` + `cartQueries.ts`/`cartSchema.ts`): a coupon input +
     Apply that fires the advisory query, shows the discounted total preview, holds the code in **UI state**
     (reload forgets it — accepted round-one behavior), and renders inline errors ("This code isn't valid." /
     "This coupon is no longer available."). The applied code rides checkout as the existing
     **`POST /orders?couponCode=`** query param (slice 6.3 — already built; see `placeOrderMutation.ts`).
   - **W3 order confirmation** (`client/src/orders/OrderConfirmationPage.tsx` + `orderSchema.ts`): bind the
     `subtotal` / `discount` / `couponCode` fields — **already returned by the API** (`EnrichedOrderView`, shipped
     in #144), so W3 is pure binding, no backend change. Render `subtotal / discount (CODE) / total`.
3. **The stale-comment tidy** (retro 038 candidate, folded here per its plan): ~10 `client/src` files carry stale
   `X-Customer-Id` header-transport comments (the header was retired in the auth hard cutover). Files:
   `api/client.ts`, `cart/{cartMutations,cartQueries,CartPage}.ts(x)`, `catalog/catalogQueries.ts`,
   `orders/{orderQueries,placeOrderMutation,MyOrdersPage}.ts(x)` + their tests. Comment-only; the W2 frontend
   session is its natural host.

## What's already done (don't rebuild)

- **The whole DCB backend** — define/redeem/release, the cap, the race retry, the discount pricing. 185 tests
  green; live-verified. `POST /orders?couponCode=` and the priced `EnrichedOrderView` are shipped.
- **The demo coupon set** — seeder defines `FLASH20` (cap 3) + `WELCOME10`; demo-runbook Step 5d; `demo-traffic
  -CouponEvery`. 6.2 does not touch these.

## Locked / standing (do NOT re-litigate or trip on)

- **Wolverine stays 6.19.0** (CritterWatch beta.4, deliberate 1-minor lead — [[critterwatch-wolverine-version-coupling]]);
  do not bump past it. CritterWatch trial **expired 2026-07-10** (console blocked). Marten resolved **9.15.1**.
- **Frontend units collide with e2e** — run with `--exclude "**/e2e/**"` (`client/e2e/seeder.spec.ts` fails under `vitest run`).
- **Advisory is advisory** — the W2 check may lag or a slot may free by cancellation; never make it a guard. The
  checkout boundary is the only authority (design intent, not a bug to "fix").
- **Two upstream findings for the ai-skills/Marten pass** (not this session's job): DEBT row 3 (DCB skills say
  Polecat-only; Marten 9.15.1 path verified) + retro 039's `DeleteAllDocumentsAsync`-trips-on-`[BoundaryAggregate]`.

## Design-return cadence

The Promotions run has **1 implementation PR** (#144). Slice 6.2 is impl PR #2 — still within the 2–3 window
before a design-return is due. After 6.2, the next PR may need to be a design-return (a new narrative, the next
BC's workshop, or a tidy) per CLAUDE.md § Design-return cadence.

## Carry-forwards (unchanged, non-blocking)

- **Stale local branches** to clean: `tidy/critterwatch-beta4-update` (#143), `workshop/003-promotions-event-model`
  (#142) — flagged for `git branch -D` pending Erik's confirmation. (The coupon-dcb branch was deleted this session.)
- Two remote branches await delete/keep (`origin/feat/cart-identity-harmonization`, `origin/research/cw-telemetry-spike`);
  dependabot #132–139 re-triage; `UseDurableLocalQueues()` saga-timeout + `ReplenishTimeout` gaps in research docs;
  refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) deferred. POST-TALK: delete the five AppHost demo knobs.

## Suggested skills for the next session

- **`openspec-propose`** (or `opsx:propose`) — author + validate the 6.2 change (tool-backed, [[feedback-prefer-tool-backed-over-freeform]]).
  Likely one ADDED requirement on the `coupon-promotion` capability (the advisory validate query); the UI binding
  is behavior the narrative carries, not a new SHALL.
- **Frontend build**: `shadcn` / `shadcn-ui`, `tanstack-query-best-practices`, `react-hook-form`, `tailwind`,
  `improve-react` — the storefront's established stack (ADR 015/016).
- **`vitest`** — for the W2/W3 component + query tests (remember the `--exclude "**/e2e/**"`).
- **Close-out**: `/post-merge` → `/handoff` → `/blurb`; `/code-review` before the PR if wanted.

## First moves

1. Read Workshop 003 § 5.1 (W2/W3 wireframes) + § 6.2 GWT, Narrative 011's forthcoming-moments, and the archived
   `coupon-promotion` spec.
2. Confirm PR #145 (archive tidy) merged; sync main; delete the stale branches if Erik confirms.
3. Author the 6.2 OpenSpec proposal + narrative 011 bump (v1.1 → v1.2, a new Moment for the previewed discount),
   then the endpoint, then the W2/W3 binding + the comment tidy, then tests + live-verify + retro.
