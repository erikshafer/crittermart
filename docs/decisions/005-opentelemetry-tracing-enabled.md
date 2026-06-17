# ADR 005: OpenTelemetry Tracing Enabled

**Status**: Accepted

## Context

The talk needs to demonstrate that event sourcing across services produces a coherent, traceable story. Marten and Wolverine both have first-class OpenTelemetry support, so the instrumentation cost is low and the payoff is high.

## Decision

OpenTelemetry tracing is enabled across all three services. Marten's `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` and `opts.OpenTelemetry.TrackEventCounters()` are wired in. Wolverine's instrumentation is enabled. Traces flow into the Aspire dashboard (see ADR 004).

## Consequences

Instrumentation comes nearly for free. The trace demo lands cleanly in the dashboard and on slides. Tradeoff: production setups would route OpenTelemetry to a real backend (Jaeger, Tempo, etc.); the Aspire dashboard suffices for round one but is not where you would ship.

## Realization

Landed in two phases:

- **chore/002 (2026-05-28) — partial.** Aspire + the ASP.NET Core / HttpClient / Wolverine ActivitySources + OTLP export. The Marten-specific half of the decision (`TrackConnections = TrackLevel.Verbose` + `TrackEventCounters()`) was deferred as a fast follow (chore/002 retro item #3), and the metrics pipeline registered no meters at all — so the `marten.event.append` counter and the Wolverine message counters emitted nowhere.
- **implementations/027 (2026-06-17) — complete.** Wired `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` + `opts.OpenTelemetry.TrackEventCounters()` into all three services' `AddMarten` blocks (the `TrackLevel` enum lives in `JasperFx.OpenTelemetry` in Marten 9, not `Marten`), and registered `.AddMeter("Marten")` + `.AddMeter("Wolverine:*")` in `ServiceDefaults.ConfigureOpenTelemetry`'s metrics pipeline (the Wolverine *meter* is `Wolverine:{ServiceName}`, distinct from the `"Wolverine"` ActivitySource — a wildcard covers all three services from the shared defaults). The cross-service purchase trace now shows the event-store layer (`marten.connection` spans with tagged write ops) alongside the HTTP and Wolverine handler spans, and `marten.event.append` exports per event type. Confirmed against a live Aspire boot; the trace + metric walkthrough (and the dashboard screenshot guide) is [`docs/research/otel-trace-walkthrough.md`](../research/otel-trace-walkthrough.md).

ADR 005 is now **fully realized**.
