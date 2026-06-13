# ADR 017: CritterWatch Integrated — Out-of-Band Trial, Single-Node, nuget.org-Sourced

**Status**: Accepted

This is the successor ADR that [ADR 013](013-critterwatch-deferred-to-messaging-slices.md) predicted ("a successor ADR will record the actual integration when slice 4.2 makes it earn its place"). It records the integration as shipped and resolves ADR 013's Open Question.

## Context

[ADR 013](013-critterwatch-deferred-to-messaging-slices.md) deferred CritterWatch to the 4.x messaging slices and left three things open: which **tier** is targeted, whether that tier ships from the auth-gated `packages.jasperfx.net` feed (which 401s on the public CI runner — PR #23 removed it as a restore source), and whether it needs a **license key**. The deferral held: by the time CritterWatch landed, slices 4.2–4.7 had stood up real cross-BC RabbitMQ traffic worth monitoring, and the Aspire AppHost already orchestrated the Postgres and RabbitMQ the console needs.

CritterWatch (JasperFx 0.9.1) was then installed **out of band** — alongside the modeled-slice pipeline rather than as a numbered slice, since it monitors the system rather than adding domain behavior. The integration is real and shipped (it rode into `main` in commit `2b127f4`); the canonical decision record had not caught up. This ADR catches it up.

## Decision

CritterWatch is integrated as a **single-node monitoring console** orchestrated by Aspire, sourced from **nuget.org**, running on a **Trial** license.

**Topology.** A dedicated `CritterMart.CritterWatch` ASP.NET host references the `CritterWatch` package and calls `builder.AddCritterWatch(connectionString, opts => …)` + `app.UseCritterWatch()` (which maps the Wolverine HTTP endpoints, the SignalR hub, and the embedded SPA). Each monitored service (Catalog, Inventory, Orders) references `Wolverine.CritterWatch` and calls `opts.AddCritterWatchMonitoring(metricsQueueUri, controlQueueUri)` inside `UseWolverine`, setting a unique `opts.ServiceName`. Telemetry flows over the **existing RabbitMQ broker** to a shared `critterwatch` queue; the console returns control commands on each service's private `{service}-control` queue.

**Package sourcing — resolves ADR 013's feed question.** CritterWatch 0.9.1 (`CritterWatch` console + `Wolverine.CritterWatch` client) publishes to **nuget.org**, not only the private feed. So no `packages.jasperfx.net` restore source was re-added; the nuget.org-only `nuget.config` from PR #23 stands and CI stays green without paid-feed access. The integration was confined to a console project plus thin per-service client references — the public, teaching-oriented build restores unchanged. The 401-on-CI risk ADR 013 named does not materialize.

**License — resolves ADR 013's tier/key question.** The targeted tier is **Trial** (a 30-day evaluation, current trial expiring 2026-07-10). The license key is read from configuration at `JasperFx:LicenseKey` and is held in **user-secrets locally — never committed**. Without a valid key the console falls back to a read-only Free tier.

**Dedicated database — outside schema-per-service.** The console keeps its own Marten event store and projections in a dedicated `critterwatch` database on the shared Postgres container, separate from the `crittermart` demo database. [ADR 002](002-shared-postgres-schema-per-service.md)'s schema-per-service governs *CritterMart's* services, not third-party tooling; giving CritterWatch its own database keeps its tables out of the demo schemas entirely.

**Single-node, non-clustered.** The console runs with `enableClusterPartitioning: false`. CritterWatch 0.9.1's default clustered mode requires a sharded RabbitMQ queue topology (`UseShardedRabbitQueues`) and refuses to start without one; a single local console node does not need it. The `critterwatch` listener is configured `Sequential()` so per-service telemetry applies in order against its event stream — the same ordering guarantee one node already provides.

**Aspire wiring + the resource-name gotcha.** The AppHost adds the `critterwatch` database and the console project, with the three services `WaitFor(critterwatch)` so the dashboard sees their startup live. The console project resource is named **`critterwatch-console`** because the database resource already owns the name **`critterwatch`**, and Aspire resource names share **one case-insensitive namespace** — the database name is also what `WithReference` injects as the connection-string key, so the database keeps the bare name. The AppHost runs the console with **`ASPNETCORE_ENVIRONMENT=Production`** (see the gotcha below).

## Consequences

CritterMart gains a second, complementary observability layer — live Wolverine node/agent/endpoint health and messaging topology — alongside the OpenTelemetry request traces in the Aspire dashboard ([ADR 005](005-opentelemetry-tracing-enabled.md)). The two are not redundant: OTel shows request traces, CritterWatch shows operational messaging state. The console renders meaningfully now because slices 4.2–4.7 produce real cross-BC traffic, which is exactly the timing ADR 013 deferred to.

**The Production-environment gotcha (the load-bearing operational note).** CritterWatch's license validation only runs outside Development. In the Development environment it silently substitutes a "Development" tier (expires never) and never reads `JasperFx:LicenseKey` — which *masks* whether the real Trial key actually validates. The AppHost therefore runs the console as **Production**, and the console host **explicitly loads user-secrets** (`AddUserSecrets(..., optional: true)`) so the Trial key is still found when running Production locally. Anyone debugging "why does the console show a Development tier?" should check the environment first.

**Accepted tradeoff — a suppressed transitive CVE.** CritterWatch 0.9.1 transitively pulls MessagePack 2.5.x, which carries a high-severity advisory (GHSA-hv8m-jj95-wg3x). The fix (MessagePack ≥ 3.0.214) is a major-version bump that breaks CritterWatch, so the advisory is suppressed via `NuGetAuditSuppress` in `Directory.Packages.props` until an upstream CritterWatch release resolves it. This is a known, time-boxed exposure tied to the trial; revisit when CritterWatch updates.

**Trial expiry is a live deadline.** The Trial license expires 2026-07-10. Past that the console drops to the read-only Free tier unless the tier decision is revisited (a paid tier from the private feed would reopen ADR 013's CI/feed design — at that point CritterWatch would need either an opt-in project excluded from the default CI restore or authenticated NuGet on CI via a secret). That decision is out of scope here; this ADR records the trial integration as it stands.

Rejected alternatives. **Re-adding the `packages.jasperfx.net` feed** — unnecessary, since 0.9.1 is on nuget.org, and it would re-break CI exactly as ADR 013 warned. **Clustered mode** — needs a sharded queue topology a single local node does not justify. **A shared schema in the `crittermart` database** — would blur third-party tooling tables into the demo's schema-per-service story; a dedicated database keeps the teaching model clean. **Running the console as Development** — silently masks the real license tier.

This ADR pairs with and realizes [ADR 013](013-critterwatch-deferred-to-messaging-slices.md) (the deferral it fulfills), and builds on [ADR 002](002-shared-postgres-schema-per-service.md) (why the console gets its own database), [ADR 003](003-wolverine-rabbitmq-transport.md) (the broker it reuses), [ADR 004](004-dotnet-aspire-orchestrator.md) (the orchestration), and [ADR 005](005-opentelemetry-tracing-enabled.md) (the complementary trace layer).
