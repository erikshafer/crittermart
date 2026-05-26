# ADR 002: Shared PostgreSQL with Schema-per-Service

**Status**: Accepted

## Context

Each service needs persistence — event-sourced aggregates for Inventory and Orders, document storage for Catalog. The options were per-service databases (true process isolation) or a shared database with schema-per-service (logical isolation, simpler local setup).

## Decision

A single PostgreSQL database, with each service writing to its own schema. Marten is configured per-service via `opts.Schema.For<X>().DatabaseSchemaName(...)` for documents and `opts.Events.DatabaseSchemaName` for event streams.

## Consequences

One Docker container for Postgres, one connection-string family across services. Visible isolation on a slide: each service has its own schema. Tradeoff: this is not the per-service-database isolation a real microservices deployment would have, and the talk is honest about it. Promoting to per-service databases is mechanical and queued as a follow-up.
