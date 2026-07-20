# CritterMart — Handoff: implement slice 6.6 (per-customer coupon preview + tailored refusal copy)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-20.
> Direct successor to [`coupon-per-customer-preview-and-409-copy.md`](coupon-per-customer-preview-and-409-copy.md)
> — that handoff briefed the **design** session; this one briefs the **implementation** session that follows it.
> The design landed in [PR #160](https://github.com/erikshafer/crittermart/pull/160) (`f9c9ae7`).
> Read the artifacts, not this doc, for detail:
> [Workshop 003 §6.6](../workshops/003-promotions-event-model.md),
> [Narrative 011 Moment 6](../narratives/011-customer-redeems-coupon.md),
> [OpenSpec change `slice-6-6-per-customer-preview-and-copy`](../../openspec/changes/slice-6-6-per-customer-preview-and-copy/proposal.md),
> [retro docs/018](../retrospectives/docs/018-design-return-slice-6-6-per-customer-preview-and-copy.md).

## Where things stand

- **Slice 6.6 is fully modeled and specified; no code exists.** Workshop 003 → v1.4 (slice row, §4 event
  amendment, §5.1 wireframe delta, nine §6.6 GWTs, §7 read model, §8 item 6 forwarded), Narrative 011 → v1.3
  (Moment 6), and OpenSpec change `slice-6-6-per-customer-preview-and-copy` (`coupon-promotion`: 2 MODIFIED
  + 1 ADDED) all landed together and validate `--strict`.
- **`design.md` and `tasks.md` do NOT exist yet — authoring them is this session's first act.** Per
  CLAUDE.md §4a those two artifacts ride the *implementation* session, not the proposal session. `openspec
  list` reporting **"No tasks"** for this change is expected and correct, not an omission to be fixed by
  hunting for a missing file.
- **The design session resolved both scoping forks with the owner; do not re-open them.** One slice (not
  two); and the preview **enriches** the shipped `GET /coupons/{code}/validate` as optionally-authenticated
  rather than forking a second route.

## Immediate next job

Author `design.md` + `tasks.md` inside the existing change directory, then implement, then retro — one
session, one PR, per the standard loop. The proposal's **Impact** section is already a near-complete task
inventory; `tasks.md` is largely a matter of sequencing it.

**Suggested sequence** (each step leaves the suite green):

1. **Event contracts** — add the optional defaulted `customerId` to `CouponRedeemed` and
   `CouponRedemptionReleased`. Populate it at every append site.
2. **Projection** — `CustomerCouponUsageView` + its inline `MultiStreamProjection`, registered in
   `Program.cs`. Fold-only unit tests here.
3. **Query** — `ValidateCoupon.cs`: optional auth, the fourth status, the precedence ladder.
4. **Copy** — the reworded `409` detail in `PlaceOrder.cs`.
5. **Frontend** — the W2 `already_redeemed` state; send the bearer token on the validate call when signed in.

## Technical grounding — carry these forward (do not re-derive)

Validated against the actual code during the design session:

- **The load-bearing constraint.** Through slice 6.5 the `(coupon × customer)` pair lived **only** as a DCB
  tag. A tag is a **write-side query mechanism, not a Marten projection grouping key** — a
  `MultiStreamProjection` routes by an **event member** (`Identity<CouponRedeemed>(e => e.CouponId)`, the
  convention `CouponUsageView` already uses). This is why step 1 above exists and must come first: without
  `customerId` on the events, the view in step 2 cannot be written at all. Workshop 003 §4 carries the full
  note.
- **`customerId` is already threaded to every append site**, for the composite tag added in slice 6.5 —
  `Features/PlaceOrder.cs` (redemption) and the three cancellation sites via `Promotions/CouponRelease.cs`
  (`Ordering/{StockReservationOutcomeHandlers,PaymentHandlers,PaymentTimeoutHandler}.cs`). Step 1 is
  plumbing an existing value onto the event, not sourcing a new one.
- **Make the field optional with a default** — `string CustomerId = ""` or nullable, matching the
  `oneRedemptionPerCustomer` / `perCustomer` precedent. Old serialized events must fold without it.
- **`partial` is load-bearing on the projection class** (Marten 9 convention — the JasperFx source generator
  extends it; without it the host refuses to boot with `InvalidProjectionException`). See
  `docs/skills/marten-projection-conventions/SKILL.md` and its DEBT row 1.
- **Key the view `"{couponId}|{customerId}"`**, mirroring `CouponCustomerTag`'s own value shape.
- **Inline, not async** — the same call `CouponUsageView` made (Workshop 003 §8 item 2): no async daemon
  runs this round, and an async advisory view would sit perpetually empty.
- **The precedence ladder is `invalid` → `already_redeemed` → `exhausted` → `valid`**, mirroring checkout's
  ordering exactly. This is a spec'd requirement, not a preference — the two reasons send a customer to
  different remedies.
- **Auth pattern to mirror**: `user.CustomerId()` via `src/CritterMart.Orders/Auth/CustomerIdentity.cs`, as
  `PlaceOrder.cs` uses it. But note the difference: `/validate` is **optionally** authenticated — it must
  **not** be blanket-`[Authorize]`'d, and must never return `401`. An anonymous caller gets today's answer
  byte-for-byte.
- **`CustomerCouponUsage` (no `View`) is the throwaway DCB boundary aggregate** — `[BoundaryAggregate]`,
  id-less, never persisted. The new `CustomerCouponUsageView` is a *separate* type. Do not conflate them;
  the naming is one character apart by design (mirroring `CouponUsage` / `CouponUsageView`).
- **Test-fixture gotcha inherited from 6.5**: `OrdersAppFixture.ResetAllDataAsync` truncates
  `mt_event_tag_%`. Marten 9.15.1's `DeleteAllDocumentsAsync` trips on id-less boundary aggregates — retro
  039 has the workaround if it resurfaces.

## Watch-outs

- **Do not let the preview become load-bearing.** Standing lock. The composite DCB append at checkout is the
  sole authority; the preview is advisory even when personally accurate. No "optimization" that skips the
  boundary read because the preview already said `already_redeemed`.
- **The `409` keeps its status code and `CouponAlreadyRedeemedByCustomer` title token.** Only the human
  `detail` sentence changes. Tests asserting on the title must keep passing untouched.
- **The forward-only limitation is spec'd behavior, not a bug to fix.** `CustomerCouponUsageView` cannot see
  pre-6.6 redemptions; the preview may **under-warn** and must never **wrongly accuse**. There is a GWT
  scenario and an OpenSpec scenario for exactly this — implement it as written rather than reaching for a
  backfill. If a backfill ever seems warranted, it is a new slice with its own ADR conversation.
- **Anonymous callers of a per-customer coupon still see `valid`.** Also spec'd, also has a scenario. It is
  a deliberate boundary, not a hole.

## Open calls this session owns

Named in Workshop 003 §8 item 6 as implementation-session UI decisions with **no model consequence** — the
query already carries the data:

1. Whether to badge the `oneRedemptionPerCustomer` policy to anonymous shoppers ("one per customer" on the
   coupon field).
2. Whether to nudge a signed-out shopper to sign in for a sharper answer.

Decide them in `design.md`, or defer them explicitly in the retro. Either is fine; silence is not.

## Cadence note

PR #160 was the design-return interleave. This session is therefore **implementation PR #1** against the
Orders BC in a fresh 2–3 budget — no design-return is owed before it or immediately after it.

## Deferred / carry-forwards (unchanged, non-blocking)

- The other three options from the earlier fork, still unstarted: the **shared discount budget** DCB (third
  variant, ADR 024), the **doc-tidy** (context-map auth-cutover staleness), and **slice-6.2 visual
  browser-verify** (deferred #146).
- Live-verify on the full Aspire stack for slice 6.5 was offered, never run — could ride this session's
  verify if the stack is up anyway.
- Two remote branches await delete/keep; dependabot #132-139 re-triage; `UseDurableLocalQueues()` /
  `ReplenishTimeout` verification gaps; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) deferred.
  **POST-TALK:** delete the AppHost demo knobs.
- Post-merge archive tidy for `slice-6-6-per-customer-preview-and-copy` is a **separate later PR** (the
  established convention — the archive does not ride the implementation PR).

## Locked / standing (do NOT re-litigate)

- **Wolverine stays 6.19.0**; Marten **9.15.1**. CritterWatch trial expired 2026-07-10 (console blocked).
- **Advisory stays advisory.** The preview never gates redemption.
- **One slice, and the enriched-endpoint shape** — both settled with the owner in the design session.
- **PR hygiene:** post the full PR URL as plain visible text; no Claude commit trailer.
- **OpenSpec:** the change is already correctly named **without** a date prefix; the archive CLI stamps the
  date on archive.
