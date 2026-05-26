# ADR 001: Separate Services Topology

**Status**: Accepted

## Context

Round one's architectural shape was a choice between a modular monolith (single project, in-process LocalQueue messaging, schema-per-module) and separate services (one project per bounded context, brokered messaging, schema-per-service). The talk benefits materially from OpenTelemetry traces spanning real processes rather than in-process queues.

## Decision

Three separate services are deployed for round one: Catalog, Inventory, and Orders. Each is its own project with its own `Program.cs`, Marten configuration, and Wolverine.Http surface. Identity is intentionally stubbed for round one (see ADR 009).

## Consequences

Cross-service messaging via a real broker gives the OpenTelemetry trace demo real teeth. Handlers remain code-identical to a modular-monolith equivalent, so the "topology is a deployment decision, not a code decision" claim is demonstrable. Cost: three services' worth of scaffolding ceremony versus a single host.
