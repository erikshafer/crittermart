# ADR 004: .NET Aspire as Orchestrator

**Status**: Accepted

## Context

Three services plus RabbitMQ plus PostgreSQL need to start together locally. Options were docker-compose, .NET Aspire, or manual multi-terminal `dotnet run`. The choice affects both day-to-day developer ergonomics and what the talk can show on a single screen.

## Decision

.NET Aspire orchestrates the topology. An AspireHost project boots the three services, the RabbitMQ container, and the PostgreSQL container. The Aspire dashboard surfaces both the running topology and the OpenTelemetry traces (see ADR 005).

## Consequences

One-command local startup. The dashboard becomes a live demo asset at talk time. Tradeoff: a fourth project (AspireHost) to scaffold, and an Aspire dependency for anyone running the repo locally. Both are acceptable for a teaching reference architecture.
