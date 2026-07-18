# CritterMart — Handoff: Post Second-DCB — next direction

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-17.
> Direct successor to [`second-dcb-one-redemption-per-customer.md`](second-dcb-one-redemption-per-customer.md), now **consumed**:
> its commissioned build — the second DCB, `one-redemption-per-customer` — **shipped as PR #149** and merged.
> Read the artifacts, not this doc, for detail:
> [`coupon-promotion` spec](../../openspec/specs/coupon-promotion/spec.md) (now **6 requirements** — the per-customer one folded in on archive),
> [Workshop 003 §6.5 + §8 item 6](../workshops/003-promotions-event-model.md) (v1.3, IMPLEMENTED),
> [Narrative 011 Moment 5](../narratives/011-customer-redeems-coupon.md) (v1.2),
> [retro implementations/041](../retrospectives/implementations/041-slice-6-5-per-customer-redemption-dcb.md) (the build — mechanics + the model correction),
> and [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md) (§38 named this variant; the last unbuilt variant is §38's shared-budget).

## Where things stand

- **PR #149 merged** — slice 6.5, CritterMart's **second DCB** and first **composite-tag** boundary (`one-redemption-per-customer`). `main` @ the #149 squash-merge (`1067db7`), post-merge sync verified deterministically (subject/sha/files/clean tree).
- **PR #150 open** — the post-merge tidy: `openspec archive slice-6-5-per-customer-redemption-dcb`, folding the deltas into the main `coupon-promotion` spec (now **6 requirements**) and moving the change to `openspec/changes/archive/2026-07-18-slice-6-5-…/` (the CLI stamped 07-18 — a date-boundary quirk, harmless). **On #150 merge, workspace is at 0 active changes.**
- **Design-return cadence:** this was the **first** implementation PR since the reset — but it was itself a same-PR draw+build (Workshop 003 v1.3 + Narrative 011 v1.2 amended in the impl PR), so it doubles as a partial design-return. After **1–2 more** `coupon-promotion` implementation PRs, the next dedicated design-return comes due.
- **The two DCBs are the pattern base.** `coupon-promotion` now teaches a **count** cap (global, `CouponUsage` over `CouponId`) *and* an **existence** check (per-customer, `CustomerCouponUsage` over the composite `CouponCustomerTag`). Both are store-scoped Marten DCBs in Orders; the composite is a single-scalar encoded tag (no two-tag `.And<>()` exists — see the key fact below).

## Immediate next job — an open owner-level next-direction fork

No single obvious next build; put 2–4 options to the owner with previews ([[feedback-options-with-previews]], [[feedback-collaborate-on-decisions]]) before proposing. Candidates, strongest first:

1. **The third and last DCB variant — a shared discount budget** (Workshop 003 §8 item 7 / ADR 024 §38). A **summed dollar value** aggregated across streams rather than a count or an existence check — e.g. a coupon with a total budget `$B` of discount across all redemptions, rejected when the next redemption would exceed `B`. This completes the DCB variant set (count → existence → sum) and is the natural teaching capstone. New boundary aggregate summing `discount`; the retry loop and tag machinery are the same proven shape.
2. **The parked 6.5 follow-ons** (Workshop 003 §8 item 6): a **per-customer advisory preview** (the anonymous `validate` query would need the caller's `sub` + a per-customer advisory view) and **tailored W2 copy** for the `CouponAlreadyRedeemedByCustomer` 409. Storefront-UX polish; smaller than a new DCB.
3. **A design-return / doc-tidy** — the context-map auth-cutover staleness (retro docs/017) + the `customer-registry` TBD `## Purpose` (still unwritten) could bundle into one "post-cutover doc sweep."
4. **Slice-6.2 visual W2/W3 browser-verify** (deferred #146; Chrome extension was unconnected — API-level verify only).

## Technical grounding — carry these forward (do not re-derive)

- **The composite-tag API fact (verified against the resolved assembly, retro 041):** `JasperFx.Events` **v2.27.0.0** (Marten 9.15.1) `EventTagQuery` has `For`/`Or`/`Or<TEvent,TTag>`/`AndEventsOfType` but **NO two-different-tag `.And<>()`**, and a tag stores a **single scalar**. Model any `(A × B)` DCB boundary as **one strong-typed tag whose value encodes the pair** (`CouponCustomerTag("{couponId}|{customerId}")`), registered/queried through the same single-tag `RegisterTagType`/`.Or<T>()`/`WithTag` path. A shared-budget variant is a single-tag sum — no composite needed there.
- **DCB opt-in + concurrency (unchanged since 039):** `RegisterTagType<T>(suffix).ForAggregate<A>()` is the sole opt-in (NOT `EnableDcb()`); DCB optimistic concurrency is **cap-blind**, so writes use the **reload-and-retry loop** (`RedeemWithDcbAsync`, fresh session per attempt). Two `FetchForWritingByTags` compose on one session (6.5 proved it).
- **The model correction to remember (retro 041):** a per-customer/uniqueness DCB's *reachable* proof is the **cross-order existence check**, not a same-customer self-race — the **one-open-cart invariant** already serializes a single customer's checkouts (they collide on the cart stream, a plain `ConcurrencyException`, before the DCB). The DCB assertion is the transactional backstop; the meaningful concurrency test is *different subjects concurrent → all succeed* (isolation).
- **Test-cleanup wrinkle:** id-less `[BoundaryAggregate]` trips Marten 9.15.1's `DeleteAllDocumentsAsync`; `OrdersAppFixture.ResetAllDataAsync` SQL-truncates and now matches `mt_event_tag_%` (catches every DCB tag table). A new boundary aggregate inherits this — the reset already covers it.
- **The DCB skill is Polecat-framed** (`[BoundaryModel]`, `EventTagQuery.For().AndEventsOfType().Or()`, `Load()`) — verify against the assembly, not the skill (DEBT row 3).

## Suggested skills for the next session

- **`marten-advanced-dynamic-consistency-boundary`** — the DCB skill (translate its Polecat symbols to the Marten path CritterMart uses). Especially if the next build is the shared-budget variant.
- **`openspec-propose`** / the openspec CLI — to author the next change (no date prefix; the archive CLI stamps it).
- Local **`event-modeling`** skill — for any Workshop 003 slice + GWT authoring.
- Before touching Marten/Wolverine facts, prefer `ctx7` / the JasperFx ai-skills over training memory.

## Deferred / carry-forwards (unchanged, non-blocking)

- **Live-verify on the full Aspire stack was OFFERED, not yet run** for slice 6.5 — the DCB path is covered by real-Postgres integration tests (127 Orders green). Booting the stack + driving the `FIRSTORDER` per-customer flow (demo-runbook Step 5d) is available on request.
- Context-map auth-cutover doc staleness (retro docs/017); `customer-registry` TBD `## Purpose` still unwritten; slice-6.2 visual browser-verify; two remote branches await delete/keep; dependabot #132–139 re-triage; `UseDurableLocalQueues()`/`ReplenishTimeout` verification gaps; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) deferred. **POST-TALK:** delete the AppHost demo knobs.
- **Stale local branch:** `feat/slice-6-5-per-customer-redemption-dcb` is merged-and-squashed; safe to `git branch -D` once #150 is in (flagged in the post-merge sync, pending owner OK).

## Locked / standing (do NOT re-litigate)

- **Wolverine stays 6.19.0** (CritterWatch beta.4 deliberate 1-minor lead — [[critterwatch-wolverine-version-coupling]]); Marten **9.15.1**. CritterWatch trial **expired 2026-07-10** (console blocked). DCB needs **no** version bump (ADR 024).
- **Advisory stays advisory** — the `validate` query never guards checkout; the DCB append is the only authority. **Frontend units** run with `--exclude "**/e2e/**"`.
- **PR hygiene:** post the full PR URL as plain visible text ([[feedback-always-post-pr-url]]); no Claude commit trailer ([[feedback-no-claude-commit-trailer]]).
- **OpenSpec:** name active changes **without** a date prefix — the archive CLI stamps the date on archive.
