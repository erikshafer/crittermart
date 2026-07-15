---
version: v1.10
status: Active
date: 2026-07-14
references:
  - docs/vision.md
  - docs/context-map/README.md
  - docs/workshops/001-crittermart-event-model.md
  - docs/workshops/002-identity-event-model.md
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
  - docs/decisions/022-convention-sagas-additive-to-pmvh.md
  - docs/decisions/023-real-authentication-for-identity.md
  - docs/decisions/024-dcb-coupon-redemption-in-orders.md
  - CLAUDE.md
---

# CritterMart — Structural Constraints (Round One)

This file is the AI session-runner's orientation surface: a flat imperative list of CritterMart's round-one structural constraints, readable in seconds. It is not an ADR; it carries no rationale prose. Each rule ends in a parenthetical cite — the cite is the rationale, by reference. When a new ADR lands or an existing constraint changes, this file gets a paired update in the same PR, and the entry below in Document History records the change.

## Service topology

- Four deployed services for round one: Catalog, Inventory, Orders, and Identity (an EF-Core-backed customer registry; not event-sourced). (ADR 001, ADR 009 second amendment)
- Each service is its own project with its own `Program.cs`, Marten configuration, and Wolverine.Http surface. (ADR 001)
- Handlers stay portable across deployment shapes; topology is a deployment decision, not a code decision. (ADR 001)
- .NET Aspire orchestrates the four services plus RabbitMQ and PostgreSQL via an `AspireHost` project. (ADR 004)

## Persistence

- One PostgreSQL database is shared across all services; each service writes to its own schema. (ADR 002)
- Per-service schemas are set via Marten's `opts.Schema.For<X>().DatabaseSchemaName(...)` and `opts.Events.DatabaseSchemaName`. (ADR 002)
- Catalog persists products in the Marten document store; no event sourcing. (vision.md § Bounded contexts)
- Inventory event-sources the Stock aggregate, one stream per SKU. (vision.md § Bounded contexts, Workshop 001 § 2)
- Orders event-sources both the Cart and the Order aggregates. (vision.md § Bounded contexts, Workshop 001 § 2)
- Identity persists customers as a plain EF-Core row per customer, in an `identity` schema; no stream, no projection, no fold. (ADR 009 second amendment, Workshop 002 § 2)
- Orders opts into Marten's DCB schema (`tags TEXT[]` column + GIN index on its `mt_events`) to enforce the **global per-coupon redemption cap**: redemption events are tagged by `CouponId`, and the cap is checked via `FetchForWritingByTags` across order streams (`DcbConcurrencyException` on the breaching race). DCB is store-scoped, so the cap lives in the Orders store; Promotions contributes coupon **definitions** only, and a standalone Promotions service is deferred. This is decided (ADR 024), not yet built. (ADR 024, ADR 002)

## Cross-service messaging

- Cross-service messaging is Wolverine over RabbitMQ for round one. (ADR 003)
- No synchronous service-to-service HTTP. (context map § Round-one stubs, ADR 001)
- Auth honors this: the JWT is validated OFFLINE against a config-distributed public key (no call into Identity), and where customer identity crosses a boundary over RabbitMQ (e.g. `ReserveStock`) it rides as message payload, not a token — no auth token travels on the bus. (ADR 023)
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

- Identity is a deployed EF-Core-backed customer registry service, sibling to Catalog/Inventory/Orders; it is NOT event-sourced (a plain row per customer). (ADR 009 second amendment, Workshop 002)
- **Auth is built (ADR 023, slices 5.8–5.11).** Identity holds real authentication via ASP.NET Core Identity — a relational user store that *extends* the boring-CRUD foil (no event sourcing) — and is the sole **auth issuer**. (ADR 023, ADR 022)
- Identity mints a self-validated, asymmetrically-signed **JWT** (customer id in the `sub` claim); resource servers verify it OFFLINE via `AddJwtBearer` against Identity's **public key distributed as config** — no HTTP into Identity, per-request or at startup (config, not a fetched JWKS). (ADR 023)
- No cookie auth and no BFF for cross-service trust; the JWT bearer is the credential the SPA sends to all four services. (ADR 023, ADR 006)
- The JWT `sub` claim is the **sole** customer trust boundary. The round-one `X-Customer-Id` header is fully retired (hard cutover, OpenSpec change `retire-x-customer-id-fallback`): no request header names a customer, Orders' customer-keyed endpoints are blanket-`[Authorize]`'d, and an unauthenticated request is `401`. (ADR 023, ADR 009 amendment)
- The four deployed services accept the customer-ID shape without translation (Conformist) — the JWT `sub` claim's string id end to end. (context map § Integration relationships, ADR 023)
- AuthZ (roles/policies) and refresh/revocation remain deferred. (ADR 023 Q15/Q16)
- Polecat is not used. (ADR 009)

