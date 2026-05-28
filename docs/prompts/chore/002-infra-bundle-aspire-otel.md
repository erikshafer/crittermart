# Prompt: Chore 002 — Infra bundle (Aspire + OpenTelemetry)

**Kind**: chore (cross-cutting infrastructure; realizes ADR 004 + ADR 005)
**Files touched**: this prompt; `src/CritterMart.AppHost/**` (new); `src/CritterMart.ServiceDefaults/**` (new); `src/CritterMart.{Catalog,Inventory}/Program.cs` (AddServiceDefaults) + `.csproj` (ServiceDefaults ref) + `Properties/launchSettings.json` (new, distinct ports); `Directory.Packages.props` (Aspire 13.3.5 + OTel); `CritterMart.slnx`; `docs/retrospectives/chore/002-infra-bundle-aspire-otel.md` (forthcoming)
**Mode**: solo infra; ctx7-verified Aspire 13 / OTel API; high-autonomy (act-on-leans) for a same-day demo
**Commit subject(s)**: `chore: infra bundle — Aspire AppHost + ServiceDefaults (OTel)` + `docs: infra-bundle prompt + retro`

## Framing

With two services now (Catalog + Inventory), the infra bundle becomes worthwhile and is a **prerequisite for the cross-BC Orders/Inventory slices** (RabbitMQ) and for a legible demo (Aspire dashboard + distributed traces). This realizes **ADR 004** (.NET Aspire orchestrator) and **ADR 005** (OpenTelemetry) — both already named as round-one targets in `docs/rules/structural-constraints.md`, so this is *realizing* existing constraints, not changing them (no rule-file edit needed).

Scope decision (act-on-leans): **Aspire AppHost + ServiceDefaults (OTel) + RabbitMQ provisioning now; defer Static/AOT Wolverine codegen (ADR 012)** as a fast follow (least demo-relevant, fiddliest). A `chore`, not a slice — the one-PR-per-slice rule doesn't apply, but it's naturally one PR.

## Goal

`dotnet run --project src/CritterMart.AppHost` boots Postgres + RabbitMQ + Catalog + Inventory, orchestrated, with the Aspire dashboard and OpenTelemetry wired so traces flow to the dashboard. Existing tests stay green.

## Spec delta

No slice behavior. Realizes ADR 004 (Aspire AppHost orchestrating the two services + Postgres + RabbitMQ) and ADR 005 (OTel via ServiceDefaults — ASP.NET Core + HttpClient + Wolverine sources, OTLP export to the dashboard). New `CritterMart.AppHost` + `CritterMart.ServiceDefaults` projects. Aspire line bumped 13.2.2 → 13.3.5 (RabbitMQ package only ships 13.3.x). The `product-catalog`/`stock-management` capabilities and all narratives are unchanged.

## Orientation

1. **ADR 004, ADR 005**, and `docs/rules/structural-constraints.md` (Observability + Service-topology sections — the targets being realized).
2. **Skill / ctx7** for Aspire 13: `DistributedApplication.CreateBuilder`, `AddPostgres().AddDatabase`, `AddRabbitMQ`, `AddProject<Projects.X>().WithReference().WaitFor()`; the standard ServiceDefaults `AddServiceDefaults()` (OTel + health + service discovery + resilience).
3. **`src/CritterMart.{Catalog,Inventory}/Program.cs`** — where `AddServiceDefaults()` plugs in; the services read `GetConnectionString("crittermart")`, so the Aspire database is named `crittermart` (its `WithReference` injects `ConnectionStrings__crittermart`).

## Working pattern

1. Pin packages (Aspire 13.3.5: AppHost/PostgreSQL/RabbitMQ; OTel: Extensions.Hosting, Exporter.OTLP, Instrumentation.AspNetCore/Http; Microsoft.Extensions.ServiceDiscovery + Http.Resilience).
2. `CritterMart.ServiceDefaults` (standard Aspire `Extensions.AddServiceDefaults`, OTLP-on-`OTEL_EXPORTER_OTLP_ENDPOINT`, Wolverine + Marten ActivitySources).
3. `CritterMart.AppHost` (Aspire.AppHost.Sdk csproj + Program.cs orchestrating Postgres db `crittermart` + RabbitMQ + both services). **AppHost needs `Properties/launchSettings.json`** (ASPNETCORE_URLS + OTLP endpoint env) or the dashboard config throws at startup.
4. Wire `AddServiceDefaults()` + ServiceDefaults project ref into both services. **Each service needs `Properties/launchSettings.json` with a distinct `applicationUrl`** — without it, services default to Kestrel `:5000` and collide under Aspire (only one binds).
5. Build; run the AppHost; verify both containers provision and both services come up on distinct Aspire-assigned ports and serve HTTP. Re-run the test suites (AddServiceDefaults runs in the Alba test host too).

## Out of scope

- **Static/AOT Wolverine codegen** (ADR 012) — deferred fast follow.
- **Wiring services to *consume* RabbitMQ** — RabbitMQ is provisioned + ready, but the Wolverine RabbitMQ transport + cross-BC message flow is **slice 2.2** (Reserve stock).
- **Marten verbose OTel** (`TrackConnections = Verbose` + `TrackEventCounters`, ADR 005) — the Marten ActivitySource is registered, but the explicit verbose-tracking config is deferred (ADR 005 partially realized). Fast follow.
- No structural-constraints edit (this realizes existing constraints, doesn't change them). No openspec change (infra, no capability/spec).
