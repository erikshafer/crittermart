# ADR 019: Wolverine Runtime Health Exposed via ASP.NET Health Checks

**Status**: Accepted

## Context

[ADR 017](017-critterwatch-integrated.md) integrated CritterWatch as the monitoring console. Its per-service overview reports **"Health checks: Not registered"** for Catalog, Inventory, and Orders. The flag is honest: the services registered no Wolverine-aware health check.

Two distinct gaps sat behind the flag:

- **Registration.** `ServiceDefaults.AddDefaultHealthChecks` registered only a bare `AddCheck("self", () => Healthy(), ["live"])` liveness probe. Nothing reported the **Wolverine runtime** (started? cancelled?) or its **listeners** (accepting / too-busy / latched / stopped) to ASP.NET's `HealthCheckService` — which is the registry CritterWatch introspects over its telemetry channel.
- **HTTP exposure.** `ServiceDefaults.MapDefaultEndpoints` (maps `/health` + `/alive`) existed but was **never called** by any service's `Program.cs`, so even the `self` check was unreachable over HTTP.

This gap pre-existed the round-one slices and is unrelated to tracing: [ADR 005](005-opentelemetry-tracing-enabled.md) already configures OpenTelemetry tracing, and CritterWatch's separate per-instance trace-provider feature is a distinct, deliberately-unconfigured integration (out of scope here). The gap surfaced during the throwaway `research/critterwatch-seed-data` spike, which fed the console live data; this slice salvages the merge-worthy finding into `main` through the normal pipeline.

## Decision

Each of the three services registers the **Wolverine bus and listener health checks** and maps the default health endpoints.

**Package.** `WolverineFx.HealthChecks` (6.8.0 — the same Critter Stack 2026 line as the rest of the stack, [ADR 012](012-critter-stack-2026-upgrade.md)) is referenced by Catalog, Inventory, and Orders.

**Registration — per-service, after `UseWolverine`.** In each `Program.cs`, immediately after the Wolverine setup:

```csharp
builder.Services.AddHealthChecks()
    .AddWolverine()           // bus check "wolverine" — runtime startup/cancellation
    .AddWolverineListeners(); // listener check "wolverine-listeners" — listener state
```

The registration lives **per-service rather than folded into the shared `ServiceDefaults.AddDefaultHealthChecks`** for two reasons: (1) it keeps the generic, Aspire-shaped `ServiceDefaults` project free of a Wolverine package dependency — Wolverine health belongs with the Wolverine services that own a runtime; and (2) it matches the `WolverineFx.HealthChecks` guidance, which registers the checks *after* `UseWolverine`. `ServiceDefaults` is unchanged.

**HTTP exposure.** Each service now calls `app.MapDefaultEndpoints()` so `/health` (all checks) and `/alive` (liveness-tagged) actually map. This stays **dev-only** by the standard Aspire posture inside `MapDefaultEndpoints` — exposing health endpoints in non-Development environments carries a security cost, and CritterWatch does not need them: it reads the *registered* checks over its telemetry channel, not over HTTP.

## Consequences

CritterWatch's **"Health checks: Not registered"** flag clears: the console sees the registered `wolverine` and `wolverine-listeners` checks. The two observability layers stay complementary — OpenTelemetry shows request traces ([ADR 005](005-opentelemetry-tracing-enabled.md)); CritterWatch shows operational messaging state, now including health.

The **listener check is a live teaching tie-in**: CritterWatch's chaos monkey can latch a service's listeners, which flips `wolverine-listeners` to unhealthy — so the health panel visibly reacts to console-driven chaos rather than being a static green light. In normal runs both checks are healthy.

**Costs and rejected alternatives.** The registration is one chained call repeated in three `Program.cs` files — accepted in exchange for not coupling the generic `ServiceDefaults` project to `WolverineFx` (the **folded-into-ServiceDefaults** alternative is DRY but couples the shared project to Wolverine and would register the check *before* `UseWolverine`). Registering the **bus check only** was rejected: it clears the flag but says nothing about listener health and forgoes the chaos-monkey tie-in. Making the **HTTP endpoints non-dev** was rejected as unnecessary — CritterWatch does not read them and the dev-only posture is the Aspire security default. The listener check reporting unhealthy under deliberate latching is intended behavior, not a regression.

This ADR realizes a gap surfaced by [ADR 017](017-critterwatch-integrated.md) and builds on [ADR 004](004-dotnet-aspire-orchestrator.md) (the `MapDefaultEndpoints` it finally calls), [ADR 005](005-opentelemetry-tracing-enabled.md) (the complementary trace layer), and [ADR 012](012-critter-stack-2026-upgrade.md) (the Critter Stack line the package version tracks).
