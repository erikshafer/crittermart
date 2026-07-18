# CritterMart — Handoff: Second DCB — `one-redemption-per-customer` (composite tag)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-17.
> Direct successor to [`post-promotions-next-direction.md`](post-promotions-next-direction.md), now **consumed**:
> its next-direction fork was put to the owner and **resolved to option 1 — build the second DCB**. The interim
> content-tidy it also named (option 2, `customer-registry` Purpose) shipped first as **PR #148** (`main` @ `ddb8635`).
> Read the artifacts, not this doc, for detail:
> [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md) (§38 names this exact variant),
> [Workshop 003 §8 item 6](../workshops/003-promotions-event-model.md),
> [`coupon-promotion` spec](../../openspec/specs/coupon-promotion/spec.md) (the first DCB, 5 requirements + Purpose),
> and [retro implementations/039](../retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md) (the first DCB **build** — the Marten mechanics to mirror).

## Where things stand

- **`main` @ `ddb8635`**, clean, **0 active OpenSpec changes**. PR #148 merged (customer-registry Purpose); the
  post-merge sync was verified deterministically (subject/sha/files/clean-tree).
- **Design-return cadence: RESET.** The second-DCB build will be the **first** implementation PR since the reset —
  well within budget. (After 2–3 implementation PRs against Orders/`coupon-promotion`, the next design-return comes due.)
