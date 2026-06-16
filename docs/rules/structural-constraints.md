---
version: v1.4
status: Active
date: 2026-06-16
references:
  - docs/vision.md
  - docs/context-map/README.md
  - docs/workshops/001-crittermart-event-model.md
  - docs/decisions/001-separate-services-topology.md
  - docs/decisions/002-shared-postgres-schema-per-service.md
  - docs/decisions/003-wolverine-rabbitmq-transport.md
  - docs/decisions/004-dotnet-aspire-orchestrator.md
  - docs/decisions/005-opentelemetry-tracing-enabled.md
  - docs/decisions/006-wolverine-http-per-service-no-bff.md
  - docs/decisions/007-process-manager-via-handlers-for-order.md
  - docs/decisions/008-inline-projections-async-teaser-no-daemon.md
  - docs/decisions/009-polecat-deferred-for-round-one.md
  - docs/decisions/010-openspec-narrative-sibling-pipeline.md
  - docs/decisions/012-critter-stack-2026-upgrade.md
  - docs/decisions/014-published-language-contracts-project.md
  - docs/decisions/020-domain-write-models-read-views.md
  - docs/decisions/021-verb-feature-folders.md
  - CLAUDE.md
---

# CritterMart — Structural Constraints (Round One)

This file is the AI session-runner's orientation surface: a flat imperative list of CritterMart's round-one structural constraints, readable in seconds. It is not an ADR; it carries no rationale prose. Each rule ends in a parenthetical cite — the cite is the rationale, by reference. When a new ADR lands or an existing constraint changes, this file gets a paired update in the same PR, and the entry below in Document History records the change.

## Service topology

- Three deployed services for round one: Catalog, Inventory, and Orders. (ADR 001)
- Each service is its own project with its own `Program.cs`, Marten configuration, and Wolverine.Http surface. (ADR 001)
- Handlers stay portable across deployment shapes; topology is a deployment decision, not a code decision. (ADR 001)
- .NET Aspire orchestrates the three services plus RabbitMQ and PostgreSQL via an `AspireHost` project. (ADR 004)

## Persistence

- One PostgreSQL database is shared across all services; each service writes to its own schema. (ADR 002)
- Per-service schemas are set via Marten's `opts.Schema.For<X>().DatabaseSchemaName(...)` and `opts.Events.DatabaseSchemaName`. (ADR 002)
- Catalog persists products in the Marten document store; no event sourcing. (vision.md § Bounded contexts)
- Inventory event-sources the Stock aggregate, one stream per SKU. (vision.md § Bounded contexts, Workshop 001 § 2)
- Orders event-sources both the Cart and the Order aggregates. (vision.md § Bounded contexts, Workshop 001 § 2)

## Cross-service messaging

- Cross-service messaging is Wolverine over RabbitMQ for round one. (ADR 003)
- No synchronous service-to-service HTTP. (context map § Round-one stubs, ADR 001)
- Handler code is portable across Wolverine transports; transport choice is configuration, not code. (ADR 003)
- Catalog has no BC-level integration with Inventory or Orders; product fields cross only via the frontend. (context map § Integration relationships)
- Cross-BC message contracts (the wire records both services exchange) live in the shared `CritterMart.Contracts` project, referenced by both services — the published language of the Orders↔Inventory Customer-Supplier relationship. (ADR 014)
- `CritterMart.Contracts` is not a service — no handlers, no `Program`, no persistence, no Wolverine/Marten dependency — so referencing it from both services does not breach "services do not reference each other's projects". (ADR 014, ADR 001)
- `Contracts` owns only the wire messages; each context maps an inbound message to its own stream event, and stream events do not leak into the shared project. (ADR 014)

## HTTP surface

- Each service exposes its own Wolverine.Http endpoints; the frontend calls each service directly. (ADR 006)
- No separate BFF project for round one. (ADR 006)

## Observability

- OpenTelemetry tracing is enabled in every service. (ADR 005)
- Marten OTel is configured with `TrackConnections = TrackLevel.Verbose` and `TrackEventCounters()`. (ADR 005)
- Wolverine OpenTelemetry instrumentation is enabled in every service. (ADR 005)
- Traces surface in the Aspire dashboard for round one. (ADR 005, ADR 004)

