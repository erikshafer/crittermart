# Prompt: Implementations 017 — Wolverine Health Checks Exposed (close the CritterWatch "Health checks: Not registered" gap)

**Kind**: cross-cutting infra slice — **not** a modeled behavioral slice. No OpenSpec change, no new SHALL, no narrative/workshop amendment (there is no aggregate and no actor-journey change). The canonical-spec record is the new **ADR 019**. Salvages one merge-worthy finding from the throwaway `research/critterwatch-seed-data` spike (research 003) into `main` via the normal pipeline.
**Source**: the gap was surfaced — and verified against `main`'s code (`grep`, not memory) — during the research-003 CritterWatch seed-data spike (scratch, never merged). The spike fed the console live data; its console overview flagged each service "Health checks: Not registered." This session is the proper-pipeline landing on `main`.
**Files touched**: this prompt; `Directory.Packages.props` (+`WolverineFx.HealthChecks` 6.8.0); the three service `.csproj` (+`WolverineFx.HealthChecks` reference); the three service `Program.cs` (+`using Wolverine.HealthChecks`, +`AddHealthChecks().AddWolverine().AddWolverineListeners()` after `UseWolverine`, +`app.MapDefaultEndpoints()`); `docs/decisions/019-wolverine-health-checks-exposed.md` (new ADR) + `docs/decisions/README.md` (index row); `docs/{prompts,retrospectives}/README.md` (counts); `docs/retrospectives/implementations/017-wolverine-health-checks.md` (forthcoming).
**Mode**: solo; two genuine forks (registration home, listener check) presented collaboratively (AskUserQuestion + previews) and resolved with the user **before any code** — they appear below as locked decisions. Run in a **git worktree off `main`** (`C:\Code\crittermart-hc`, branch `feat/wolverine-health-checks`) because the research-003 spike stack is running live in the primary checkout and must not be disturbed.
**Commit subject**: `feat: expose Wolverine runtime + listener health checks across services`

## Framing

The research-003 spike (scratch, not for merge) used CritterMart as a test bed for feeding CritterWatch live data. One finding was merge-worthy and *not* spike-caused: the CritterWatch per-service overview reports **"Health checks: Not registered."** Verified against `main`: `ServiceDefaults.MapDefaultEndpoints` (maps `/health` + `/alive`) is defined but never called; `AddDefaultHealthChecks` registers only a bare `AddCheck("self", …)` liveness probe; and `WolverineFx.HealthChecks` is not installed — so nothing reports Wolverine runtime/listener state to ASP.NET's `HealthCheckService`, which is what the console reads. (Tracing is *not* part of the gap: ADR 005 already configures OTel; CritterWatch's separate per-instance trace-provider feature is deliberately unconfigured and out of scope.)

This is small, additive, and pipeline-worthy. It touches all three services, so it is ADR-shaped (criterion (a): reversing crosses multiple BCs; criterion (c): the per-service-vs-shared and listener-check choices would otherwise be re-derived).

## Goal

Each service (Catalog, Inventory, Orders) registers the Wolverine **bus** + **listener** health checks and calls `MapDefaultEndpoints()` so `/health` + `/alive` map. CritterWatch's "Health checks: Not registered" flag clears (the console reads the *registered* checks over its telemetry channel). The test suite stays green with **no test changes** — `main`'s inline projection config means there is no read-after-write staleness, and the `/health` endpoint is dev-only and unhit by Alba scenarios; the suite passing already proves the new startup wiring runs in all three hosts. ADR 019 records the decision.

## Spec delta

**No OpenSpec / workshop / narrative change** — cross-cutting infra, no aggregate, no actor-journey movement. The canonical-spec movement is the **new ADR 019** (Wolverine runtime health exposed) plus a cross-reference relationship to **ADR 017** (the CritterWatch integration that surfaced the gap; ADR 019 realizes it). Four-step closure: this prompt names it → the session executes → the retro confirms → the decision log records ADR 019 + index row.

## Locked decisions (forks resolved with the user at session start, 2026-06-15)

1. **Per-service registration, not folded into `ServiceDefaults`.** The Wolverine checks register in each `Program.cs` right after `UseWolverine` (matching the `WolverineFx.HealthChecks` docs ordering), keeping the generic Aspire-shaped `ServiceDefaults` project free of a `WolverineFx` dependency — Wolverine health belongs with the services that own a runtime. `ServiceDefaults` is unchanged; each service merely *calls* the already-defined `MapDefaultEndpoints()`.
2. **Bus + listener checks** (`.AddWolverine().AddWolverineListeners()`), not bus-only. The listener check makes the health panel react when CritterWatch's chaos monkey latches a listener — a live teaching tie-in — and is green in normal runs. Bus-only would clear the flag but forgo the listener signal and the chaos tie-in.

## Orientation

1. **The research-003 handoff** + **`docs/research/critterwatch-seed-data.md`** — the spike that surfaced the gap; the "NEW — pre-existing gap surfaced on `main`" section names this exact candidate slice.
2. **CLAUDE.md** — the ADR capture threshold, one-prompt-one-PR, no-opportunistic-edits, spec-delta closure.
3. **ADR 017** (`017-critterwatch-integrated.md`) — the integration that surfaced the flag; the Production-environment + telemetry-channel mechanics this slice clears.
4. **`src/CritterMart.ServiceDefaults/Extensions.cs`** — `MapDefaultEndpoints` (defined, dev-only, uncalled) and `AddDefaultHealthChecks` (the bare `self` check).
5. **The three `Program.cs`** — the `UseWolverine` / `AddWolverineHttp` / `MapWolverineEndpoints` shape that names the insertion points.
6. **`WolverineFx.HealthChecks` docs** (ctx7, `/jasperfx/wolverine`) — the exact `AddWolverine()` / `AddWolverineListeners()` API and the after-`UseWolverine` ordering. **Skills** `critterwatch-install` / `marten-advanced-async-daemon-deep-dive` for console/health-exposure context only.

## Working pattern

ctx7 verify the `WolverineFx.HealthChecks` API and package id → worktree off `main` → `Directory.Packages.props` + three `.csproj` + per-service registration + `MapDefaultEndpoints` → `dotnet build` green → full suite green (no test changes) → ADR 019 + decisions index row + prompt/retro README counts → retro. One PR; the user opens it (outward-facing).

## Out of scope

- **No tracing change** — ADR 005 already configures OTel; CritterWatch's per-instance trace-provider feature stays unconfigured (a separate integration, not a bug).
- **No `ServiceDefaults` change** — registration is per-service; `MapDefaultEndpoints` already exists and is merely called.
- **No non-dev health endpoints** — the dev-only Aspire posture stands; CritterWatch reads registration, not the HTTP endpoint.
- **No test changes / no Alba `/health` test** — the default `MapHealthChecks` response is plain text (`Healthy`), not a JSON check list, and the endpoint is dev-only; the suite passing already proves the startup wiring runs. The live `/health` body + the console flag clearing is verifiable only by booting the full Aspire stack with the console (deferred; see retro).
- **No `nuget.config` change** — the pre-existing NU1507 two-sources warning is untouched (it predates this slice and touching it means the credentialed config).
- **No salvage of the spike's projections / seeders / async daemon** — those are separate candidate slices; this session lands only the health-check finding.
