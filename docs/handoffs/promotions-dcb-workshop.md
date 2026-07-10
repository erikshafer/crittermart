# CritterMart — Handoff: Promotions/DCB direction chosen → run the event-modeling pass

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-10.
> Direct successor to [`post-auth-next-direction.md`](post-auth-next-direction.md), whose open fork
> this session closed: the six-candidate Long-road fork is resolved — **Promotions via DCB is chosen**,
> recorded in [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md) (shipped, PR #131).
> This handoff does **not** repeat ADR 024's reasoning — read the ADR if that history is needed; it is
> not required to execute this session's mission.

## Mission for the next session

The next direction is **chosen and its decision layer is shipped**. This session's job is the
**first build-facing design step** of that direction: the **Promotions event-modeling pass**. It is
still **design-phase, not implementation** — produce the event model (events, commands, views, swim
lanes, vertical slices, GWT scenarios), not code.

**Do not default to code.** The per-slice loop (OpenSpec proposal → narrative → prompt → implement →
retro) comes *after* the workshop pass lands and a fresh implementation handoff is authored.

## The first decision this session must make (before authoring)

**Where does the Promotions event model live — a new Workshop 003, or an amendment to Workshop 001?**
This is a genuine modeling fork, deliberately left open by ADR 024:

- The **coupon-definition concept** is new territory → argues a standalone **Workshop 003 (Promotions)**.
- But **redemption executes on Orders streams** (Workshop 001's territory, since the DCB lives *inside*
  the Orders store) → the pass adds slices to the **Orders swim lane** either way.

Present this fork to Erik (AskUserQuestion with previews, per his standing preference) with a
recommendation before authoring. A reasonable lean: a **new Workshop 003** that owns the Promotions
concept and coupon definitions, with its redemption slices drawn in an **Orders swim lane** — keeping
the "one workshop per BC concept" convention while honoring that execution lands in Orders. But let
Erik decide.

## ADR 024 decisions — locked, do NOT re-litigate (carry into the workshop)

- **Direction:** Promotions via Marten **Dynamic Consistency Boundary (DCB)** — CritterMart's first DCB.
- **Invariant:** a **global per-coupon redemption cap** ("coupon usable ≤ N times, ever").
- **Topology:** redemption + cap enforcement live **inside the Orders store**; **Promotions contributes
  coupon *definitions* only** this increment (Published-Language / seed Orders consumes); a **standalone
  Promotions service is deferred** (the Customer-Supplier redemption-gate option, mirroring Inventory).
- **Mechanism:** redemption events tagged by a strong-typed `CouponId`; the cap enforced at checkout via
  `FetchForWritingByTags` over `EventTagQuery().Or<CouponId>(id)`, `DcbConcurrencyException` on the
  breaching race. Likely a `CouponUsage` DCB view aggregating the count across order streams.
- **Verified facts (don't re-verify):** DCB is first-class in the pinned **Marten 9.11.0** (confirmed in
  `Marten.dll`) — **no Polecat, no Wolverine/Marten bump** (CritterWatch coupling holds); DCB is
  **store-scoped**, which is *why* enforcement must live in the one store that sees all redemptions =
  Orders. Opt-in schema: `tags TEXT[]` + GIN index on the Orders `mt_events`.
- **Richer variants named as future increments** (not this pass): one-redemption-per-customer (composite
  `(coupon × customer)` tag) and a shared discount budget (summed value across streams).

## What's true right now (2026-07-10, verified this session — don't re-derive)

- `main` @ `e43bb59`, clean, pushed, synced. That commit is the squash-merge of **PR #131** (ADR 024,
  design-only). Post-merge sync verified (subject/sha/files/clean-tree all confirmed).
- **ADR 024's decision layer is fully shipped and archived into the doc set**: the ADR + context-map
  § Long road resolution + `structural-constraints.md` **v1.9** + vision § Long road (4th increment,
  chosen-but-not-yet-built) + decisions/README index + retrospectives/README (decisions 4→5) + retro
  `decisions/005`. No code, no `openspec` change yet — that's this direction's *next* phase.
- **Workshop 001 was deliberately NOT edited by PR #131** (no opportunistic edits) — its Promotions/DCB
  parking-lot entry (§ 9) still points at context-map § Long road, which now reads "chosen." The workshop
  amendment (or new Workshop 003) is *this* session's delta.
- **Branch/stack hygiene done this session:** all five stale/merged local branches deleted; local stack
  confirmed down (no live containers). Two *remote* branches await Erik's call:
  `origin/feat/cart-identity-harmonization` and `origin/research/cw-telemetry-spike`.
- **CritterWatch trial expired 2026-07-10** — renewal unresolved. Treat any live-CritterWatch-console
  verification as **blocked** until it resolves; check before attempting.

