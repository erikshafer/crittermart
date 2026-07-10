# CritterMart — Handoff: Post-Auth — pick the next direction + carry-forwards

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-10.
> Closes out the real-authentication mission (parent: [`identity-real-auth-direction.md`](identity-real-auth-direction.md) →
> [`identity-auth-implementation.md`](identity-auth-implementation.md)) and opens the next one. Direct successor
> to [`post-saga2-next-direction.md`](post-saga2-next-direction.md), whose open fork this handoff re-derives
> now that candidate #1 from that fork (real authentication) has shipped.
> This doc does **not** repeat the auth mission's content (ADR 023's architecture, the slice-by-slice build,
> the retro) — read the parent chain if that history is needed; it isn't required to execute this session's
> mission.

## Mission for the next session

CritterMart's **round-one modeled implementation set is complete, and three post-round-one increments are
shipped, archived, and doc-corrected** (below): two convention sagas plus real authentication for Identity.
There is no committed next increment. This session's job is **design-phase, not implementation**: help Erik
pick the next post-round-one direction from `docs/vision.md` § Long road, then run whatever the first step of
that direction is (likely a workshop pass or an ADR, per the two-phase pipeline in `CLAUDE.md`).

**Do not default to code.** If the session opens with an ambiguous prompt ("what's next"), re-derive this
handoff's "Open fork" section below and present it before writing anything.

## What's true right now (2026-07-10, verified this session — don't re-derive)

- `main` @ `14297d6`, clean, pushed. That commit is `tidy: housekeeping — update README + vision for
  post-auth state`, one commit after `2ad3971` (`tidy: openspec — archive
  slices-5-8-5-11-identity-authentication`), one commit after `beab235` (squash-merge of **PR #130**, the
  auth implementation).