## Identity

- Identity is stubbed for round one; no deployed Identity service. (ADR 009, context map § Round-one stubs)
- A customer ID is hardcoded into the frontend and flows through commands as if from a real identity system. (ADR 009)
- The three deployed services accept the customer-ID shape from the frontend without translation (Conformist). (context map § Integration relationships)
- Polecat is not used for round one. (ADR 009)

## Aggregates and process managers

- The Order aggregate IS the process manager via PMvH; no separate saga state stream and no `Wolverine.Saga` base class. (ADR 007)
- The Order stream tracks progress with `StockReserved` and `PaymentAuthorized` state-flag events. (ADR 007)
- The Order stream terminates with either `OrderConfirmed` or `OrderCancelled`. (ADR 007)
- `OrderPaymentTimeout` is a Wolverine self-scheduled message, idempotent via state guards on the Order stream — not via Wolverine inbox dedup. (ADR 007)
- Process-manager handlers are pure functions, unit-testable without Wolverine or Marten. (ADR 007)

## Projection lifecycle

- All event-sourced aggregates use `SnapshotLifecycle.Inline`. (ADR 008)
- Exactly one async projection lives in the codebase as a teaser. (ADR 008)
- That async projection is `CartAbandonmentReport`. (Workshop 001 § 7)
- No async daemon is driven in the demo path for round one. (ADR 008, Workshop 001 § 7)

## Build & code generation

- The stack targets the Critter Stack 2026 line: Wolverine 6, Marten 9, JasperFx 2. (ADR 012)
- Wolverine runtime codegen runs in `TypeLoadMode.Dynamic` via the `WolverineFx.RuntimeCompilation` package for round one; Static/AOT (`codegen write` + `TypeLoadMode.Static`) is deferred. (ADR 012)
- Marten code generation is source-generated and automatic — no `Internal/Generated/` folder, no `dotnet run -- codegen write` for the Marten portion, no codegen-config knobs. (ADR 012)
- Marten 9's flipped `StoreOptions` defaults are adopted; `RestoreV8Defaults()` is not called. (ADR 012)

## Event naming

- Event names are past tense, carry no `Event` suffix, and are domain-meaningful. (Workshop 001 § 4)
- The Order stream's four load-bearing event names — `StockReserved`, `PaymentAuthorized`, `OrderConfirmed`, `OrderCancelled` — are immovable. (ADR 007, Workshop 001 § 4)
- Workshop 001 § 4 is the canonical naming source for downstream OpenSpec proposals, narratives, and code. (Workshop 001 § 4)

## Aggregate and read-model naming

- Aggregates are domain-named, `sealed`, immutable write models — no `*View`/`*Aggregate` technical suffix; state changes return a new instance via `this with { … }`. (ADR 020)
- The raw aggregate is never serialized over HTTP; a public read is a separate, purpose-built `*View` read model projected from the same events, created only when a read needs one. (ADR 020)
- The aggregate carries the write-side invariants (e.g. the open-cart partial-unique index); the read model carries only what its consumers query. (ADR 020)
- Feature/slice folders are named for the **activity** (a verb/gerund — `Shopping/`, `Ordering/`); domain types inside keep canonical **noun** names (`Cart`, `Order`). A verb namespace never collides with a noun type, so an aggregate needs no qualifying suffix. (ADR 021)
- Applied where a singular noun folder would otherwise collide with its aggregate, not as a blanket rule. This round: Cart's slice is `Shopping/` + the canonical `Cart` aggregate + `CartView` read model; Order follows with `Ordering/Order`; `OrderStatusView`/`StockLevelView` still double as aggregate + read model pending their pilots (`Stock`'s `StockLevel` ≠ `…Stock`, no folder change needed). (ADR 020, ADR 021)

## SDD pipeline discipline

