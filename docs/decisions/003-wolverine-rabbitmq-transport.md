# ADR 003: Wolverine Messaging with RabbitMQ Transport

**Status**: Accepted

## Context

With separate services (see ADR 001), cross-service messaging needs a broker. Wolverine supports many transports; the choice mattered for local-dev footprint, OpenTelemetry demo richness, and alignment with existing Critter Stack sample code.

## Decision

RabbitMQ is the message transport for round one. It matches the EcommerceMicroservices sample in the CritterStackSamples repo, has the lightest local-dev footprint (a single container), and Wolverine's RabbitMQ integration is the most mature.

## Consequences

One additional container in the Aspire orchestration. The OpenTelemetry trace shows messages crossing the broker, which lands well on a slide. Other transports remain available without changing handler code, which is the headline pedagogical point. "Swapping the bus" remains a follow-up topic and a candidate future ADR.