- **Real authentication for Identity is fully shipped**: ADR 023 (#129) → Workshop 002 §§ 5.8–5.11 →
  OpenSpec `slices-5-8-5-11-identity-authentication` → narrative 010 → implementation (#130, 172 backend +
  112 frontend tests green, HTTP-level live-verified). **Archived** into the `customer-registry` spec
  (2026-07-10, 4 ADDED requirements: `POST /register`, `POST /login`, Orders JWT-bearer resource-server
  config, `POST /logout`). README and `docs/vision.md` refreshed same day to stop describing it as "the next
  increment, not yet built."
- **The cutover is layered, not hard.** `sub` (from the JWT) is the trust boundary, but Orders still accepts
  an `X-Customer-Id` header as a dev-only fallback when no Bearer token is presented — this keeps the seeder,
  pre-existing Orders/cross-BC tests, and `demo-traffic.ps1` working unchanged. See "Open fork" below;
  removing this fallback is tracked as debt, not chosen as the next direction by default.
- **Three post-round-one increments now exist** on the Long road: `Replenishment` (Inventory, Marten-backed
  saga), `EmailChange` (Identity, EF-Core-backed saga), and real authentication (Identity, ASP.NET Core
  Identity + self-validated JWT). The first candidate on the prior fork is closed; six remain, unranked
  (below).
- **CritterWatch trial expiry (2026-07-10) is unresolved** — Erik confirmed this session that the
  Babu/Jeremy renewal conversation has not concluded. Treat any live-CritterWatch-console verification work
  as blocked until that resolves; check before attempting it.
- **The SPA-browser-drive gap from the auth session is intentionally parked**, not scheduled — Erik's call
  this session. It stays recorded in `docs/retrospectives/implementations/037-slices-5-8-5-11-identity-authentication.md`'s
  Outstanding section (register→login→add-to-cart→checkout→logout, driven live through the SPA, unverified
  because Claude-in-Chrome wasn't connected during that session — it is connected now, so a future session
  *could* close this opportunistically, but it is not this handoff's mission).

## Open fork — the actual decision this session exists to make

`docs/vision.md` § Long road names the remaining candidates, unranked, none committed:

1. **A returns slice** — new domain territory, no existing precedent in the modeled set.
2. **A promotions slice using Dynamic Consistency Boundaries** — would be CritterMart's first DCB pattern;
   highest novelty, likely highest teaching payoff if the talk still has room for a new pattern.
3. **Broader async projection use** for replay demonstrations — extends the existing
   `CartAbandonmentReport` teaser rather than opening new domain territory.
4. **A separate BFF** promoting the Wolverine.Http surface — reverses ADR 015/016's no-BFF stance; would need
   its own ADR revisit before design work starts.
5. **Multi-tenant scaffolding** — infrastructural, not domain-shaped; lowest narrative fit for a talk.
6. **Richer frontend interactions** — polish, not a new pattern; lowest teaching payoff of the six.

Present these to Erik via `AskUserQuestion` (2–4 options with previews, per
[[feedback-options-with-previews]]) rather than picking one — this is a genuine fork, not a lean to act on
([[feedback-collaborate-on-decisions]]). Worth naming a recommendation (candidate 2, DCB, has the strongest
teaching-payoff case now that both prior top candidates — a saga pair and real auth — are shipped; it would
be a genuinely new pattern for the talk. Candidate 1, returns, is the strongest "new domain territory but low
novelty-risk" alternative), but let Erik decide.

**Not on this list, and deliberately not folded in as a silent default:** the `X-Customer-Id` hard cutover
(removing the dev-only fallback, migrating the seeder/tests/demo-traffic.ps1 to mint real JWTs — see retro
037's Outstanding section) is implementation debt on the *already-shipped* auth increment, not a new
Long-road direction. It's a legitimate candidate for "smaller carry-forward, pick up opportunistically"
below, or its own small session — but don't let it substitute for actually resolving the Long-road fork if
Erik wants a new direction chosen.

## Smaller carry-forward items (not blocking, pick up opportunistically)

- **The `X-Customer-Id` hard cutover** (new this handoff — see retro 037's Outstanding section). Removes the
  dev-only fallback header entirely; migrates the seeder, pre-existing Orders/cross-BC tests, and
  `demo-traffic.ps1` to mint/carry real JWTs instead.
- **CritterWatch trial renewal** — unresolved as of 2026-07-10 (see above). Not a repo-code task; just a
  gating check before live-console work.
- **Marten-sibling verification gap** (named in `critterwatch-saga-visibility-beta1.md` "Promotion path"
  table): the claim that `Replenishment`'s `ReplenishTimeout` is also absent from
  `inventory.wolverine_incoming_envelopes` (same non-durable-local-queue cause as `EmailChangeTimeout`) is
  *inferred*, not verified. Still open — carried forward unchanged from the prior handoff.
- **`UseDurableLocalQueues()` decision for saga timeouts** — both sagas currently lose their scheduled
  timeout if the owning service restarts mid-window (harmless for a single-node teaching demo). Crosses
  CLAUDE.md's ADR threshold once someone actually decides it — either fix it or formally log it to
  `docs/skills/DEBT.md`. Still just an observation in a research doc, unchanged from the prior handoff.
- **Five AppHost demo knobs** (`Payment__DeclineOverAmount`, `Payment__AuthDelay`, `Orders__PaymentTimeout`,
  `Inventory__ReplenishTimeout`, `Identity__EmailChangeTimeout`) — **post-talk only**, do not delete yet.
- **Four stray local branches** noted across two prior handoffs, still untouched: `research/cw-telemetry-spike`,
  `research/wolverine-saga-feasibility`, `spike/critterwatch-seed-data`,
  `tidy/demo-traffic-observability-review`. Worth a `git branch` triage pass (delete, open a PR, or confirm
  still-live) next time housekeeping comes up — they've now persisted across two full handoff cycles without
  being touched.
- **Do NOT bump Wolverine past 6.16.0** until a newer CritterWatch release targets it
  ([[critterwatch-wolverine-version-coupling]]).
- **Do not bump transitive dependencies JasperFx packages bring in** (e.g. the still-suppressed MessagePack
  CVE `GHSA-hv8m-jj95-wg3x`) — note + suppress + wait for upstream, per
  [[feedback-no-transitive-dep-bumps]]. Only manage the high-level packages CritterMart references directly.
- **Refresh tokens + server-side revocation remain deferred** (ADR 023 open Q15) — logout is client-side
  discard; a token stays valid until its (short) lifetime expires. Not scheduled; noted for completeness.
- **Authorization (roles/policies) remains deferred** (ADR 023 open Q16) — the token authenticates, never
  authorizes; a second actor would open this. Not scheduled; noted for completeness.

## Orientation files (read first, in order)

1. This handoff (you're reading it).
2. `docs/vision.md` § Long road — the candidate list the open fork draws from.
3. `docs/context-map/README.md` § Long road — cross-BC relationship candidates for whichever direction wins
   (e.g. a promotions slice would need a context-map entry for how DCB interacts with existing aggregates).
4. Whichever ADR is relevant to the chosen direction (none exist yet for the six remaining candidates — the
   chosen direction likely *starts* with an ADR or a workshop pass, not references one).
5. `CLAUDE.md` — the pipeline itself (context mapping → domain storytelling [likely still skippable] → event
   modeling workshop → per-slice loop), since whichever direction is chosen starts back at the pre-code design
   phase, not mid-pipeline.

## Working style (Erik's standing preferences — carried from memory)

Present options + a recommendation at genuine forks, ideally via `AskUserQuestion` with previews
([[feedback-collaborate-on-decisions]], [[feedback-options-with-previews]]); prefer tool-backed artifacts over
freeform (`openspec` CLI — [[feedback-prefer-tool-backed-over-freeform]]); ask where something should live
before writing it if Erik says "persist"/"make durable" ([[feedback-ask-where-to-persist]]); live-verify
against the real stack after non-trivial changes and drive demo flows yourself rather than handing back a URL
([[feedback-live-verify-after-changes]], [[feedback-drive-demo-flows]]); flag deferred/non-terminal state
explicitly rather than shipping a half-finished path silently ([[feedback-flag-deferred-state-on-completion]]).

## Definition of done

- [ ] Open fork presented to Erik (AskUserQuestion, previews), a direction chosen
- [ ] If the direction needs a context-map update or new ADR, that lands first (per CLAUDE.md's pre-code
      design phase — don't skip to a workshop pass without it if the direction requires it)
- [ ] First design artifact for the chosen direction authored (context map amendment and/or workshop pass,
      depending on what the direction needs)
- [ ] This handoff's smaller carry-forward items triaged — picked up, explicitly deferred with a reason, or
      logged to `docs/skills/DEBT.md`
- [ ] `/post-merge` → `/handoff` → `/blurb` close-out ritual run once this session's PR(s) land

## Suggested skills

- `event-modeling` (in-repo skill, `docs/skills/event-modeling/SKILL.md`) — if the chosen direction starts
  with a workshop pass.
- `domain-modeling` — for context-map / ubiquitous-language work if the direction introduces new vocabulary.
- `marten-advanced-dynamic-consistency-boundary` — if the promotions/DCB candidate is chosen; covers
  `[BoundaryModel]`, `EventTagQuery`, and cross-stream consistency without distributed transactions.
- `opsx:propose` / `openspec-propose` — once the direction reaches OpenSpec-proposal stage.
- `post-merge` → `handoff` → `blurb` — the close-out ritual once this session's PR(s) land.
