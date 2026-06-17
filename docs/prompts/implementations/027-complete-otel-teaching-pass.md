# Prompt: Implementations 027 — Complete the OpenTelemetry teaching pass (Marten event-store traces + metrics)

**Kind**: round-two **verification + ADR-realization** slice — closes the OTel in-browser visual pass owed since chore/002 *and* lands the deferred half of ADR 005. The talk's thesis is "event sourcing across services produces a coherent, traceable story"; until now the trace stopped at HTTP + Wolverine spans and the event-store layer was invisible. **No new event, command, projection, or index — two infra wirings (Marten OTel + metrics meters) + a teaching artifact.**
**Source**: the post-#69 open pick. The owner chose the **OTel / in-browser visual pass** from a four-candidate `AskUserQuestion` (over "My Orders" list, cart identity-transport harmonization, the §6.1 tidy). A second `AskUserQuestion` settled the one genuine fork — *how the dashboard screenshots get captured* — at **"I boot+drive, owner screenshots"** (the chore/002 precedent: visual confirmation = user). **Cadence**: 2nd implementation after the #68 design-return (the #69 fix was the 1st); budget intact, one interleave still in the tank.
**Files touched**: this prompt; `src/CritterMart.{Catalog,Inventory,Orders}/Program.cs` (Marten `TrackConnections`/`TrackEventCounters` + `using JasperFx.OpenTelemetry`); `src/CritterMart.ServiceDefaults/Extensions.cs` (`.AddMeter("Marten")` + `.AddMeter("Wolverine:*")`); `docs/decisions/005-opentelemetry-tracing-enabled.md` (Realization note → fully realized); `docs/research/otel-trace-walkthrough.md` (new teaching artifact); `docs/{prompts,retrospectives}/README.md` (implementations 26 → 27); `docs/retrospectives/implementations/027-complete-otel-teaching-pass.md`.
**Mode**: solo. One fork surfaced (visual-capture approach) and was owner-decided; the code itself is **ADR-005-settled** (the ADR names `TrackConnections = TrackLevel.Verbose` + `TrackEventCounters()` verbatim) — a no-fork realization, not a new decision.
**Commit subject**: `feat: complete the OpenTelemetry teaching pass — Marten verbose tracking + event-append metrics across all three services`

## Framing

[ADR 005](../decisions/005-opentelemetry-tracing-enabled.md) decided OpenTelemetry across all three services, naming Marten's `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` and `TrackEventCounters()` explicitly. [chore/002](../../retrospectives/chore/002-infra-bundle-aspire-otel.md) realized only the ASP.NET + Wolverine + HttpClient half over OTLP; its retro logged two deferrals as fast-follows: the Marten verbose/counter config (item #3) and the in-browser trace visual confirmation (item #4). Both have sat open since 2026-05-28.

Two defects compound: (1) Marten's event-store work was **absent from the trace** — a handler span showed *that* a handler ran but not the appends it committed; and (2) `ConfigureOpenTelemetry` registered the `"Marten"`/`"Wolverine"` *ActivitySources* on tracing but **no meters at all**, so `marten.event.append` and the Wolverine message counters emitted into a void (the `wolverine-observability-opentelemetry-setup` skill flags this exact "source without meter" anti-pattern). A code-only pass would have shipped `TrackEventCounters()` looking done while exporting nothing — which is why this slice is verification-led, against a live Aspire boot.

## Goal

The cross-service purchase trace (one `POST /orders` → `ReserveStock`/`StockReserved`/`CommitStock` over RabbitMQ + the in-process payment hops) shows the event-store layer: `marten.connection` spans with the write ops tagged, sitting under the HTTP and Wolverine handler spans, and `marten.event.append` exporting per `event_type`. Confirmed against a live boot (services healthy with the new wiring; the full W1→W4 saga drives to `confirmed`; no telemetry export errors). The owner captures the dashboard screenshots; the session leaves a walkthrough with the observed span hierarchy and exact dashboard navigation. Full test suite green; `dotnet build` clean.

## Spec delta

The canonical spec here is **[ADR 005](../decisions/005-opentelemetry-tracing-enabled.md)** (the decision this realizes). The delta: ADR 005 gains a **## Realization** note recording the two-phase landing (chore/002 partial → 027 complete) and flips to **fully realized**, cross-referencing a **new research artifact** `docs/research/otel-trace-walkthrough.md` (the trace/metric walkthrough + screenshot guide). Spec-shaped: *new ADR realization note + new cross-referenced artifact.* Four-step closure: **this prompt names it → the session executes → the retro confirms → ADR 005 records it.**

