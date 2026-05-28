---
retrospective: 002
kind: chore
prompt: docs/prompts/chore/002-infra-bundle-aspire-otel.md
deliverable: src/CritterMart.AppHost/** (new); src/CritterMart.ServiceDefaults/** (new); src/CritterMart.{Catalog,Inventory}/{Program.cs, csproj, Properties/launchSettings.json}; Directory.Packages.props; CritterMart.slnx; docs/prompts/chore/002-...; docs/retrospectives/chore/002-... (this file)
date: 2026-05-28
mode: solo infra, high-autonomy (act-on-leans); ctx7-verified Aspire 13 / OTel
session-runner: Claude (Opus 4.7)
---

# Retrospective — Chore 002: Infra bundle (Aspire + OpenTelemetry)

## Outcome summary

Realized **ADR 004** (.NET Aspire) and **ADR 005** (OpenTelemetry). New `CritterMart.AppHost` orchestrates **Postgres + RabbitMQ + Catalog + Inventory**; new `CritterMart.ServiceDefaults` wires OTel (ASP.NET Core + HttpClient + Wolverine/Marten ActivitySources, OTLP export) + health checks + service discovery + resilience into both services. The Aspire line was bumped **13.2.2 → 13.3.5** (the RabbitMQ hosting package only ships 13.3.x).

**Verified end-to-end (headless):** `dotnet run --project src/CritterMart.AppHost` boots the dashboard, DCP provisions both containers (`postgres:17.6`, `rabbitmq:4.2`), and **both services come up on distinct Aspire-assigned ports** (Catalog `:60923`, Inventory `:60924`) and serve HTTP through them (`GET /products` → `200 []`; `GET /stock/none` → `404`) against the Aspire-provisioned Postgres. All 10 existing tests still pass with `AddServiceDefaults` in the host. **Not verified headlessly:** the dashboard UI + the distributed traces themselves (no browser) — the OTLP wiring is in place; the user should open `http://localhost:15090` to confirm traces visually.

## What worked

- **ctx7 nailed the Aspire 13 API** — `AddPostgres().AddDatabase`, `AddRabbitMQ`, `AddProject<Projects.X>().WithReference().WaitFor`, and the standard ServiceDefaults shape. The whole orchestration compiled on the first build.
- **Naming the Aspire database `crittermart`** meant `WithReference` injects `ConnectionStrings__crittermart`, which both services already read via `GetConnectionString("crittermart")` — **zero connection-config change** in the services.
- **ServiceDefaults built clean on .NET 10** with current OTel 1.15.x packages; the `OTEL_EXPORTER_OTLP_ENDPOINT`-guarded OTLP exporter means it's inert in tests (no endpoint) and active under Aspire.
- **Act-on-leans kept velocity up** — scoped to Aspire + OTel now, deferred Static codegen, and made the call without round-tripping.

## What was harder than expected — three real bugs caught headlessly

1. **AppHost startup throw — missing dashboard env.** Running the AppHost via `dotnet run` without a `Properties/launchSettings.json` fails: the dashboard needs `ASPNETCORE_URLS` + an OTLP endpoint env var (normally in the template-generated launchSettings). Fixed by adding the AppHost launchSettings (https + http profiles); verified with `--launch-profile http`.
2. **Port 5000 collision (the demo-breaker).** The two services had **no `launchSettings.json`**, so under Aspire they both defaulted to Kestrel `:5000` — only one could bind, the other crashed. The symptom (headless) was "only one service process up at a time across runs," and `Get-NetTCPConnection` pinned it: the running service was on **`:5000`**, proving Aspire wasn't assigning managed endpoints. Fixed by adding `launchSettings.json` with distinct `applicationUrl`s (Catalog `:5101`, Inventory `:5102`) so Aspire allocates distinct managed endpoints (verified `:60923`/`:60924`). **This would have broken the demo**; caught only by process/port inspection, not by the build or the host log (which stayed clean).
3. **`WaitFor(rabbitmq)` blocked Inventory.** Inventory doesn't consume RabbitMQ yet, but `WaitFor(rabbitmq)` gated its startup on RabbitMQ's *health check* (which lagged). Removed the gate; RabbitMQ stays *provisioned but unreferenced* until slice 2.2 wires it.

## Methodology refinements that emerged

1. **Headless Aspire verification is finicky but doable.** The dashboard holds resource health/traces (browser-only), and services cycle as they start — so a single `tasklist` snapshot misleads. The reliable headless proxy: `Get-Process` + `Get-NetTCPConnection` (definitive process/port state) + a direct HTTP smoke through the Aspire-assigned port + a clean host-log error scan. That combination caught the `:5000` bug a snapshot would have hidden.
2. **An Aspire service project needs `launchSettings.json` (or explicit `.WithHttpEndpoint`)** for Aspire to assign managed endpoints — otherwise it collides on `:5000`. Bake this into any future new-service skeleton (Orders).
3. **Realizing vs. changing a constraint:** ADR 004/005 were already the documented target, so this needed no `structural-constraints.md` edit — only realization. Distinguish "the rule already says this; we're implementing it" from "we're changing the rule."

## Outstanding items / next-session inputs

1. **Slice 2.2 (Reserve stock) is now unblocked** — the cross-BC centerpiece. Wire Inventory (and the future Orders) to RabbitMQ (`WithReference(rabbitmq)` + the Wolverine RabbitMQ transport) and implement `ReserveStock` Orders→Inventory + the `StockReserved`/`StockReservationFailed` flow. This is where the Aspire **distributed traces** become the money demo.
2. **Static/AOT Wolverine codegen (ADR 012)** — deferred fast follow (`codegen write` + `TypeLoadMode.Static`, drop `RuntimeCompilation` in production publish).
3. **Marten verbose OTel (ADR 005)** — `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` + `TrackEventCounters()` not yet wired (the Marten ActivitySource *is* registered in ServiceDefaults). ADR 005 partially realized (ASP.NET + Wolverine OTel + OTLP export done). Fast follow.
4. **Dashboard/trace visual confirmation = user** (browser, `http://localhost:15090`).
5. **`openspec archive`** (1.3 + 2.1 still unarchived); **`tidy: docs`** debt; **NuGet source mapping** (NU1507 multi-source warning persists, now across Aspire/OTel packages too).
6. **docker/Aspire containers** are ephemeral per run (no data volume) — fine for the demo; add `.WithDataVolume()` if persistence is wanted.

## Spec-delta — landed?

**Yes (with one partial).** ADR 004 (Aspire orchestrating both services + Postgres + RabbitMQ) — **landed**, verified booting. ADR 005 (OpenTelemetry) — **landed for ASP.NET Core + HttpClient + Wolverine sources with OTLP export**; the Marten-verbose-tracking specifics are **deferred** (partial, recorded). No structural-constraints change (realizing, not changing). No slice behavior; all tests green.

## Process notes

- One PR: `chore:` (the infra projects + wiring) and `docs:` (prompt + retro). Branch `chore/infra-bundle-aspire-otel` (created **before** committing — unlike the slice 2.1 slip).
- Aspire bumped 13.2.2 → 13.3.5 across AppHost/PostgreSQL/RabbitMQ.
- High-autonomy session per the user's act-on-leans direction (demo same day).