- **The first DCB is shipped and is the pattern to build on.** `coupon-promotion` enforces a **global per-coupon
  redemption cap** via `FetchForWritingByTags<CouponUsage>` in the Orders store (slices 6.1/6.3/6.4, PR #144;
  advisory UI 6.2, PR #146). The second DCB layers a **composite tag** on top of that count-cap machinery.

## Immediate next job — draw + build the second DCB (`one-redemption-per-customer`)

This is the standout teaching follow-on: a **second** DCB that introduces the **composite `(coupon × customer)`
tag** pattern on top of the count-cap the first one taught — ADR 024 §38's named "more dynamic boundary,"
Workshop 003 §8 item 6. The invariant: **a given customer may redeem a given coupon at most once**, enforced by
the same store-scoped, transactional DCB read at checkout (no per-stream aggregate).

**This is a full vertical slice, not a tidy** — run the consolidated round-two shape (per [[feedback-consolidate-slice-prs]]):
workshop amendment (a new Workshop 003 slice + GWT) → OpenSpec change against `coupon-promotion` → narrative touch if
the customer journey shifts → implementation → retro, **all in one PR**. Name the OpenSpec change **without** a date
prefix (the archive CLI stamps it — retro docs/016 methodology note).

### The one genuine design fork to put to the owner first (do NOT assume)

**Does one-redemption-per-customer *compose with* the existing global cap on the same coupon, or is it a distinct
coupon *kind*?** Two coherent models — present both (with previews per [[feedback-options-with-previews]]) before
proposing:

1. **Compose** — every coupon carries *both* boundaries: the global cap (`≤ N` ever) **and** once-per-customer.
   Checkout opens the composite boundary `.And<CouponId>(id).And<CustomerId>(cust)` (reject if this customer already
   redeemed) *and* the existing count boundary. Richest teaching (two DCB reads, one transaction) but the most moving parts.
2. **A new coupon kind / flag on the definition** — `CouponDefined` gains a `oneRedemptionPerCustomer` bit (or a
   `perCustomerLimit`); the checkout picks the boundary shape from the definition. Cleaner configuration-as-events story,
   isolates the new pattern from the count-cap.
3. **A standalone once-per-customer coupon** (no global cap) — the purest minimal demonstration of the composite tag,
   least entangled with slice 6.3's retry loop.

The owner settled the *global-cap* invariant for the first DCB; this per-customer invariant's relationship to it is
the open modeling call. Resolve it, then draw the slice.

## Technical grounding — what the second DCB adds over the first (mirror retro 039, don't re-derive)

The first DCB's mechanics are the template — see [retro implementations/039](../retrospectives/implementations/039-slices-6-1-6-3-6-4-coupon-dcb.md)
and the `coupon-promotion` spec's *Redeem…under a global cap* requirement. In brief, what carries over vs. what is new:

- **Carries over:** `opts.Events.RegisterTagType<CouponId>("coupon").ForAggregate<CouponUsage>()` is the sole DCB
  opt-in (no `EnableDcb()`); the tagged event rides the **real order stream** (`StartStream` + `session.Events.Append(orderId, evt.WithTag(...))`);
  the **canonical reload-and-retry loop** (`RedeemWithDcbAsync`, fresh session per attempt) because DCB optimistic
  concurrency is **cap-blind** — a bare pre-check under-admits under a burst.
- **New for the composite tag:** register a second strong-typed tag (`CustomerId`); the checkout boundary query
  becomes composite over both tags; the invariant is a **existence** check ("has this `(coupon, customer)` pair a
  net redemption already?") rather than a **count** check — so the rejection is a new `409` (e.g. `CouponAlreadyRedeemedByCustomer`),
  and the reload-and-retry semantics differ (a per-customer boundary contends far less than the global one).
- **Verify the Marten path against the installed skill, don't trust the skill's framing.** The upstream
  `marten-advanced-dynamic-consistency-boundary` skill is **Polecat-framed**; CritterMart runs the **Marten** path on
  **Marten 9.15.1** (resolved; ADR 024 verified DCB present at 9.11.0). Confirm `EventTagQuery` supports the composite
  (`.And<>()` / multi-tag) shape on the resolved assembly **before** modeling the boundary read — this is the one place
  a stale skill could mislead. The first DCB used `.Or<CouponId>(id)`; the composite is the unverified delta.
- **Note the id-less boundary aggregate rough edge** (retro 039 footnote b): `CouponUsage` is `[BoundaryAggregate]`
  and id-less, which trips Marten 9.15.1's `DeleteAllDocumentsAsync` in test cleanup — worked around with a SQL truncate.
  A per-customer boundary aggregate will likely inherit the same test-cleanup wrinkle.

## Suggested skills for the next session

- **`marten-advanced-dynamic-consistency-boundary`** — the DCB skill (Polecat-framed; translate to the Marten
  `RegisterTagType`/`WithTag`/`FetchForWritingByTags`/`EventTagQuery` path CritterMart already uses). **Especially**
  leverage the JasperFx ai-skills here per the owner's standing ask — this is the DCB-centric session.
- **`marten-aggregate-handler-workflow`** + **`wolverine-http-marten-integration`** — for the checkout handler +
  endpoint shape the redemption rides.
- **`openspec-propose`** — to author the new `coupon-promotion` change (no date prefix).
- Local **`event-modeling`** skill — for the Workshop 003 slice + GWT authoring.
- Before touching Marten/Wolverine facts, prefer the `ctx7` CLI / JasperFx ai-skills over training memory (the
  composite-tag API is exactly the kind of recent detail memory may not carry).

## Deferred / carry-forwards (unchanged, non-blocking)

- **Context-map auth-cutover staleness (NEW, from retro docs/017):** `docs/context-map/README.md` § *Round-one stubs*
  still narrates the `X-Customer-Id` seam as live pending the auth slices, but the hard cutover retired it. A ready
  doc-tidy candidate — the *third* post-cutover doc-staleness the Promotions-era tidies surfaced (after the two TBD
  Purposes); a single consolidating "post-cutover doc sweep" could bundle it.
- **Visual W2/W3 browser-verify** (slice 6.2) — API-level live-verify only; the Claude-in-Chrome extension was
  unconnected. To close: connect the extension, boot the stack (demo-runbook Step 1 + 5d), drive `http://localhost:5273`.
- Two **remote** branches await delete/keep (`origin/feat/cart-identity-harmonization`, `origin/research/cw-telemetry-spike`);
  dependabot #132–139 re-triage; `UseDurableLocalQueues()` saga-timeout + `ReplenishTimeout` verification gaps;
  refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) deferred. **POST-TALK:** delete the AppHost demo knobs.

## Locked / standing (do NOT re-litigate)

- **Wolverine stays 6.19.0** (CritterWatch beta.4 deliberate 1-minor lead — [[critterwatch-wolverine-version-coupling]]);
  do not bump. Marten **9.15.1**. CritterWatch trial **expired 2026-07-10** (console blocked). DCB needs **no** version bump (ADR 024).
- **Advisory is advisory** — the 6.2 validate query never guards checkout; the DCB append is the only authority.
  **Frontend units** run with `--exclude "**/e2e/**"`.
- **PR hygiene:** post the full PR URL as plain visible text ([[feedback-always-post-pr-url]]); no Claude commit
  trailer ([[feedback-no-claude-commit-trailer]]).
- **OpenSpec:** name active changes **without** a date prefix — the archive CLI stamps today's date on archive.
