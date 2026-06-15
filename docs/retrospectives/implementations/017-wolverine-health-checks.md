---
retrospective: 017
kind: implementations
prompt: docs/prompts/implementations/017-wolverine-health-checks.md
deliverable: docs/decisions/019-wolverine-health-checks-exposed.md, src/CritterMart.Catalog/Program.cs, src/CritterMart.Inventory/Program.cs, src/CritterMart.Orders/Program.cs
date: 2026-06-15
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 017: Wolverine Health Checks Exposed

## Outcome summary

Shipped the merge-worthy finding salvaged from the throwaway research-003 CritterWatch spike into `main` via the normal pipeline. Each of the three services (Catalog, Inventory, Orders) now references `WolverineFx.HealthChecks` (6.8.0, the Critter Stack 2026 line) and, right after `UseWolverine`, registers the Wolverine **bus** check (`wolverine` — runtime startup/cancellation) and **listener** check (`wolverine-listeners` — accepting / too-busy / latched / stopped) via `AddHealthChecks().AddWolverine().AddWolverineListeners()`. Each service also now calls `app.MapDefaultEndpoints()` so `/health` (all checks) and `/alive` (liveness-tagged) actually map — the method was defined in `ServiceDefaults` but had never been called. `ServiceDefaults` itself is **unchanged** (the registration is per-service by design — locked decision 1). The decision is recorded in **ADR 019**, cross-referencing ADR 017 (the integration that surfaced the gap). Run in a git worktree off `main` so the live spike stack in the primary checkout was never disturbed.

## Tests

Full solution green, **97 total, 0 failures** — Catalog 9, Inventory 16, Orders 69, CrossBc 3 — identical to the retro-016 baseline. **No test changes**: the suite passing across all three Alba hosts already proves the new startup wiring (`AddWolverine` / `AddWolverineListeners` / `MapDefaultEndpoints`) runs without throwing in every service. `dotnet build` clean (0 errors; the 22 `NU1507` warnings are the pre-existing two-NuGet-sources condition, not introduced here).

## What worked

- **Worktree isolation.** The spike's seeded async stack (AppHost + 4 services + Postgres/RabbitMQ containers) was running live in `C:\Code\crittermart` and the owner was mid-exploration of CritterWatch's async views. Working in a `git worktree` off `main` (`C:\Code\crittermart-hc`) meant the build/test/edit cycle never touched the spike's checkout or its `bin/obj`, and Testcontainers spun up its own ephemeral infra — zero contention with the running stack.
- **Two forks up front via AskUserQuestion + previews.** Registration home (per-service vs ServiceDefaults) and listener-check scope (bus+listeners vs bus-only) were resolved with the user before any code, so the session ran straight through. The per-service choice kept the diff honest — the generic `ServiceDefaults` project stays Wolverine-free.
- **ctx7 verify-before-wiring.** The Wolverine docs corrected the package id (`WolverineFx.HealthChecks`, not `Wolverine.HealthChecks`), gave the exact `AddWolverine()`/`AddWolverineListeners()` API and check names, and showed the after-`UseWolverine` registration ordering — so the wiring compiled first try and the placement decision rested on docs, not guesswork.
- **The inline-vs-async distinction held.** The spike's lesson #4 (its async config broke 29 tests) explicitly did *not* recur: on `main`'s inline config, the only change is health-check *registration*, which the tests never exercise. Suite stayed green with zero test edits — a clean demonstration that what a change *touches* decides its blast radius.

## What was harder than expected

- **Distinguishing the two halves of the "gap."** "Health checks: Not registered" reads like one problem but is two: the DI-level *registration* (what CritterWatch introspects over its telemetry channel) and the HTTP *exposure* (`MapDefaultEndpoints`, for Aspire's own probing). Only the registration clears the console flag; the `MapDefaultEndpoints` call is complementary hygiene. Getting the ADR and comments to state that precisely took more care than the code did.
- **Verification ceiling — named, not hidden.** The suite proves the wiring runs at host startup, but the *live* `/health` HTTP response and the *actual* clearing of the CritterWatch flag are only observable by booting the full Aspire stack with the console — which would collide with the running spike stack (the PR #55 stale-queue hazard) and so was deferred. This is an honest residual, not a closed loop (see Outstanding).

## Methodology refinements

- **Salvaging a scratch-spike finding through the pipeline works cleanly when the finding is code-verified, not memory-recalled.** The handoff flagged this candidate slice and named the exact files; re-verifying against `main` with `grep` before trusting it (per the "verify before recommending" discipline) meant the ADR's Context section rested on the current tree, not the spike's notes. Pattern: a spike's "merge-worthy finding" earns a real slice only after a fresh code check on the target branch.
- **A worktree is the right default when a long-lived stack is running in the primary checkout.** Rather than stash/branch-switch (which would change the source under a running exploration), the worktree gave a clean `main` surface with no coordination cost. Worth reaching for whenever "work on another branch without disturbing this one" is the actual constraint.

## Outstanding / next-session inputs

- **Live-stack verification deferred** — boot the full Aspire topology (worktree's AppHost, or merge then run normally) and confirm: (a) `GET /health` returns `Healthy` per service in Development, and (b) the CritterWatch console's per-service overview no longer flags "Health checks: Not registered." Not done here to avoid colliding with the running spike stack; a natural check for the owner's next CritterWatch session, or post-merge.
- **The other research-003 salvage candidates remain open** — the 5 spike projections as a proper *inline* slice, and a `CRITTERMART_ASYNC` A/B boot toggle (scratch). Untouched by this session (out of scope); they are independent follow-ups.
- **The spike stack is still running** in `C:\Code\crittermart` (owner mid-exploration) and the worktree `C:\Code\crittermart-hc` persists until this PR is merged/closed — `git worktree remove` it afterward.
- **`tidy: encode-ceremony-rule`** remains overdue (carried since retro 013).

## Spec-delta — landed?

**Named delta landed.** The prompt named *no* OpenSpec/workshop/narrative change (cross-cutting infra, no aggregate) and none was made — the honest "no behavioral spec delta" is forward-confirmed, not confabulated. The canonical-spec movement was the **new ADR 019** (Wolverine runtime health exposed), which landed with its `docs/decisions/README.md` index row and a cross-reference to **ADR 017** (the integration it realizes). Four-step closure complete: prompt named → session executed → retro confirms → decision log records.
