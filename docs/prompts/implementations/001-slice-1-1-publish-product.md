# Prompt: Implementations 001 — Slice 1.1 PublishProduct (Catalog skeleton + first slice)

**Kind**: per-slice implementation (Catalog service skeleton + first vertical slice)
**Files touched**: `docs/prompts/implementations/001-slice-1-1-publish-product.md` (new, this file — bootstraps the `implementations/` kind); `openspec/changes/slice-1-1-publish-product/design.md` + `tasks.md` (new); `src/CritterMart.Catalog/**` (new project); `tests/CritterMart.Catalog.Tests/**` (new project); `CritterMart.slnx` (add the two projects); `Directory.Packages.props` (add package versions only if needed — see deliverable plan); `docker-compose.yml` (new, local Postgres); `docs/retrospectives/implementations/001-slice-1-1-publish-product.md` (forthcoming, authored at session close)
**Mode**: solo implementation with tool-backed openspec scaffolding (openspec CLI 1.3.1); current-docs verification via `ctx7` before writing framework code
**Commit subject(s)**: `feat: bootstrap Catalog service skeleton` + `feat: implement slice 1.1 PublishProduct` (bundled in one PR per the **"Skeleton + first slice"** exception; the design/tasks/prompt artifacts ride under a `docs:` subject)

## Framing

