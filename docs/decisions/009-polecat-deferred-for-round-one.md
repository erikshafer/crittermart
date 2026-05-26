# ADR 009: Polecat Deferred for Round One

**Status**: Accepted

## Context

Polecat is the JasperFx identity stack; CritterCab uses it. CritterMart's Identity bounded context could be implemented as a Polecat-backed deployed service, but the six-day round-one timeline makes that a stretch and pulls focus away from the event-sourcing material the talk is built on.

## Decision

Polecat is explicitly deferred. Identity is implemented as a hardcoded customer ID in the frontend for round one, with no deployed Identity service. The customer ID is used in commands, narratives, and OpenSpec scenarios as if it came from a real identity system.

## Consequences

One less service to scaffold; one less stack to learn for the talk. Tradeoff: the talk cannot demonstrate real identity flows. Promoting the stubbed Identity context into a deployed Polecat-backed service is queued in the Vision doc's Long Road as a natural sequel.