- Each slice has both an OpenSpec proposal and a sibling narrative authored before its implementation prompt; both must agree. (ADR 010)
- One prompt equals one session equals one PR. (CLAUDE.md § Operating Disciplines)
- Session edits stay within the files the prompt's deliverable plan named; no opportunistic edits. (CLAUDE.md § Operating Disciplines)
- After 2–3 implementation PRs against one bounded context, the next PR is a workshop, narrative, or `tidy:` session. (CLAUDE.md § Design-return cadence)
- Every prompt names its spec delta in 2–4 lines; the retrospective confirms whether the delta landed. (CLAUDE.md § Spec-delta closure loop)
- Maintenance sessions use `tidy: <area> — <details>` commit subjects; artifact-producing sessions do not. (CLAUDE.md § Operating Disciplines)
- Design intent and decisions live in version-controlled artifacts, not chat windows or tickets. (CLAUDE.md § Operating Disciplines)
- Vision-doc updates are deliberate, never opportunistic. (CLAUDE.md § Routing Layer)
- A new bounded context requires a paired context-map update and workshop pass. (CLAUDE.md § Do Not — round one)
- A future ADR that changes a structural constraint pairs with a rule-file update in the same PR. (this file, header)
- A tidy that authors spec content carries the full prompt/retro pair; a purely mechanical tidy may run light. (CLAUDE.md § Operating Disciplines, retros docs/007–010)
- One OpenSpec capability per aggregate or document type, not per bounded context. (CLAUDE.md § 4a; Orders = `shopping-cart` + `order-lifecycle`)
- Workshop frontmatter `version:` tracks the workshop's Document History; both bump in every amendment. (docs/workshops/README.md, retro docs/010)

## Round-one explicit deferrals

- No vendor portal, vendor identity, marketplace listings, or multi-channel sales. (vision.md § What this deliberately is not)
- No backoffice or admin UI. (vision.md § What this deliberately is not)
- No real payment integration; payment is stubbed inside Orders. (vision.md § What this deliberately is not, context map § Round-one stubs)
- No returns, no promotions, no shipping rate calculations, no real-time storefront updates. (vision.md § What this deliberately is not)
- No live coding in the demo. (CLAUDE.md § Do Not — round one)
- No collapse back to a monolith without an explicit ADR reversing ADR 001. (CLAUDE.md § Do Not — round one)
- Long-road items (Polecat-backed Identity, Returns BC, Promotions with DCB, async daemon coverage, separate BFF, multi-tenant scaffolding) are deferred. (vision.md § Long road, context map § Long road)

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-05-26 | Initial round-one structural-constraints synthesis from ADRs 001–010, vision.md, context map, Workshop 001, and CLAUDE.md operating disciplines. |
| v1.1    | 2026-05-28 | Added the **Build & code generation** section (Critter Stack 2026 line; Wolverine Dynamic codegen via `WolverineFx.RuntimeCompilation`; Marten source-gen; v9 defaults adopted), paired with ADR 012. |
| v1.2    | 2026-05-31 | Added three **Cross-service messaging** rules for the published-language `CritterMart.Contracts` project (shared, both services reference it; not a service so no ADR 001 breach; owns wire messages only), paired with ADR 014 (slice 4.2's first cross-BC flow). |
| v1.3    | 2026-06-02 | Added three **SDD pipeline discipline** rules from the `tidy: encode` bundle: the tidy ceremony rule (spec-content tidy → full prompt/retro pair; mechanical tidy → may run light), one-capability-per-aggregate OpenSpec granularity, and workshop frontmatter version tracking. Paired with the matching CLAUDE.md additions (§ Operating Disciplines, § 4a). |
| v1.4    | 2026-06-16 | Added the **Aggregate and read-model naming** section (ADR 020 + ADR 021): aggregates are domain-named immutable `sealed record` write models (no `*View`/`*Aggregate` suffix, `this with` evolution), the raw aggregate is never served, a public read is a separate `*View` projection, and slice folders are named for the **activity** (verb — `Shopping/`, `Ordering/`) so aggregates keep canonical noun names. Piloted on Cart (`Shopping/` folder + canonical `Cart` aggregate + `CartView` read model); Order/Stock pending. |
