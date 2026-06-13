---
retrospective: 011
kind: docs
prompt: docs/prompts/docs/011-round-one-close-reconciliation.md
deliverable: docs/decisions/017-critterwatch-integrated.md, docs/decisions/013-critterwatch-deferred-to-messaging-slices.md, docs/decisions/README.md, docs/workshops/001-crittermart-event-model.md, docs/skills/wolverine-cross-bc-cascading/SKILL.md, docs/skills/README.md
date: 2026-06-13
mode: solo synthesis; collaborative scope (A + C + D chosen by the maintainer via AskUserQuestion; B declined)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 011: Round-One Close + Doc Reconciliation

## Outcome summary

The design-return interleave before frontend mode, run as a reconciliation: three canonical docs had
drifted *behind* shipped reality, and this session brought them current and durably recorded two
decisions the code already embodied. The maintainer chose the scope at session start via
`AskUserQuestion` — **A** (doc reconciliation), **C** (capture the cascading gotcha), **D** (Aspire
smoke-check) — and **declined B** (the `global.json` SDK pin). Shipped:

- **ADR 017 — CritterWatch Integrated** (new). The successor ADR that ADR 013 explicitly predicted.
  Records the as-shipped integration (console project + per-service `AddCritterWatchMonitoring`,
  dedicated `critterwatch` Postgres DB, telemetry over the existing RabbitMQ, single-node
  `enableClusterPartitioning: false`, the Aspire `critterwatch`/`critterwatch-console` resource-name
  collision) and **resolves ADR 013's Open Question**: Trial tier, key in user-secrets (never
  committed), and — the key feed finding — **CritterWatch 0.9.1 publishes to nuget.org**, so the
  private `packages.jasperfx.net` feed was never re-added and CI stays green. Captured two live
  caveats: the **Production-environment gotcha** (Development silently masks the real license tier) and
  a **suppressed transitive MessagePack CVE** (`GHSA-hv8m-jj95-wg3x`, a known time-boxed trade-off).
- **ADR 013 forward-pointer** (edit, append-only). A one-line "Realized by ADR 017" note under the
  status; status stays `Accepted` (the deferral was honored, not reversed); body untouched.
- **README index** (edit). ADR 017 row added.
- **Workshop 001 frontmatter sync + v1.7 row** (edit). `status` Draft → "Round-one complete; round-two
  frontend amendments pending (ADR 016)"; `date` 2026-05-26 → 2026-06-13 (the frontmatter date had
  drifted behind its own v1.6 history row). A v1.7 Document History row marks the round-one close (all
  18 modeled slices shipped) and names the round-two amendments as pending, not done.
- **`wolverine-cross-bc-cascading` skill** (new) + skills README entry. Lifts the `object?`-breaks-
  conventional-routing gotcha out of the archived slice-2-4 `design.md` into a local skill, framed as
  CritterMart's typed-cascading-return convention with in-repo precedents (the Orders→Inventory
  payment-gate hops).
- **Aspire boot smoke-check** (verification, no artifact). See below.

Build clean before and after (0 warnings, 0 errors). No code touched; this is a docs-surface PR.

## Aspire boot smoke-check — result

`dotnet run --project src/CritterMart.AppHost` booted clean and ran 3+ minutes:

- **Orchestrator**: Aspire 13.4.3 started, dashboard listening on `:17090`, "Distributed application
  started," no exceptions / `exited with code` / license-failure lines in the AppHost stdout — and
  Aspire *does* surface resource-start failures to that stream, so the silence is a positive signal,
  not an absence of monitoring.
- **Infra**: the expected **single** Postgres (`postgres:18.3`) + **single** RabbitMQ (`rabbitmq:4.3`)
  containers came up healthy with mapped ports. Service processes launched (extra HTTPS listeners
  beyond the dashboard).
- **Teardown**: killing the AppHost let DCP reap every container — `docker ps` clean afterward, no
  orphans.
- **Bonus**: the boot also reaped a **pre-existing orphan pair** (`postgres:18-alpine`,
  `rabbitmq:3-management`) left over from an earlier run — worth a glance if Docker ever shows stale
  CritterMart infra between sessions.

**Not independently confirmed**: a live per-service **CORS preflight**. The services speak HTTP/2
behind DCP's dynamic proxy, so a guessed-port `OPTIONS` couldn't resolve to a service's CORS middleware
without the dashboard's endpoint map. The CORS code is present (PR #46) and compiles (0/0). The live
per-service CORS check belongs to the frontend-wiring step, when the real origin is injected via
`Cors:AllowedOrigins` — that is the natural place to assert the preflight against a known endpoint.

## What worked

- **The AskUserQuestion scope fork.** Presenting A/B/C/D as a multi-select kept the bundle decision with
  the maintainer, who took A + C + D and dropped B — a split I would not have predicted from the lean.
  Acting on the stated lean (A + B) would have shipped an unwanted `global.json` and skipped C and D.
- **Following ADR 013's own prediction.** 013 named its successor in advance ("a successor ADR will
  record the actual integration"); minting ADR 017 rather than rewriting 013 in place honored the
  append-only discipline *and* the doc's own intent. The reconciliation wrote itself once that framing
  was clear — the question wasn't "rewrite or supersede?" but "fulfill the predicted successor."
