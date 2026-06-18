# Design: Customer registry — landing the EF-Core Identity service on `main`

## Context

[Workshop 002](../../../docs/workshops/002-identity-event-model.md) promoted Identity from a round-one stub ([ADR 009](../../../docs/decisions/009-polecat-deferred-for-round-one.md)) to a kept **EF-Core customer registry** — CritterMart's one non-event-sourced bounded context. The reference implementation is the spike `spike/efcore-identity` @ `0ffe42e`, live-verified in-stack: 4 services healthy, register/read/404 over HTTP, four schemas coexisting, the outbox drained to 0, the `CustomerRegistered` exchange present with no bindings. This change re-lands that 15-file shape on `main` through the per-slice chain (proposal → narrative → prompt → code → retro), restoring the design→code trace the spike inverted, and adds the one guard the spike skipped.

The genuine engineering content is small — the spike already settled the hard parts (EF-over-Wolverine wiring, the column-casing reconciliation). What this design records is *why* those choices are right, plus the one new decision the owner settled: the duplicate-email guard.

## Goals / Non-Goals

**Goals:**
- A `CritterMart.Identity` service on `main` — Wolverine over EF Core / Npgsql, schema-per-service `identity` — satisfying the `customer-registry` capability's three requirements (register; email uniqueness; resolve).
- Register a customer → a `Customer` row + `CustomerRegistered` cascaded through the EF-Core transactional outbox in one transaction; resolve by id (the Open-Host Service read).
- Reject a duplicate **normalized** email with `CustomerAlreadyRegistered` (`409`), idempotently, backed by both an app-level guard and a DB unique index.

**Non-Goals:**
- No authentication / authorization / Polecat (ADR 009) — Identity is a *data store*. No login, claims, or sessions.
- No `X-Customer-Id` resolution against the registry (slice 5.3, future); no cross-BC consumer of `CustomerRegistered` (slice 5.4, future).
- No move of `CustomerRegistered` to `CritterMart.Contracts` (Decision 4) — it stays Identity-local until a consumer lands.
- No frontend / SPA wiring — Identity has no frontend-driven flow this slice.

## Decisions

### Decision 1 — Wolverine over EF Core, schema-per-service `identity` (the persistence-agnostic teaching beat)

Identity runs Wolverine over an `IdentityDbContext` (Npgsql), not Marten: `PersistMessagesWithPostgresql(conn, "identity")` + `AddDbContextWithWolverineIntegration<IdentityDbContext>` + `UseEntityFrameworkCoreWolverineManagedMigrations` + `AutoApplyTransactions`. The `Customer` row insert and the cascaded `CustomerRegistered` commit in **one transaction**; the handler never calls `SaveChanges` by hand — the transactional middleware owns the commit, exactly as the Marten endpoints never commit by hand.