This is the **third and final edge** of the slice 1.1 triangle (narrative → OpenSpec proposal → **implementation prompt**) and the **first implementation PR** in the project — the first code under `src/`. The design phase is complete and merged (PRs #4–#7): Narrative 001, the openspec `slice-1-1-publish-product` change (`proposal.md` + `specs/product-catalog/spec.md`, passing `openspec validate --strict`), ADR 011, and the decisions README.

Slice 1.1 is CritterMart's **blueprint-architecture step**: build one vertical slice by hand — skeleton and all — before the per-slice loop is turned loose. Catalog is deliberately the *"when CRUD is fine"* teaching example: a Marten **document store**, where `Product` is the source of truth and `ProductPublished` is an **audit-only lifecycle moment**, not state-reconstruction material. Catalog has **no cross-BC integration** in round one (no RabbitMQ, no outbound messages) — so this slice is isolated and small, which is exactly why it is first.

Two session-shaping decisions were settled before this prompt was frozen and are recorded here as intent:

1. **Lean skeleton; Aspire deferred.** This PR stands up the Catalog service + Testcontainers-backed integration tests + a `docker-compose` Postgres for local runs. The `.NET Aspire` AppHost (`src/CritterMart.AppHost`) is **deliberately deferred** to a later dedicated infra/orchestration session, once a second service exists to orchestrate. This is a *sequencing* choice, not a reversal of ADR 004 — `docs/rules/structural-constraints.md` still names Aspire as the round-one orchestrator; it simply isn't realized yet. No rule-file edit is warranted.
2. **`ProductPublished` lives on a per-product Marten event stream.** The audit trail is an append-only event stream keyed per product, in the *same* Catalog Marten store as the `Product` document. This is the pedagogical beat: *even a CRUD service can keep an event-sourced audit log without becoming event-sourced for state.* The `Product` document remains the source of truth; no projection reconstructs `Product` from events.

Per **ADR 011**, this session is the **first real exercise of the grain-aware layered model**. ADR 011 asserts (untested until now) that `design.md` coexists with ADRs at a different grain (change-local technical approach vs. cross-change decisions) and `tasks.md` coexists with this implementation prompt (live execution checklist vs. frozen session intent). If authoring those two artifacts contradicts ADR 011, **amend ADR 011** (and bump `docs/decisions/README.md` if its status changes) — the retrospective-to-design feedback edge.

**Version note — read before writing framework code.** `Directory.Packages.props` pins **Wolverine 5.39.3** (the 2025 Critter Stack line), *not* Wolverine 6 / Marten 9. Marten arrives transitively via `WolverineFx.Marten`. Target v5-era / Marten-8-era APIs; the `*-migration-v5-to-v6` and `*-v8-to-v9` skills do **not** apply. Verify current API specifics with `ctx7` (per the global rule) before relying on training data.

## Goal

Ship a running Catalog service that satisfies the `product-catalog` capability's two requirements (publish a product; SKUs are unique), proven by integration tests over both Workshop 001 § 6.1 GWT scenarios, with the openspec change's `design.md` + `tasks.md` authored and `openspec validate --strict` still green.

Concretely:

- A `src/CritterMart.Catalog` service: Wolverine.Http surface, Marten document **and** event store on the shared Postgres under a Catalog-owned schema (ADR 002).
- `PublishProduct` end-to-end: command → handler → `Product` document persisted → `ProductPublished` appended to a per-product event stream → surfaced through the `ProductCatalogView` read shape.
- Duplicate-SKU publish rejected with `ProductAlreadyPublished`, **idempotently**: no second document, no second stream/event, existing document untouched.
- Both GWT scenarios green under Alba + Testcontainers; the service verified to actually start and serve `PublishProduct` (not just tests passing).

## Spec delta

This session closes the slice 1.1 triangle: the `product-catalog` capability (publish; SKU uniqueness) gains its first satisfying implementation under `src/`, and the openspec change gains its two deferred artifacts (`design.md` + `tasks.md`), making `slice-1-1-publish-product` a complete four-artifact change ready to `openspec archive` in a future session. No new requirement or scenario is added — the proposal and `specs/product-catalog/spec.md` are unchanged; this session *satisfies* the existing contract rather than extending it. New ADR cross-reference: this session is ADR 011's first exercise and either confirms or amends it. Narrative 001 and Workshop 001 § 6.1 remain the source of truth and are **not** edited.

## Orientation

Read these in this order before writing anything:

1. **`openspec/changes/slice-1-1-publish-product/proposal.md`** + **`specs/product-catalog/spec.md`** — the machine-readable contract the code must satisfy. Two requirements, two scenarios.
2. **`docs/narratives/001-seller-manage-catalog.md`** — the human-readable sibling; Moment 1 (publish happy path) and Moment 2 (duplicate-SKU rejection). Code and tests must agree with both it and the proposal.
3. **`docs/workshops/001-crittermart-event-model.md`** — § 2 (Catalog BC: document store; `ProductPublished` is audit, not state-reconstruction; `Product` is source of truth), § 4 Catalog vocabulary (`ProductPublished`), § 5 slice 1.1 row (command/event/view/reads-from/writes-to), § 6.1 (the two authoritative GWT scenarios, anchor data `crit-001` / "Cosmic Critter Plush" / `24.99`).
4. **`docs/decisions/011-openspec-cli-grain-aware-layered-integration.md`** — governs how `design.md` / `tasks.md` are authored this session. Treat its grain-aware layered model as a hypothesis to validate.
5. **`docs/rules/structural-constraints.md`** — the flat imperative list. Note: schema-per-service (ADR 002), Wolverine.Http per service / no BFF (ADR 006), inline projections / no async daemon (ADR 008), Identity stubbed (ADR 009), OTel in every service (ADR 005).
6. Supporting ADRs: **002** (shared Postgres, schema-per-service), **006** (Wolverine.Http, no BFF), **008** (inline projections, no daemon), **009** (Identity stubbed). For the OTel tension, **005**.
7. **`Directory.Packages.props`** + **`Directory.Build.props`** + **`CritterMart.slnx`** — the pinned versions, target framework (net10.0 / C# 14), and the `CritterMart.<Service>` naming convention the new projects follow.
8. **openspec CLI instructions** — `openspec instructions design --change slice-1-1-publish-product` and `... tasks ...` supply the templates; follow them as the authoring contract.
9. **Skills** (defer to the upstream JasperFx library for mechanics; these are the relevant ones): `wolverine-http-fundamentals`, `wolverine-handlers-fundamentals`, `wolverine-handlers-declarative-persistence` (Marten **document** persistence — *not* event-sourcing), `marten-integration-testing`, `wolverine-testing-alba`, `wolverine-testing-with-testcontainers`. Use `ctx7`/`find-docs` to verify current API.

## Working pattern

1. **Author `design.md`** via `openspec instructions design`. Keep it change-local and **reference** cross-cutting ADRs rather than restating them (ADR 011's grain rule). It must record the genuine technical decisions this slice forces:
   - `Product` document is source of truth; `ProductCatalogView` is a **read/query shape over `Product` documents**, served synchronously — *not* a Marten `IProjection` over the event stream. (Resolves the "inline projection" loose wording in the proposal/ADR 008 — ADR 008's inline rule is scoped to event-sourced aggregates, which Catalog is not.)
   - `ProductPublished` is appended to a **per-product Marten event stream** in the Catalog store as an audit log; nothing reconstructs `Product` from it.
   - Duplicate-SKU rejection mechanism (e.g., a SKU-unique check / `IndexProductBySku` and a railway-style `ProblemDetails` or typed failure on the HTTP path) — name the choice and why; the failure is idempotent.
   - **OTel deferral**: ADR 005 wants OTel in every service, but the OpenTelemetry SDK + exporter is deferred with Aspire. Decide and record: set the cheap Marten/Wolverine instrumentation flags now (no new packages) vs. defer the whole OTel story to the Aspire session. Whichever — record it as a deliberate deferral so the constraint is not silently dropped; flag it in the retro.
2. **Author `tasks.md`** via `openspec instructions tasks` — the live, mutable implementation checklist (checkbox format the apply phase parses). It coexists with this frozen prompt: execution vs. intent.
3. **Verify current API** with `ctx7` for Wolverine 5.39.3 (HTTP endpoints, handler discovery) and the transitive Marten (document `Store`, `Events.StartStream`, session lifetimes) before writing.
4. **Build the skeleton**: `src/CritterMart.Catalog` (Program.cs, Marten document+event store with `DatabaseSchemaName`/`Events.DatabaseSchemaName` = catalog schema per ADR 002, Wolverine + Wolverine.Http), `docker-compose.yml` for local Postgres, add both projects to `CritterMart.slnx`.
5. **Implement `PublishProduct`** end-to-end per the design decisions above.
6. **Write the two integration tests** (`tests/CritterMart.Catalog.Tests`, Alba + Testcontainers.PostgreSql + xUnit + Shouldly) — one per GWT scenario, quote-identical anchor data. Clean Marten data between runs (`CleanAllMartenDataAsync` or equivalent).
7. **Verify**: `dotnet build` + `dotnet test` green; **run the service** and exercise `PublishProduct` end-to-end (happy + duplicate); `openspec validate slice-1-1-publish-product --strict` still passes.
8. **Author the retrospective** at session close: spec-delta closure (forward-confirm: the contract is *satisfied*, nothing added), and the **ADR 011 verdict** — held, or amend it. Update the `next-pickup-slice-1-1` memory to point at the next work (slice 1.2 / next BC).

## Deliverable plan

| Deliverable | Path | Notes |
| --- | --- | --- |
| This prompt | `docs/prompts/implementations/001-slice-1-1-publish-product.md` | Bootstraps the `implementations/` kind |
| Design doc | `openspec/changes/slice-1-1-publish-product/design.md` | Grain-aware, references ADRs |
| Task checklist | `openspec/changes/slice-1-1-publish-product/tasks.md` | Live checkbox list |
| Catalog service | `src/CritterMart.Catalog/` | Program.cs, Marten config, `PublishProduct` command/handler/endpoint, `Product`, `ProductPublished`, `ProductCatalogView` |
| Test project | `tests/CritterMart.Catalog.Tests/` | Alba + Testcontainers; two GWT scenarios |
| Solution wiring | `CritterMart.slnx` | Add both projects |
| Package versions | `Directory.Packages.props` | Add **only if needed** (e.g., a direct `Marten` pin, `WolverineFx.Http.Marten` for `[Document]` loading); prefer the existing pins |
| Local Postgres | `docker-compose.yml` | Single Postgres for `dotnet run` |
| Retrospective | `docs/retrospectives/implementations/001-slice-1-1-publish-product.md` | Authored at session close |

## Out of scope

- **No Aspire AppHost** (`src/CritterMart.AppHost`). Deferred to a dedicated infra session — see Framing decision 1.
- **No RabbitMQ / no cross-BC messaging.** Catalog is isolated in round one; publishing fires no message to Orders or Inventory.
- **No Inventory or Orders code.** This slice is Catalog-only.
- **No frontend / back-office UI.** Frontend is TBD (CLAUDE.md tech stack; vision § "what this deliberately is not"). The narrative's "back-office form" is illustrative; this slice delivers the API + persistence + tests only.
- **No slice 1.2 (browse) or 1.3 (change price).** `ProductCatalogView` is built only as far as slice 1.1 needs (so the published product is observable in tests); a `GET /products` browse endpoint belongs to slice 1.2.
- **No `openspec archive`.** Archiving the change (syncing the delta into `openspec/specs/`) is a separate, deliberate step for a future session.
- **No `/opsx:propose` or `/opsx:apply`.** Drive openspec manually per ADR 011's one-artifact-class-per-session convention; `tasks.md` is authored and worked by hand this session.
- **No edits to Workshop 001 or Narrative 001.** They are the source of truth. If the code surfaces a contradiction, **stop and raise it** rather than editing them silently.
- **No opportunistic docs edits.** The known loose end — the missing `specs/` kind on the *Current population* lines of `docs/prompts/README.md` and `docs/retrospectives/README.md` — stays **out of scope** here; it is a `tidy: docs` concern, not part of this implementation deliverable. (Authoring the `implementations/` and `retrospectives/implementations/` population lines for the *new* kind this session creates is in-bounds, since those READMEs index the dirs this session brings into existence — but defer it to the retro/PR step and keep it minimal.)
- **No OpenTelemetry SDK/exporter wiring** beyond the cheap instrumentation-flag decision recorded in `design.md` — deferred with Aspire per Framing decision 1 and the OTel deferral note.
