# ADR 006: Wolverine.Http API Surface per Service, No Separate BFF for Round One

**Status**: Accepted

## Context

Three services each need an HTTP surface for the frontend to call. The frontend could call each service directly, or a Backend-For-Frontend project could compose calls across services into a single API the frontend consumes.

## Decision

Each service exposes its own Wolverine.Http endpoints. The frontend (stack TBD) calls each service directly. No separate BFF project for round one.

## Consequences

One fewer project to scaffold and reason about. The frontend orchestrates calls across services, which is a teachable pattern in its own right. Tradeoff: production setups typically introduce a BFF for aggregation, auth, and rate-limiting. Promoting Wolverine.Http endpoints into a dedicated BFF is queued as a follow-up topic and likely blog post.
