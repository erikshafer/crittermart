# ADR 005: OpenTelemetry Tracing Enabled

**Status**: Accepted

## Context

The talk needs to demonstrate that event sourcing across services produces a coherent, traceable story. Marten and Wolverine both have first-class OpenTelemetry support, so the instrumentation cost is low and the payoff is high.

## Decision

OpenTelemetry tracing is enabled across all three services. Marten's `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` and `opts.OpenTelemetry.TrackEventCounters()` are wired in. Wolverine's instrumentation is enabled. Traces flow into the Aspire dashboard (see ADR 004).

## Consequences

Instrumentation comes nearly for free. The trace demo lands cleanly in the dashboard and on slides. Tradeoff: production setups would route OpenTelemetry to a real backend (Jaeger, Tempo, etc.); the Aspire dashboard suffices for round one but is not where you would ship.