## Aggregates and process managers

- The Order aggregate IS the process manager via PMvH; no separate saga state stream and no `Wolverine.Saga` base class for Order or Cart specifically — neither is a candidate for conversion to a convention saga. (ADR 007, ADR 022)
- The Order stream tracks progress with `StockReserved` and `PaymentAuthorized` state-flag events. (ADR 007)
- The Order stream terminates with either `OrderConfirmed` or `OrderCancelled`. (ADR 007)
- `OrderPaymentTimeout` is a Wolverine self-scheduled message, idempotent via state guards on the Order stream — not via Wolverine inbox dedup. (ADR 007)
- Process-manager handlers are pure functions, unit-testable without Wolverine or Marten. (ADR 007)
- Convention `Wolverine.Saga` is used additively elsewhere, never as a PMvH conversion and never re-implementing event sourcing on relational/document storage: Inventory's `Replenishment` (Marten-backed) is the first shipped instance. (ADR 022)

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
- Applied where a singular noun folder would otherwise collide with its aggregate, not as a blanket rule. This round, all three event-sourced aggregates are split: Cart's slice is `Shopping/` + the canonical `Cart` aggregate + `CartView` read model; Order's is `Ordering/Order` + the `OrderStatusView` read model (implementations/022); Stock's is `StockLevel` + the `StockLevelView` read model with no folder change (`StockLevel` ≠ `…Stock`, so no collision) (implementations/024). The ADR 020 rollout is complete. (ADR 020, ADR 021)

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
- No returns, no shipping rate calculations, no real-time storefront updates. **Promotions** now has a chosen DCB coupon-redemption increment realized inside Orders ([ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md)) — definitions-only, standalone service deferred; still design-only (no code yet). (vision.md § What this deliberately is not, context map § Round-one stubs)
- No live coding in the demo. (CLAUDE.md § Do Not — round one)
- No collapse back to a monolith without an explicit ADR reversing ADR 001. (CLAUDE.md § Do Not — round one)
- Long-road items (Polecat-backed Identity, Returns BC, async daemon coverage, separate BFF, multi-tenant scaffolding) are deferred; **Promotions with DCB is chosen** ([ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md)) — realized inside Orders (see Persistence), with the standalone Promotions service still deferred. (vision.md § Long road, context map § Long road)

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-05-26 | Initial round-one structural-constraints synthesis from ADRs 001–010, vision.md, context map, Workshop 001, and CLAUDE.md operating disciplines. |
| v1.1    | 2026-05-28 | Added the **Build & code generation** section (Critter Stack 2026 line; Wolverine Dynamic codegen via `WolverineFx.RuntimeCompilation`; Marten source-gen; v9 defaults adopted), paired with ADR 012. |
| v1.2    | 2026-05-31 | Added three **Cross-service messaging** rules for the published-language `CritterMart.Contracts` project (shared, both services reference it; not a service so no ADR 001 breach; owns wire messages only), paired with ADR 014 (slice 4.2's first cross-BC flow). |
| v1.3    | 2026-06-02 | Added three **SDD pipeline discipline** rules from the `tidy: encode` bundle: the tidy ceremony rule (spec-content tidy → full prompt/retro pair; mechanical tidy → may run light), one-capability-per-aggregate OpenSpec granularity, and workshop frontmatter version tracking. Paired with the matching CLAUDE.md additions (§ Operating Disciplines, § 4a). |
| v1.4    | 2026-06-16 | Added the **Aggregate and read-model naming** section (ADR 020 + ADR 021): aggregates are domain-named immutable `sealed record` write models (no `*View`/`*Aggregate` suffix, `this with` evolution), the raw aggregate is never served, a public read is a separate `*View` projection, and slice folders are named for the **activity** (verb — `Shopping/`, `Ordering/`) so aggregates keep canonical noun names. Piloted on Cart (`Shopping/` folder + canonical `Cart` aggregate + `CartView` read model); Order/Stock pending. |
| v1.5    | 2026-06-16 | **Order pilot landed** (implementations/022): the naming-rollout status ticks — Order split into the `Order` write aggregate (also the PMvH state) in an `Ordering/` verb folder + the `OrderStatusView` read model; only `StockLevelView` remains pending its pilot. No rule changed — the convention is unchanged; this records the rollout reaching Order. |
| v1.6    | 2026-06-16 | **Stock pilot landed** (implementations/024): the naming-rollout status closes — Stock split into the `StockLevel` write aggregate (carrying the reserve/release/commit idempotency state: `Available` + `Reservations`) + the `StockLevelView` read model, with no folder change (`StockLevel` ≠ `…Stock`). All three round-one event-sourced aggregates are now split; the ADR 020 rollout is complete. No rule changed — this records the rollout reaching Stock (and catches the frontmatter `version` up from the v1.5 entry, which had bumped the table but not the header). |
| v1.7    | 2026-07-02 | **Closes a pairing gap an independent design review (Fable 5) flagged during Saga #2 design:** ADR 022 (convention sagas additive to PMvH) and ADR 009's second amendment (Identity promoted to a deployed EF-Core service, slices 5.1–5.4) each shipped without this file's required paired update (this file's own header rule, line 28/121). No NEW constraint is introduced — this entry syncs the file to constraints that already changed. **Service topology:** three→four deployed services (Identity included). **Persistence:** added Identity's plain-EF-Core-row persistence line. **Identity section:** rewritten — Identity is a deployed registry service, not stubbed; still no authN/authZ. **Aggregates and process managers:** scoped the "no `Wolverine.Saga` base class" rule to Order/Cart specifically (it was never a repo-wide ban — ADR 022 makes convention sagas additive elsewhere) and named Inventory's `Replenishment` as the first shipped instance. |
| v1.8    | 2026-07-07 | **Paired with [ADR 023](../decisions/023-real-authentication-for-identity.md)** (real authentication for Identity), per this file's own header rule. **Identity section:** rewritten — auth is now decided (ADR 023), not deferred; Identity gains ASP.NET Core Identity (relational user store, extends the boring-CRUD foil) and becomes the sole auth **issuer** of a self-validated, asymmetrically-signed JWT that Catalog/Inventory/Orders verify OFFLINE via `AddJwtBearer` against a config-distributed public key (no HTTP into Identity; no cookie, no BFF). Current code still uses `X-Customer-Id` until the auth slices (Workshop 002 §§ 5.8–5.11) ship; the `sub` claim replaces it then. Conformist framing kept (header shape now, `sub` claim after). **Cross-service messaging:** added a line noting auth honors no-sync-HTTP (offline validation; identity crosses RabbitMQ as payload, not a token). The auth constraints bind the future slices; they describe a decided target, not yet-enforced code. |
| v1.10   | 2026-07-14 | **Identity section synced to shipped reality** (paired with OpenSpec change `retire-x-customer-id-fallback`): auth is **built** (slices 5.8–5.11 shipped 2026-07-07 — v1.8's "not yet built"/"until those slices ship" framing was never updated when they landed) **and** the hard cutover now retires the `X-Customer-Id` header entirely — the `sub` claim is the sole customer trust boundary, Orders' customer-keyed endpoints are blanket-`[Authorize]`'d, and an unauthenticated request is `401` (was `400` pre-auth). Conformist line updated (one `sub`-claim shape, no "header now / claim later" split); added the explicit AuthZ + refresh/revocation deferral line (ADR 023 Q15/Q16). |
| v1.9    | 2026-07-10 | **Paired with [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md)** (DCB-protected coupon redemption in Orders), per this file's own header rule. **Persistence:** added a line — Orders opts into Marten's DCB schema (`tags TEXT[]` + GIN index on its `mt_events`) to enforce the global per-coupon **redemption cap** via `FetchForWritingByTags` across order streams; DCB is store-scoped, so the cap lives in the Orders store and Promotions contributes coupon **definitions** only (standalone service deferred). **Round-one explicit deferrals:** retired "Promotions with DCB" from the deferred long-road list (now chosen) and forward-marked the "no promotions" non-goal line — both point at ADR 024 and note the increment is definitions-only inside Orders, still design-only (no code). No topology, no new service, no Polecat, no version bump — ADR 024 operates within ADRs 001/002/007/009/012. The DCB constraint binds the future Promotions slices; it describes a decided target, not yet-enforced code. |