## Smaller carry-forward items (not blocking, pick up opportunistically)

- **Two remote branches** (`origin/feat/cart-identity-harmonization`, `origin/research/cw-telemetry-spike`)
  — left for Erik's decision; delete or keep as remote backups.
- **The `X-Customer-Id` hard cutover** — implementation debt on the *shipped auth* increment (remove the
  dev-only fallback; migrate seeder/tests/`demo-traffic.ps1` to mint real JWTs). In retro 037's
  Outstanding; **not** part of the Promotions direction.
- **`UseDurableLocalQueues()` decision for saga timeouts** + the **Marten-sibling `ReplenishTimeout`
  verification gap** — both still open observations in research docs; unchanged.
- **Five AppHost demo knobs** (`Payment__DeclineOverAmount`, `Payment__AuthDelay`, `Orders__PaymentTimeout`,
  `Inventory__ReplenishTimeout`, `Identity__EmailChangeTimeout`) — **post-talk only**, do not delete yet.
- **Do NOT bump Wolverine past 6.16.0** ([[critterwatch-wolverine-version-coupling]]) or transitive
  JasperFx deps (suppressed MessagePack CVE — [[feedback-no-transitive-dep-bumps]]).
- **Refresh tokens + server-side revocation** (ADR 023 Q15) and **authorization/roles** (ADR 023 Q16)
  remain deferred — noted for completeness.
- **Pre-existing frontend test collision** (not ours): `client/e2e/seeder.spec.ts` fails under
  `vitest run`; run frontend units with `--exclude "**/e2e/**"`.

## Orientation files (read first, in order)

1. This handoff.
2. [`docs/decisions/024-dcb-coupon-redemption-in-orders.md`](../decisions/024-dcb-coupon-redemption-in-orders.md) — the chosen direction's full reasoning and forward questions.
3. [`docs/retrospectives/decisions/005-adr024-promotions-dcb.md`](../retrospectives/decisions/005-adr024-promotions-dcb.md) — how the decision was reached; the workshop-location fork is named in its Outstanding section.
4. [`docs/workshops/001-crittermart-event-model.md`](../workshops/001-crittermart-event-model.md) — the Orders/Catalog/Inventory event model the Promotions slices attach to (Orders swim lane), and the candidate host for an amendment.
5. [`docs/workshops/README.md`](../workshops/README.md) — workshop authoring conventions + frontmatter `version:` discipline.
6. [`docs/context-map/README.md`](../context-map/README.md) § Long road — the resolved Promotions entry.
7. `CLAUDE.md` — the pipeline (Event Modeling workshop → per-slice loop) and the design-return cadence.

## Working style (Erik's standing preferences — carried from memory)

Present options + a recommendation at genuine forks, ideally via `AskUserQuestion` with previews
([[feedback-collaborate-on-decisions]], [[feedback-options-with-previews]]); prefer tool-backed artifacts
over freeform (`openspec` CLI — [[feedback-prefer-tool-backed-over-freeform]]); ask where something should
live before writing it if Erik says "persist"/"make durable" ([[feedback-ask-where-to-persist]]);
live-verify against the real stack after non-trivial *code* changes and drive demo flows yourself
([[feedback-live-verify-after-changes]], [[feedback-drive-demo-flows]]); flag deferred/non-terminal state
explicitly ([[feedback-flag-deferred-state-on-completion]]).

## Definition of done

- [ ] Workshop-location fork (Workshop 003 vs Workshop 001 amendment) presented to Erik and decided
- [ ] The Promotions event model authored — events, commands, views, swim lanes (incl. Orders), vertical
      slices with reads-from/writes-to lists, and ≥1 GWT happy path per slice, plus explicit failure paths
      (the cap-breach → redemption-rejected path is mandatory, not implied)
- [ ] DCB mechanics deferred to the `marten-advanced-dynamic-consistency-boundary` skill, not re-derived
- [ ] Workshop frontmatter `version:` bumped + Document History row (per workshop conventions)
- [ ] This handoff's carry-forwards triaged — picked up, deferred-with-reason, or logged
- [ ] Close-out ritual (`/post-merge` → `/handoff` → `/blurb`) once this session's PR lands

## Suggested skills

- `event-modeling` (in-repo, `docs/skills/event-modeling/SKILL.md`) — the workshop authoring pattern.
- `marten-advanced-dynamic-consistency-boundary` — DCB mechanics (`[BoundaryModel]`, `EventTagQuery`,
  `FetchForWritingByTags`, cross-stream consistency). Note: its title frames DCB as Polecat — CritterMart
  uses the **Marten 9.11** path ADR 024 verified.
- `domain-modeling` — if the Promotions vocabulary (coupon, redemption, cap) needs pinning down first.
- `post-merge` → `handoff` → `blurb` — the close-out ritual once this session's PR lands.