**Why, and why this is the point:** the slice exists to prove Wolverine's handler model is **persistence-agnostic** — the same static-endpoint shape and the same transactional-outbox guarantee over a `DbContext` instead of an `IDocumentSession`. It is CritterMart's *"when an event store would be over-engineering"* example: current-state CRUD where the row is the source of truth and the one "event" is an outbound notification, not a stream. Re-applied verbatim from the spike (ADR 002 schema-per-service is the EF mirror of the Marten services' `DatabaseSchemaName`).

**Alternative rejected:** making Identity event-sourced (a `Customer` stream + projection) for uniformity. That would manufacture a stream and a fold for data that is pure current state — the exact over-engineering the BC is designed to contrast against.

### Decision 2 — Explicit lowercase column names: the Weasel-vs-EF reconciliation (load-bearing)

`IdentityDbContext` names every column lowercase (`HasColumnName("id")`, `"email"`, `"display_name"`, `"registered_at"`).

**Why:** `UseEntityFrameworkCoreWolverineManagedMigrations` drives **Weasel** to emit the table DDL with **unquoted** identifiers, which Postgres folds to lowercase (`Id` → `id`). EF Core's runtime always **quotes** identifiers (`"Id"`), which is case-sensitive and would miss the folded column. Naming the columns lowercase makes the two agree (EF emits `"id"`, Weasel creates `id`). The Marten services dodge this because Marten owns both the DDL and the queries; the moment two tools share a table, casing must be reconciled. This is the spike's load-bearing fix — kept verbatim.

### Decision 3 — Duplicate-email guard: normalized email, enforced at the app layer AND the database (owner-settled fork)

The spike inserted a row on every `POST /customers`. The kept service rejects a duplicate. Two sub-decisions were **settled with the owner** before authoring:

- **Uniqueness key = normalized email** (trimmed + lowercased). `RegisterCustomer` normalizes once (`Normalize(email) => email.Trim().ToLowerInvariant()`) and reuses the result for the guard query, the stored row, and the published event. A guard that `Ada@` could slip past `ada@` would not be a uniqueness guard at all.
- **Enforced twice over.** A railway-style `ValidateAsync(RegisterCustomer, IdentityDbContext)` returns `CustomerAlreadyRegistered` (`409 Conflict`) for the common case — mirroring `PublishProduct.ValidateAsync` exactly — and short-circuits before the handler, so the duplicate path inserts no row and cascades no event (idempotent). Behind it, a **unique index** on the email column (`e.HasIndex(x => x.Email).IsUnique()`) is the true backstop that closes the check-then-insert race the app check can't.

**Why both layers:** the app guard alone is racy (two concurrent registrations could both pass the check before either commits); the index alone gives a raw `DbUpdateException`, not a friendly `409`. Together they are the faithful mirror of **Catalog's actual guarantee** — Catalog gets DB-level uniqueness *for free* because a product's SKU **is** its Marten document id (the primary key enforces it), whereas Identity keys uniqueness on email, which is **not** the primary key, so it must add the index to reach the same guarantee level. The `ValidateAsync` is the only net-new code over the spike; the index is the only schema addition.

**Alternative rejected:** app guard only (mirror `PublishProduct.ValidateAsync` line-for-line, no index). Simpler and code-symmetric, but leaves a real check-then-act race — and would be a *weaker* guarantee than Catalog's, not an equal mirror, because Catalog's SKU-as-PK backstop has no Identity analogue without the explicit index.

### Decision 4 — `CustomerRegistered` stays Identity-local (no `CritterMart.Contracts` move)

`CustomerRegistered` is defined in `CritterMart.Identity.Customers`, not in the shared `CritterMart.Contracts` assembly. It cascades to the EF-Core outbox and publishes over RabbitMQ (conventional routing) to its own exchange — **with nothing consuming it.**

**Why:** the **Published-Language** relationship is *declared* in the context map but carries no traffic yet. Moving the event to `Contracts` before a consumer exists would manufacture a premature shared contract — the very coupling the context map's OHS+PL split is careful about. It graduates to `Contracts` the moment slice 5.4 lands a consumer (likely Orders enriching `OrderStatusView` with a display name). Until then, Identity-local is correct, and the unconsumed publish keeps the transactional-outbox beat real (the message is genuinely on the wire) with zero cross-BC coupling.

### Decision 5 — Server-minted opaque string id (not a `Guid` type, not the email)

`Customer.Id` is a `string` (a `Guid.NewGuid().ToString()`), the primary key; the email is a separate unique-indexed column.

**Why:** a `string` id lines up with the storefront's `X-Customer-Id` seam (ADR 009) — a future slice 5.3 could resolve that header against this table without a type change. Keying the PK on email instead would conflate the natural key with the surrogate key and make the id leak PII into URLs (`GET /customers/ada@example.com`). The opaque id in the `Location` header and `GET /customers/{id}` route keeps the email out of the addressable surface.

## Risks / Trade-offs

- **[Unconsumed RabbitMQ publish]** → `CustomerRegistered` publishes to an exchange with no bindings. This is intentional (Decision 4), not a leak — verified in the spike's live boot (exchange present, zero bindings, outbox drains to 0). It is a deliberate non-terminal edge a later slice (5.4) completes, flagged in the retro rather than shipped silently.
- **[Two tools share the `identity` schema]** → Weasel (managed migrations) and EF Core both touch the `customers` table; the column-casing reconciliation (Decision 2) is what keeps them in agreement. The unique index is part of the EF model, so Weasel applies it on startup — no manual migration.
- **[Ephemeral dev/test DB]** → Aspire Postgres is fresh each boot and the integration suite spins its own Testcontainer, so the new unique index and the `identity` schema self-apply on startup with no migration concern.
- **[`409` vs `Conflict` semantics]** → a duplicate registration is a `409 Conflict`, mirroring `ProductAlreadyPublished`. Consistent across the two registry-style guards in the codebase.

## Migration Plan

No data migration — a new service with a new schema. Deploy = the AppHost gains the `identity` resource (`:5105`); on startup Weasel applies the `identity` schema (the `customers` table + unique index + Wolverine envelope tables). Rollback = remove the `identity` AppHost resource; no other service references Identity (no `WithReference` from the SPA, no consumer of its event), so removal is isolated. The spike branch `spike/efcore-identity` is retained as the reference implementation until this lands, then cleaned up as a separate step.

## Open Questions

*(none — the two guard sub-decisions, normalized-email and app-guard-plus-DB-index, were confirmed with the owner before authoring; the EF-over-Wolverine wiring and the column-casing reconciliation were settled and live-verified by the spike.)*