## Orientation

1. **CLAUDE.md** — one-prompt-one-PR, no-opportunistic-edits, `{type}/{slug}` branch (`feat/otel-marten-tracing`, created). The Marten wiring + the meter registration + the ADR note + the walkthrough land in **one** PR.
2. **[ADR 005](../decisions/005-opentelemetry-tracing-enabled.md)** — the decision being realized; names the exact Marten config.
3. **[chore/002 retro](../../retrospectives/chore/002-infra-bundle-aspire-otel.md)** items #3 (Marten verbose deferred) + #4 (visual = user) — the two deferrals this slice closes, and the precedent that the owner captures the visual.
4. **The skill that settles the wiring**: `wolverine-observability-opentelemetry-setup` — confirms the `"source without meter"` anti-pattern, the `marten.event.append` (`event_type`, `tenant.id`) counter, and the crucial nuance that the Wolverine **meter** is `Wolverine:{ServiceName}` (≠ the `"Wolverine"` ActivitySource).
5. **The wire points**: each service's `AddMarten(opts => …)` block (insert before `.IntegrateWithWolverine()`); `ServiceDefaults.ConfigureOpenTelemetry`'s `WithMetrics` lambda.
6. **Version reality over docs**: the `TrackLevel` enum lives in `JasperFx.OpenTelemetry` in Marten 9.6.0 (not `Marten`, as the upstream master docs imply) — verified against the restored assembly. This is exactly the case the project's ctx7 rule guards against.

## Working pattern

**Wire first**: `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` + `opts.OpenTelemetry.TrackEventCounters()` into all three `AddMarten` blocks (+ `using JasperFx.OpenTelemetry`); `.AddMeter("Marten")` + `.AddMeter("Wolverine:*")` into ServiceDefaults' `WithMetrics`. → `dotnet build`. → **Live boot**: kill orphans, boot the AppHost (background + log), poll `/health` on `:5101/:5102/:5103`, read the per-boot dashboard token. → **Drive W1→W4**: publish product (Catalog), receive stock (Inventory), add-to-cart with `productSnapshot` (Orders), `POST /orders` (Orders) → poll to `confirmed`, confirm stock `100→98 / reserved→committed`. → confirm no telemetry export errors in the log. → author the walkthrough + the ADR note. → full `dotnet test`. → hand the live dashboard link + the orderId to the owner for screenshots; tear down on the owner's go. One PR on `feat/otel-marten-tracing`; **the owner merges**.

## Deliverable plan

- **`src/CritterMart.{Catalog,Inventory,Orders}/Program.cs`** — `TrackConnections = TrackLevel.Verbose` + `TrackEventCounters()` in each `AddMarten`; `using JasperFx.OpenTelemetry`. No handler/projection/index change.
- **`src/CritterMart.ServiceDefaults/Extensions.cs`** — `.AddMeter("Marten")` + `.AddMeter("Wolverine:*")` in `ConfigureOpenTelemetry`'s metrics pipeline.
- **`docs/decisions/005-opentelemetry-tracing-enabled.md`** — `## Realization` note (two-phase landing, fully realized, cross-ref the walkthrough).
- **`docs/research/otel-trace-walkthrough.md`** (new) — the CLI-confirmed reproduction, the observed span hierarchy, the `marten.event.append` event-type table, and the exact Aspire-dashboard click-paths for the owner's screenshots.
- **Docs** — `docs/{prompts,retrospectives}/README.md` implementations 26 → 27; retro 027.

## Out of scope

- **No Prometheus scrape endpoint / `MapPrometheusScrapingEndpoint`** — the Aspire dashboard is the round-one OTLP sink (ADR 005); a scrape endpoint is a production concern.
- **No `[Audit]` tags / custom metric tags / per-endpoint telemetry tuning** — real OTel features, but beyond realizing ADR 005's named config; would be opportunistic.
- **No embedded screenshots** — the owner captures the slide visuals in-browser (chore/002 precedent). The walkthrough is the navigation guide, not an image dump.
- **No `TrackLevel.Normal` production profile / sampling** — noted as a caveat in the walkthrough; not configured here.
- **No openspec archive of `harden-add-to-cart-snapshot` and no workshop §6.1 flip** — those are the *separate* post-#69 tidy candidate (open-pick option 4), explicitly not this PR. Named here so they are not silently absorbed.
