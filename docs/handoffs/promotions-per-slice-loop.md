# CritterMart — Handoff: Workshop 003 shipped → run the Promotions per-slice loop

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-15.
> Direct successor to [`promotions-dcb-workshop.md`](promotions-dcb-workshop.md), which is now
> **consumed**: the Promotions event-modeling pass it commissioned shipped as **PR #142**
> (`main` @ `d3ab8e9`) — [Workshop 003](../workshops/003-promotions-event-model.md) v1.0 plus
> Workshop 001's v1.13 resolved-pointer amendment. This handoff does not repeat the workshop's
> content — read Workshop 003 itself; it is the authoritative model.

## ⚠️ Read this first — a dependency flag that predates the mission

Owner commit `b048b47` (direct to `main`, between PRs #140 and #142) bumped **WolverineFx.\* 6.16.0 → 6.19.0**
while **CritterWatch stays 1.0.0-beta.1**, which is compiled against 6.16.0. The pin note in
`Directory.Packages.props` (lines ~22–31) still says "PINNED AT 6.16.0" and documents the failure mode:
running a higher WolverineFx than CritterWatch targets throws a startup `TypeLoadException` in the
**CritterWatch console**. The dependabot CritterWatch **beta.2** PRs (#137/#139) may be the intended
lockstep partner — that is the owner's call, not this handoff's. **At session start: ask Erik whether
6.19.0 is deliberate (then take CW beta.2 in lockstep if it targets 6.19.0, update the pin note, and
refresh [[critterwatch-wolverine-version-coupling]]) or a slip (then revert to 6.16.0).** Until resolved,
treat the CritterWatch console as non-bootable (it is already blocked anyway — trial expired 2026-07-10)
and do NOT touch the Wolverine lines yourself without that conversation. Note the Marten transitive may
also have moved past 9.11.0 with Wolverine 6.19.0 — ADR 024's DCB verification was against 9.11.0
(DCB only gets *more* first-class in later 9.x, but re-confirm the resolved Marten version when the
implementation session restores packages).

## Mission for the next session

**The Promotions per-slice implementation loop.** Workshop 003 is the spec source; the loop is:
**OpenSpec proposal + narrative + implementation prompt + implementation + retro — consolidated in ONE
PR** per Erik's standing slice-PR preference ([[feedback-consolidate-slice-prs]]). This is the first
*code* session of the Promotions direction: the Orders store opts into the DCB schema, and slices
6.1–6.4 land.

**Scope guidance (workshop § 5):** 6.1 define (P0), 6.3 redeem-with-DCB (P0), 6.4 release-on-cancel (P0)
are the invariant-bearing core; 6.2 (advisory cart-review validation, P1) is the storefront UX layer and
the natural host for the stale-frontend-comments tidy (below). Whether all four land in one consolidated
PR or 6.2 trails as a frontend-focused second PR is a session-start scoping call for Erik.

## Locked decisions — do NOT re-litigate

- **ADR 024** (all of it): global per-coupon cap; DCB inside the Orders store; tagged strong-typed
  `CouponId`; `FetchForWritingByTags` over `EventTagQuery().Or<CouponId>(id)`; `DcbConcurrencyException`
  on the breaching race; Promotions = definitions-only, standalone service deferred; opt-in
  `tags TEXT[]` + GIN on the Orders `mt_events` only.
- **Workshop 003's four session forks** (owner-decided, recorded in its § 1): standalone workshop;
  cart-review UI-held coupon entry (single write point at `PlaceOrder { couponCode? }`, Cart aggregate
  untouched); definitions born as events (`DefineCoupon` → `CouponDefined`, configuration-as-events,
  seed-realized); release-on-cancel (compensating tagged `CouponRedemptionReleased`; boundary counts
  redemptions − releases).
- **Event vocabulary and GWTs** (workshop §§ 4/6) are the authoritative naming/behavior source —
  including the mandatory cap-breach → `CouponExhausted` rejection (no Order stream created) and the
  race scenario (loser retries into the breach rejection; exactly one racing order survives).

## Open questions the proposal/implementation must settle (workshop § 8 items 1–5)

1. **Append mechanics:** how `StartStream` composes with `FetchForWritingByTags` in one session/transaction,
   and where the `DcbConcurrencyException` retry policy seats (Wolverine retry vs. handler-local).
2. **`CouponUsageView` lifecycle:** inline (ADR 008-consistent) vs. async (a second teaser). Advisory role
   tolerates lag either way.
3. **Code uniqueness (6.1):** `CouponView` unique-index backstop (open-cart precedent) while definitions
   are seed-issued.
4. **Discount shape:** `discountPercent` modeled; a flat-amount widening is payload-level, not model-level.
5. **Demo coupon set:** a cap-3 flash coupon makes the race hand-demonstrable; demo-runbook +
   `demo-traffic.ps1` updates ride the implementation session.

**Capability mapping (settle at proposal time, per one-capability-per-aggregate):** likely a **new
capability** for the Coupon stream + `CouponView` (definitions aggregate) **plus `order-lifecycle`
MODIFIED requirements** (redemption/release ride Order streams; `PlaceOrder` gains optional
`couponCode`; `OrderStatusView` gains `subtotal`/`discount`/`couponCode?`).

## Upstream skill staleness — logged, don't get bitten

`docs/skills/DEBT.md` **row 3** (logged this session): the `marten-advanced-dynamic-consistency-boundary`
and `marten-advanced-cross-stream-operations` skills both present DCB as **Polecat-only** with a
Polecat-flavored API (`[BoundaryModel]`, `Load() => EventTagQuery.For(…)`, `IEventBoundary<T>`).
**Do not copy those symbols.** The Marten path ADR 024 verified in the pinned assembly is the one
CritterMart uses: `FetchForWritingByTags`, `EventTagQuery().Or<CouponId>(id)`, `DcbConcurrencyException`.
Use the skills for *concepts* (boundary-state shape, decision guidance); use ADR 024 + current Marten
docs for *symbols*. The upstream fix goes to the `ai-skills` repo (the DLQ-pitfall route), not local skills.

## What's true right now (2026-07-15, verified this session — don't re-derive)

- `main` @ `d3ab8e9` = the PR #142 squash-merge, verified (subject/sha/6 files/clean tree).
  Workshop 003 v1.0, Workshop 001 v1.13, workshops README 2→3, prompt+retro `workshops/003`,
  retros README workshops count back-filled 1→3.
- The openspec workspace has **0 active changes**.
- **Design-return cadence:** Workshop 003 *was* the interleave; the counter is reset — the Promotions
  implementation run has 2–3 PRs of room before the next design return.
- The local `workshop/003-promotions-event-model` branch is stale post-squash (delete with
  `git branch -D` when confirmed).

## Carry-forwards (triaged in retro workshops/003; unchanged unless noted)

- **⚠️ NEW — the Wolverine 6.19.0 / CritterWatch beta.1 mismatch** (top of this doc). Owner decision.
- **~10 `client/src` files with stale header-transport comments** (retro 038) — fold into slice 6.2's
  frontend work or a `tidy: frontend comments` pass.
- **Two remote branches** await Erik's delete/keep: `origin/feat/cart-identity-harmonization`,
  `origin/research/cw-telemetry-spike`.
- **Dependabot PRs #132–139** — partially overtaken by `b048b47` (Wolverine/Npgsql/Alba moved);
  #137/#139 (CritterWatch beta.2) are now potentially the *fix* for the mismatch rather than a risk.
  Re-triage after the mismatch conversation.
- `UseDurableLocalQueues()` saga-timeout decision + Marten-sibling `ReplenishTimeout` verification gap —
  open observations in research docs.
- **STANDING:** CritterWatch trial **expired 2026-07-10** (live-console verification BLOCKED till renewal);
  no transitive JasperFx dep bumps (suppressed MessagePack CVE); POST-TALK only: delete the five AppHost
  demo knobs; frontend units with `--exclude "**/e2e/**"` (pre-existing e2e/vitest collision).
- Refresh/revocation (ADR 023 Q15) + authZ/roles (Q16) — still deferred, noted for completeness.

## Orientation files (read first, in order)

1. This handoff (including the ⚠️ flag).
2. [`docs/workshops/003-promotions-event-model.md`](../workshops/003-promotions-event-model.md) — the model: §§ 4–6 are what the proposal transcribes; § 8 items 1–5 are the open calls.
3. [`docs/decisions/024-dcb-coupon-redemption-in-orders.md`](../decisions/024-dcb-coupon-redemption-in-orders.md) — locked reasoning + verified Marten symbols.
4. [`docs/retrospectives/workshops/003-promotions-event-model.md`](../retrospectives/workshops/003-promotions-event-model.md) — the capability-mapping note + composition-with-4.1 subtlety (Outstanding section).
5. `openspec/` conventions via the `openspec-propose` / `opsx:propose` skill — tool-backed, not freeform ([[feedback-prefer-tool-backed-over-freeform]]).
6. [`docs/skills/DEBT.md`](../skills/DEBT.md) row 3 — the Polecat-symbol trap.
7. `Directory.Packages.props` pin-note block — before any restore/build conversation.

## Working style (Erik's standing preferences)

Options + recommendation at genuine forks via `AskUserQuestion` with previews; consolidated slice PR;
live-verify on the full Aspire stack after the implementation lands and drive the demo flow (place an
order with the flash coupon, drive the cap to breach, show the race) — [[feedback-live-verify-after-changes]],
[[feedback-drive-demo-flows]]; flag deferred/non-terminal state at close; never add Claude commit trailers.

## Definition of done (the implementation session's)

- [ ] Wolverine/CritterWatch mismatch surfaced to Erik and resolved (bump-in-lockstep or revert) **before** building against the new package graph
- [ ] OpenSpec change authored + validated via the CLI (new coupon capability + `order-lifecycle` deltas), narrative authored as its sibling
- [ ] Slices 6.1/6.3/6.4 implemented per the workshop GWTs (6.2 per the session-start scoping call); Orders store opts into the DCB schema; cap-breach + race paths covered by tests
- [ ] Live-verified on the full stack incl. the coupon demo flow; demo-runbook + seeder/demo-traffic updated with the demo coupon set
- [ ] Workshop 003 Document History records what shipped (the spec-delta closure loop); retro authored before the PR opens
- [ ] Close-out ritual (`/post-merge` → `/handoff` → `/blurb`) once the PR lands
