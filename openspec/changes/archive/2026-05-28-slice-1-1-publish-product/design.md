## Context

Slice 1.1 stands up the Catalog service and its first command, `PublishProduct`. Catalog is CritterMart's *"when CRUD is fine"* example: a Marten **document store**, not an event-sourced aggregate. Workshop 001 § 2 is emphatic — the `Product` document is the source of truth; `ProductPublished` is an audit-only lifecycle moment, **not** state-reconstruction material.

This is also the **first code under `src/`** and the blueprint-architecture step (build one slice by hand before the per-slice loop runs). The cross-cutting decisions are already fixed by ADRs and need only be *referenced* here, per ADR 011's grain-aware layered model:

- Shared Postgres, schema-per-service — **ADR 002**.
- Wolverine.Http per service, no BFF — **ADR 006**.
- Inline projections, no async daemon — **ADR 008** (scoped to event-sourced aggregates).
- Identity stubbed — **ADR 009**.
- OTel in every service — **ADR 005**.

What remains are the *change-local* technical choices this slice forces. They are recorded below.

## Goals / Non-Goals

**Goals:**

- `PublishProduct` end-to-end: command → handler → `Product` document persisted → `ProductPublished` appended to a per-product event stream → product observable through the `ProductCatalogView` read shape.
- Duplicate-SKU publish rejected with `ProductAlreadyPublished`, idempotently (no second document, no second event, existing document untouched).
- A lean, runnable Catalog service skeleton other Catalog slices (1.2 browse, 1.3 change price) extend without rework.

**Non-Goals:**

- No `.NET Aspire` AppHost, no RabbitMQ, no cross-BC messaging (Catalog is isolated in round one).
- No `ProductCatalogView` browse endpoint (that is slice 1.2); the view exists only far enough for slice 1.1 to observe the published product.
- No state reconstruction of `Product` from events. The event stream is an audit log, full stop.
- No OpenTelemetry SDK/exporter wiring (deferred with Aspire — see Decisions).

## Decisions

### 1. `Product` document is the source of truth; `ProductCatalogView` is a query, not a Marten projection

`Product` is persisted via the Marten document store. `ProductCatalogView` is a **read/query shape over `Product` documents**, served synchronously — *not* a Marten `IProjection` over the event stream.

*Why X over Y:* ADR 008's inline-projection rule is scoped to event-sourced aggregates (Cart/Order/Stock); Catalog is not one. Building `ProductCatalogView` as a projection over `ProductPublished` would event-source a CRUD service and contradict Workshop 001 § 2 (document is source of truth). In a document store the document *is* the read model. *Alternative considered:* a Marten single-stream projection rebuilding `Product`/the view from events — rejected as contradicting the workshop and over-engineering the "CRUD is fine" example.

### 2. `ProductPublished` is appended to a per-product Marten event stream as an audit log

In the same Catalog Marten store, `ProductPublished` is appended to an event stream keyed per product. Nothing reconstructs `Product` from this stream; it is a durable, append-only audit fact.

*Why X over Y:* This is the slice's teaching beat — *even a CRUD service can keep an event-sourced audit log without becoming event-sourced for state.* Marten hosts the document store and event store in one store/session, so the document write and the event append commit in **one transaction** (`IDocumentSession.SaveChangesAsync`). *Alternatives considered (and rejected by the prompt's locked decision):* an append-only collection embedded on the `Product` document; a standalone audit document. Both lose the "document store can also use the event store" beat and give no independent stream to demo a rebuild/audit-query against.

### 3. SKU is the natural identity; uniqueness is enforced by document identity

The `Product` document's identity **is** the SKU (`Id = sku`, a string). The per-product event stream uses the same SKU as its stream key. Duplicate-SKU detection is a load-by-id existence check before publish.

*Why X over Y:* SKUs are immutable domain identifiers (Workshop 001), so a natural key is sound and the simplest correct thing. Document identity gives uniqueness for free and makes the stream key fall out naturally (document id == stream key == SKU). The existence check makes the failure idempotent without a second mechanism. *Alternative considered:* a surrogate Guid id plus a unique index / `IndexProductBySku` on a `Sku` field — rejected as extra machinery for a single-key domain; revisit only if a non-SKU identity need appears.

### 4. Duplicate-SKU rejection flows as ProblemDetails on the HTTP path

On a duplicate SKU the handler does **not** throw; it returns a `ProblemDetails` (railway-style flow control) describing `ProductAlreadyPublished`, naming the existing product. No document is stored, no stream started, no event appended.

*Why X over Y:* Wolverine.Http treats a returned `ProblemDetails` as flow control, short-circuiting the success path cleanly — idiomatic and testable without exception handling. *Alternative considered:* throwing a domain exception mapped to a 409 — rejected; exceptions are control-flow noise for an expected, modeled outcome.

### 5. Identity is a stubbed seller flowing through the command

`PublishProduct` carries the acting seller/operator id as if from a real identity system (ADR 009); a hardcoded single-seller constant supplies it for round one. The id is recorded on `ProductPublished` for audit.

*Why:* ADR 009 — no deployed Identity service in round one; commands carry the actor shape so the audit trail is real even though the source is stubbed.

### 6. OpenTelemetry SDK/exporter deferred with Aspire

ADR 005 requires OTel in every service, but the OpenTelemetry SDK + exporter packages are **not** in `Directory.Packages.props` and there is no Aspire dashboard to receive traces this session. Decision: **defer the whole OTel wiring** (SDK registration, Marten `TrackLevel.Verbose` + `TrackEventCounters`, Wolverine OTel, exporter) to the dedicated Aspire/observability session. Do not add OTel packages now.

*Why X over Y:* Wiring exporters with no collector/dashboard is inert and pulls in packages the lean-skeleton decision deliberately excludes. The Marten/Wolverine instrumentation flags only emit when the SDK is registered, so setting them in isolation buys nothing. This is a **deliberate, temporary deferral of an ADR-005 constraint**, recorded here and surfaced in the retrospective so it is not silently dropped. *Alternative considered:* set the Marten/Wolverine flags now and add only the OTel SDK — rejected as half-wiring with nowhere for traces to go.

## Risks / Trade-offs

- **ADR-005 OTel constraint temporarily unmet** → tracked in the retrospective and closed by the Aspire/observability session; the constraint remains the round-one target in `docs/rules/structural-constraints.md`.
- **Document identity coupled to a business key (SKU)** → acceptable because SKUs are immutable domain identifiers; if a future slice needs mutable identity, migrate to a surrogate id + unique index (mechanical).
- **Document store + event store in one Marten store may read as "event-sourced"** → mitigated by Decision 1/2 and the workshop rule; `design.md` and code comments (where load-bearing) make "document is source of truth" explicit.
- **Capability granularity (one `product-catalog` capability)** → inherited open item from the proposal retro; revisit when slices 1.2/1.3 land. No action this slice.

## Open Questions

- Does `ProductCatalogView` eventually become a *stored* read model (its own document maintained on write) rather than a live query? Slice 1.2 (browse) decides, when read shape and query volume are real. Not this slice.
- Whether the per-product audit stream gains later events (`ProductPriceChanged`, slice 1.3; `ProductDiscontinued`, parked) on the *same* stream — likely yes; confirmed when 1.3 is authored.
