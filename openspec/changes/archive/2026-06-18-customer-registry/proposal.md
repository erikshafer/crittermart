## Why

CritterMart's Identity bounded context was a round-one stub — a hardcoded customer id carried on the `X-Customer-Id` seam ([ADR 009](../../../docs/decisions/009-polecat-deferred-for-round-one.md)). [Workshop 002](../../../docs/workshops/002-identity-event-model.md) promoted it to a kept, deployed **EF-Core customer registry** and named its strategic-design relationship to the other contexts (**Open-Host Service + Published Language**). An exploratory spike (`spike/efcore-identity` @ `0ffe42e`) proved the shape end-to-end; this change re-lands that kept code on `main` through the normal per-slice chain, restoring the design→code trace the spike legitimately inverted.

Identity is CritterMart's **one non-event-sourced bounded context**, and that is its teaching payoff. Where Catalog persists documents and Inventory/Orders persist event streams, Identity persists current state in a row, and its single `CustomerRegistered` "event" is not a stream event or the source of truth — it is an outbound integration notification published from a state change through the **EF-Core transactional outbox**. The slice proves Wolverine's handler model (command → handler → cascaded message, one transactional outbox) is persistence-agnostic: the same static-endpoint shape over a `DbContext` instead of an `IDocumentSession`.

Per the consolidate-slice-PRs convention, slices **5.1 Register** and **5.2 Resolve** are one capability and ship in one PR rather than two (the proposal, the [narrative](../../../docs/narratives/006-customer-registration.md), and the implementation land together). The change also adds the one thing the spike deliberately skipped: a **duplicate-email guard** (the spike inserted a new row on every call), modeled in Workshop 002 § 6 slice 5.1's failure path.

## What Changes

- **New service `CritterMart.Identity`** — Wolverine over **EF Core / Npgsql** (not Marten), schema-per-service in the `identity` schema ([ADR 002](../../../docs/decisions/002-shared-postgres-schema-per-service.md)) on the shared Postgres.
- Introduce **`RegisterCustomer`** (`POST /customers`): the storefront registers a customer carrying an email and display name. The service mints an opaque id and a `registeredAt` timestamp, persists a `Customer` row, and cascades **`CustomerRegistered`** through the EF-Core transactional outbox **in the same transaction** — published to RabbitMQ after the commit succeeds.
- **Customer emails are unique**, on a **normalized** key (trimmed + lowercased, case-insensitive). A duplicate is rejected with **`CustomerAlreadyRegistered`** (`409 Conflict`), idempotently: no row written, no event published. Enforced **both** at the application layer (a railway-style `ValidateAsync` → `ProblemDetails` guard, mirroring `PublishProduct.ValidateAsync`) **and** by a DB **unique index** on the email column — the true backstop that closes the check-then-insert race. (Catalog gets DB-level uniqueness for free because the SKU *is* the Marten document id; Identity keys uniqueness on email, which is **not** the primary key, so the index is the faithful mirror of Catalog's actual guarantee.)
- Introduce **`GetCustomer`** (`GET /customers/{id}`): resolve a customer by id, returning `{ id, email, displayName, registeredAt }`, or `404` for an unknown id. This is the **Open-Host Service** read for the storefront — the same shape as Catalog's product reads, read-only (no row mutated, no event).
- **`CustomerRegistered` stays Identity-local** — it is **not** moved to `CritterMart.Contracts`, because nothing cross-BC consumes it yet. It publishes to RabbitMQ unconsumed: the **Published-Language** relationship is *declared* (context map), not yet trafficked. It graduates to Contracts only when slice 5.4 lands a consumer.
- **Aspire-wired** on http `:5105` (CritterWatch owns `:5104`), appearing as a 4th CritterWatch-monitored node, with Wolverine health checks. The SPA gets no Identity URL (no `WithReference`) — Identity has no frontend-driven flow in this slice.

This change introduces **no authentication or authorization** ([ADR 009](../../../docs/decisions/009-polecat-deferred-for-round-one.md) holds) — Identity is a *data store*, not an auth provider; no Polecat. The `X-Customer-Id` seam remains the identity transport and is **not** wired to this registry yet — resolving the header against the registry is the future slice 5.3, which (per [ADR 001](../../../docs/decisions/001-separate-services-topology.md)) will read a consumer-LOCAL model fed by the Published-Language event, never a synchronous call into Identity.

## Capabilities

### New Capabilities

- `customer-registry`: registering customers into the EF-Core registry and resolving them by id. This is the Identity bounded context's one capability — one capability per aggregate/document type, the `Customer` registry record being the unit. Slices 5.1 (register) and 5.2 (resolve) introduce its requirements. The future OHS/PL integration slices (5.3 resolve `X-Customer-Id`, 5.4 consume `CustomerRegistered`) land their requirements in the **consuming** bounded contexts when a consumer genuinely needs customer data — not in this capability.

### Modified Capabilities

<!-- None. This is the first capability for the Identity bounded context; no existing specs/ to modify. -->

## Impact

- **New service:** `src/CritterMart.Identity` (4th deployed service) + `tests/CritterMart.Identity.Tests`. EF Core / Npgsql on the shared PostgreSQL under the `identity` schema.
- **Persistence:** a mutable `Customer` row (no stream, no projection, no fold — the row *is* the read model) plus the Wolverine inbox/outbox envelope tables mapped into `IdentityDbContext`, all in the `identity` schema. A unique index on the (normalized) email column.
- **Packages (central management):** `WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql`, `Npgsql.EntityFrameworkCore.PostgreSQL` — the EF-Core line, alongside the existing Wolverine/Marten pins.
- **HTTP surface:** Wolverine.Http `POST /customers` (register) + `GET /customers/{id}` (Open-Host Service read). No synchronous service-to-service calls (ADR 001).
- **Messaging:** `CustomerRegistered` cascaded to the EF-Core outbox and published over RabbitMQ ([ADR 003](../../../docs/decisions/003-wolverine-rabbitmq-transport.md)) — **unconsumed**; the Published-Language edge is declared in the context map but carries no traffic yet.
- **Orchestration:** AppHost gains an `identity` resource (`:5105`) waiting on Postgres + RabbitMQ + CritterWatch; no SPA reference.
- **Identity transport:** the `X-Customer-Id` seam is unchanged and not wired to this registry — round-one identity stays stubbed ([ADR 009](../../../docs/decisions/009-polecat-deferred-for-round-one.md)).
- **Reference implementation:** the kept code re-applies `spike/efcore-identity` @ `0ffe42e` (15 files) plus the duplicate-email guard; the spike branch is retained as the reference, not merged.
- **Downstream artifacts:** `design.md` + `tasks.md` are authored in the same (consolidated) implementation session per the consolidate-slice-PRs convention.
