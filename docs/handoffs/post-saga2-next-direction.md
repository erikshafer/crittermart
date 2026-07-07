# CritterMart — Handoff: Post-Saga-#2 — pick the next direction + carry-forwards

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-07.
> Closes out the Saga #2 mission (parent: [`saga2-handoff.md`](saga2-handoff.md), sessions
> [1](saga2-session1-adr009-revisit.md)/[2](saga2-session2-email-change-saga.md)) and opens the next one.
> This doc does **not** repeat Saga #2's content (shape, API gotchas, retro) — read the parent chain if that
> history is needed; it isn't required to execute this session's mission.

## Mission for the next session

CritterMart's **round-one modeled implementation set is complete, and both post-round-one convention-saga
increments are shipped, archived, and doc-corrected** (below). There is no committed next increment. This
session's job is **design-phase, not implementation**: help Erik pick the next post-round-one direction from
`docs/vision.md` § Long road, then run whatever the first step of that direction is (likely a workshop pass
or an ADR, per the two-phase pipeline in `CLAUDE.md`).

**Do not default to code.** If the session opens with an ambiguous prompt ("what's next"), re-derive this
handoff's "Open fork" section below and present it before writing anything.

## What's true right now (2026-07-07, verified this session — don't re-derive)

- `main` @ `119158b`, clean, pushed, CI green (Format/Build/Unit/Integration all ✓).
- **Saga #2 (Identity `EmailChange`, EF-Core-backed) is fully shipped**: ADR-009 revisit + ADR 022 (#119) →
  Workshop 002 v1.1 → OpenSpec `slices-5-5-5-7-email-change-saga` → narrative 009 → implementation (#121,
  161/161 tests green, live-verified). **Archived** into `customer-registry` spec (2026-07-07). README and
  `docs/vision.md` refreshed same day to stop describing it as forthcoming.
- **CritterWatch beta.1 does not visually surface saga instances for either saga** — empirically confirmed
  (`docs/research/critterwatch-saga-visibility-beta1.md`), and the stale "Saga Explorer" claims in
  `demo-runbook.md` §5c and `docs/workshops/002-identity-event-model.md` §8 item 10 (Workshop 002 → v1.2) are
  now corrected to match.
- **Dependabot backlog is clear** — all 9 open PRs (frontend groups, .NET test-stack, Swashbuckle,
  OpenTelemetry instrumentation) merged and branches deleted this session.
- **Two convention sagas now exist**: `Replenishment` (Inventory, Marten-backed) and `EmailChange` (Identity,
  EF-Core-backed) — together they're the shipped proof that `Wolverine.Saga` is additive to PMvH (ADR 007)
  and its storage backend is swappable (ADR 022).

## Open fork — the actual decision this session exists to make

`docs/vision.md` § Long road names the candidates, unranked, none committed:

1. **Polecat-backed real authentication** for Identity — the registry service already shipped; this would
   give it actual authN/authZ, replacing the hardcoded `X-Customer-Id` header (ADR 009's current boundary).
2. **A returns slice** — new domain territory, no existing precedent in the modeled set.
3. **A promotions slice using Dynamic Consistency Boundaries** — would be CritterMart's first DCB pattern;
   highest novelty, likely highest teaching payoff if the talk still has room for a new pattern.
4. **Broader async projection use** for replay demonstrations — extends the existing
   `CartAbandonmentReport` teaser rather than opening new domain territory.
5. **A separate BFF** promoting the Wolverine.Http surface — reverses ADR 015/016's no-BFF stance; would need
   its own ADR revisit before design work starts.
6. **Multi-tenant scaffolding** — infrastructural, not domain-shaped; lowest narrative fit for a talk.
7. **Richer frontend interactions** — polish, not a new pattern; lowest teaching payoff of the seven.

Present these to Erik via `AskUserQuestion` (2–4 options with previews, per
[[feedback-options-with-previews]]) rather than picking one — this is a genuine fork, not a lean to act on
([[feedback-collaborate-on-decisions]]). Worth naming a recommendation (candidates 1 and 3 have the strongest
teaching-payoff case: Polecat closes a stub the vision doc has flagged since round one, DCB would be a
genuinely new pattern for the talk), but let Erik decide.

## Smaller carry-forward items (not blocking, pick up opportunistically)

- **CritterWatch trial expires 2026-07-10.** Erik is talking to Babu/Jeremy about renewal outside this
  repo's session cadence — check whether that's resolved before attempting any live CritterWatch-console
  verification work.
- **Marten-sibling verification gap** (named in `critterwatch-saga-visibility-beta1.md` "Promotion path"
  table): the claim that `Replenishment`'s `ReplenishTimeout` is also absent from
  `inventory.wolverine_incoming_envelopes` (same non-durable-local-queue cause as `EmailChangeTimeout`) is
  *inferred*, not verified. A quick live-stack + Postgres check would close it. Needs the CritterWatch trial
  window or just direct Postgres access (doesn't actually need the console).
- **`UseDurableLocalQueues()` decision for saga timeouts** — both sagas currently lose their scheduled
  timeout if the owning service restarts mid-window (harmless for a single-node teaching demo, flagged as a
  "robustness observation, not a shipped bug" in the same research doc). This crosses CLAUDE.md's ADR
  threshold once someone actually decides it — either fix it (small change, doubles as a CritterWatch
  Scheduled-view teaching payoff once instance discovery ships) or formally log it to
  `docs/skills/DEBT.md`. Currently just sitting as an observation in a research doc — not yet an ADR-tracked
  decision either way.
- **Five AppHost demo knobs** (`Payment__DeclineOverAmount`, `Payment__AuthDelay`, `Orders__PaymentTimeout`,
  `Inventory__ReplenishTimeout`, `Identity__EmailChangeTimeout`) — **post-talk only**, do not delete yet.
- **Do NOT bump Wolverine past 6.16.0** until a newer CritterWatch release targets it
  ([[critterwatch-wolverine-version-coupling]]).
- **Do not bump transitive dependencies JasperFx packages bring in** (e.g. the still-suppressed MessagePack
  CVE `GHSA-hv8m-jj95-wg3x`) — note + suppress + wait for upstream, per
  [[feedback-no-transitive-dep-bumps]]. Only manage the high-level packages CritterMart references directly.

## Orientation files (read first, in order)

1. This handoff (you're reading it).
2. `docs/vision.md` § Long road — the candidate list the open fork draws from.
3. `docs/context-map/README.md` § Long road — cross-BC relationship candidates for whichever direction wins
   (e.g. Polecat would need a context-map entry for how it relates to Identity).
4. Whichever ADR is relevant to the chosen direction (e.g. `docs/decisions/009-polecat-deferred-for-round-one.md`
   if Polecat is chosen; `docs/decisions/015-vite-react-frontend-stack.md` / `016` if BFF is chosen).
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
- `domain-modeling` — for context-map / ubiquitous-language work if the direction introduces new vocabulary
  (e.g. Polecat's auth vocabulary vs. Identity's registry vocabulary).
- `opsx:propose` / `openspec-propose` — once the direction reaches OpenSpec-proposal stage.
- `post-merge` → `handoff` → `blurb` — the close-out ritual once this session's PR(s) land.