- **Grounding ADR 017 in the live wiring, not memory.** Reading `CritterWatch/Program.cs`, the AppHost,
  the per-service calls, `Directory.Packages.props`, and `nuget.config` surfaced the load-bearing facts
  the memory note didn't carry — the nuget.org sourcing (which resolves 013's feed worry) and the
  suppressed MessagePack CVE. The ADR records what the code does, not what was remembered.
- **The smoke-check earned its keep beyond a pass/fail.** It confirmed the orchestrator boots clean and,
  incidentally, that Aspire reaps orphaned infra — and it sharpened the honest scope of what headless
  verification can and cannot assert (orchestrator + infra: yes; per-service CORS: no, needs the
  dashboard).

## What was harder than expected

- **Verifying *service* health from outside the dashboard.** Aspire routes child-process logs to the
  dashboard, not the AppHost console, so the AppHost stdout shows the orchestrator and infra but not
  per-service "started" lines. Confidence in the 3 services + CritterWatch rests on (a) the clean
  AppHost stdout where *failures* would appear, (b) launched processes, and (c) the build — not on a
  direct green-health read. A definitive per-service assertion needs the dashboard resource API (token-
  gated) or a known endpoint. Named honestly rather than overstated.
- **Headless teardown on Windows.** The PID recorded via bash `$!` was the transient subshell, not the
  persistent AppHost; the real teardown target was the DCP-monitored AppHost PID (the `:22090` dashboard
  listener / `--monitor` target). Tree-killing that PID let DCP reap the containers cleanly. Lesson
  below.

## Methodology refinements that emerged

- **A predicted-successor ADR is the cleanest reconciliation shape.** When an earlier ADR explicitly
  names a successor (a deferral that will be "realized when X"), reconciling later is: mint the named
  successor, add a one-line forward-pointer to the original, keep the original's status and body.
  Append-only, honest, and the diff is tiny. This is the second deferral-shaped ADR pair in the repo
  (mirrors the ADR 009 Polecat deferral); the pattern is now worth naming.
- **Reconciliation is a reverse spec-delta and the cadence rule covers it.** The design-return cadence
  is usually framed as "loop retro findings back into design." This session showed the same edge runs
  the other way: when docs drift behind shipped code, a `docs:` reconciliation is a legitimate
  design-return PR. The drift here (stale frontmatter, an un-realized deferral, a buried gotcha) was
  invisible until something forced a read of the canonical layer — the cadence interleave is what forces
  that read.
- **For an Aspire smoke-check, kill the DCP-monitored AppHost PID, not the launcher.** The graceful
  teardown path is the AppHost process Aspire's DCP monitors (the dashboard-hosting PID); killing it
  triggers container reaping. A bash-recorded launcher `$!` is unreliable on Windows. Find the real
  AppHost PID (the `:22090`/dashboard listener or the DCP `--monitor` argument) and tree-kill that.
- **Headless verification has an honest ceiling — state it.** A smoke-check from outside the dashboard
  confirms orchestrator + infra + "no failures surfaced," and that is genuinely useful. It does not
  confirm per-service runtime behavior (e.g., CORS). Record the ceiling in the retro rather than
  implying a green health read that wasn't taken.

## Outstanding items / next-session inputs

- **Frontend mode is the next session (ADR 016, step one).** Unchanged from PR #46's retro: a workshop
  `Wireframe`-column amendment + net-new view slices, and a customer-journey narrative. **Model Gap #1
  (open-cart-by-customer, provisional slice 3.5) first** — the unique computed index on
  `CartView.CustomerId` already exists; it is the one blocking gap from the endpoint audit.
- **Live CORS verification is deferred to frontend wiring.** When `AddViteApp` lands and the AppHost
  injects the real origin via `Cors:AllowedOrigins`, assert the preflight against a known service
  endpoint then. Add the `npm` ecosystem block to `.github/dependabot.yml` at the same time.
- **CritterWatch trial expires 2026-07-10.** ADR 017 records this as a live deadline. Past it the
  console drops to the read-only Free tier unless the tier decision is revisited — which would reopen
  ADR 013's CI/feed design (a paid tier from the private feed needs an opt-in project excluded from the
  default restore, or authenticated NuGet on CI).
- **Suppressed MessagePack CVE** (`GHSA-hv8m-jj95-wg3x`) — revisit when an upstream CritterWatch release
  moves to MessagePack ≥ 3.0.214; remove the `NuGetAuditSuppress` then.
- **`global.json` SDK pin (Option B) remains available** — declined this session, not closed. Still a
  cheap hedge against the "passes locally / fails CI" SDK-drift class.
- **Memory `next-pickup` should be refreshed** — it predates this reconciliation.

## Spec-delta — landed?

**Named delta landed.** The prompt named: a v1.7 workshop Document History row recording the round-one
close + frontmatter sync; ADR 017 minted as the integration record ADR 013 predicted, with a forward-
pointer on 013 and a README index row; and a new local skill recording the cross-BC typed-cascading
convention. All landed as named. No workshop *slice* and no OpenSpec *capability* changed — this
reconciled existing canonical artifacts with shipped code; it did not alter the modeled scenario set.
Workshop 001 records the amendment in its `## Document History` (v1.7), closing the
prompt → execute → retro → spec-record loop.
