---
retrospective: 001
kind: implementations
prompt: docs/prompts/implementations/001-slice-1-1-publish-product.md
deliverable: src/CritterMart.Catalog/** (new service); tests/CritterMart.Catalog.Tests/** (new); openspec/changes/slice-1-1-publish-product/{design.md, tasks.md} (new); CritterMart.slnx (+2 projects); docker-compose.yml (new); docs/prompts/implementations/001-slice-1-1-publish-product.md (new); docs/prompts/README.md + docs/retrospectives/README.md (implementations/ population line); docs/retrospectives/implementations/001-slice-1-1-publish-product.md (this file)
date: 2026-05-28
mode: solo implementation with tool-backed openspec scaffolding (CLI 1.3.1) and ctx7 docs verification
session-runner: Claude (Opus 4.7)
---

# Retrospective — Implementations 001: Slice 1.1 PublishProduct (Catalog skeleton + first slice)

## Outcome summary

The **third and final edge** of the slice 1.1 triangle landed, and with it the **first code under `src/`**. The session authored the implementation prompt (bootstrapping the `implementations/` kind), the two deferred openspec artifacts (`design.md` + `tasks.md`, completing the change to all four artifacts), and built a runnable Catalog service that satisfies the `product-catalog` capability's two requirements:

- `src/CritterMart.Catalog` — Wolverine.Http surface, Marten document **and** event store on the shared Postgres under a `catalog` schema (ADR 002). `PublishProduct` → `Product` document persisted + `ProductPublished` appended to a per-product (SKU-keyed) event stream, committed in one transaction via `AutoApplyTransactions`. Duplicate SKU → `ProductAlreadyPublished` ProblemDetails (409), idempotent by construction (the railway guard short-circuits before any write).
- `tests/CritterMart.Catalog.Tests` — Alba + Testcontainers.PostgreSql, both Workshop 001 § 6.1 GWT scenarios green (happy publish; duplicate-SKU rejection), quote-identical anchor data (`crit-001` / "Cosmic Critter Plush" / `24.99`).
- Verified beyond tests: the service was run against the `docker-compose` Postgres and exercised over real HTTP — `POST /products` → `201 {"url":"/products/crit-001"}`, re-`POST` → `409` with an RFC-9110 `problem+json` body.

`openspec validate slice-1-1-publish-product --strict` passes; the full solution builds.

## What worked

- **Two scope-shaping questions up front, before freezing the prompt.** The lean-skeleton-vs-Aspire and audit-trail-mechanism decisions were settled by the user *before* the prompt was written, so the frozen prompt was actually right rather than right-ish. Asking before freezing (not after) kept the prompt honest to the one-prompt-one-session discipline.
- **Recon the pinned versions before writing code.** Reading `Directory.Packages.props` surfaced that the project is on **Wolverine 5.39.3 / Marten 8.35.0**, not the Wolverine 6 / Marten 9 the handoff's skill list implied. This redirected the whole implementation to v5/v8-era APIs and made the `*-migration-v6`/`*-v9` skills correctly irrelevant.
- **ctx7 docs verification caught a real namespace move.** `StreamIdentity` lives in `JasperFx.Events`, not `Marten.Events`, in the Marten 8.x line (event abstractions already extracted to JasperFx pre-Marten-9). The build error pinpointed it; a package-cache grep confirmed the namespace in one shot.
- **Railway-style ProblemDetails guard.** Splitting the two GWT scenarios across Wolverine's `ValidateAsync` (duplicate track) and `Post` (happy track) made idempotency *structural*: the guard short-circuits before the transactional middleware, so no code path can write on the duplicate track. The 409 `problem+json` came back correctly with zero manual content-negotiation.
- **Real-run verification, not just green tests.** Booting the service against docker-compose and curling both paths validated what Alba's in-memory TestServer can't: `docker-compose.yml`, Kestrel on a socket, and `ApplyAllDatabaseChangesOnStartup` creating the `catalog` schema against a fresh database.

## What was harder than expected

- **"Inline projection" in the proposal was a grain-mismatched wording.** The proposal's Impact line says "inline `ProductCatalogView` projection," but ADR 008's inline rule is scoped to *event-sourced aggregates* — and Catalog is a document store where the document *is* the read model. `design.md` Decision 1 resolves it: `ProductCatalogView` is a query/read shape over `Product` documents, not a Marten `IProjection`. The proposal was left unedited (it is a frozen, merged contract; `proposal.md` prose is not synced into `openspec/specs/` on archive — only the `specs/` delta is, and that delta never says "projection" — so the wording is harmless and stays as a historical artifact).
- **Schema auto-creation reliability across hosting contexts.** ASP.NET defaults to `Production` when `ASPNETCORE_ENVIRONMENT` is unset, where Marten's default `AutoCreate` is `None`. Rather than guess the `AutoCreate` enum's (also-relocated) namespace, `ApplyAllDatabaseChangesOnStartup()` was used — it forces schema application at startup independent of environment, sidestepping both problems.
- **Connection-string injection into the Alba host.** To avoid guessing Alba 8's builder-callback overload signature, the test fixture injects the Testcontainer connection string via the environment-variable config provider (`ConnectionStrings__crittermart`), which `WebApplicationBuilder` adds after `appsettings.json` so it wins. Robust and signature-agnostic.

## Methodology refinements that emerged

1. **Pin-version recon belongs in the orientation pass for every implementation session.** Handoff/skill suggestions can lag the actual pinned versions. Reading `Directory.Packages.props` first (and `dotnet list package --include-transitive` to see what transitively resolves) should be a standard early move before any framework code — it changes which APIs and skills apply.
2. **ctx7-before-framework-code paid for itself.** Per the global rule, fetching current docs (not relying on training data) caught the `JasperFx.Events` namespace and confirmed the Wolverine.Http `Validate`/`ValidateAsync` ProblemDetails convention before the first build. Cheaper than build-error archaeology.
3. **Verify the running app, not just tests, even for an API-only slice.** "Tests pass" and "I started it and it served a real request" are different claims. The docker-compose run is the honest second claim and the blueprint slice is the right place to establish the habit.
4. **`design.md` is the right home for resolving grain-mismatched wording in upstream artifacts.** Rather than edit a frozen proposal, the change-local `design.md` records the corrected interpretation and cites *why* — exactly the grain ADR 011 predicted.

## Outstanding items / next-session inputs

1. **Marten 8.35.0 carries a critical-severity advisory (NU1904, GHSA-vmw2-qwm8-x84c).** It is a *transitive* pin from `WolverineFx.Marten 5.39.3`. Resolving it is a package-line decision (bump the Wolverine/Marten line, or accept-with-rationale for round one) — out of scope for this slice, but it should be decided deliberately, likely in the Aspire/observability infra session or a dedicated `chore`/`tidy` PR. **Flagged, not silently absorbed.**
2. **OpenTelemetry is deferred (ADR 005 constraint temporarily unmet).** Per `design.md` Decision 6, the OTel SDK + exporter + Marten/Wolverine instrumentation flags are deferred with Aspire. The constraint remains the round-one target in `docs/rules/structural-constraints.md`; it closes when the Aspire/observability session lands. **Flagged.**
3. **Aspire AppHost deferred.** `src/CritterMart.AppHost` was intentionally not built (lean-skeleton decision). A dedicated infra/orchestration session stands it up once a second service exists. This does not reverse ADR 004; the rule file is unchanged.
4. **`specs/` kind still missing from the README *Current population* lines.** This session added the `implementations/` line (in-bounds, new kind) but left the pre-existing `specs/` gap per the prompt's out-of-scope. Fold into a `tidy: docs` sweep with any other README reconciliation.
5. **Capability-granularity revisit.** The one-`product-catalog`-capability choice (inherited from the proposal retro) holds for slice 1.1; revisit when 1.2 (browse) and 1.3 (change price) land.
6. **Design-return cadence reset.** This is the **first implementation PR** for the Catalog BC. After 2–3 Catalog implementation PRs, the next PR must be a design-return (next BC workshop, a new narrative, or a `tidy:` session).
7. **Archiving the change is deferred.** `openspec archive slice-1-1-publish-product` (syncing the `specs/` delta into `openspec/specs/product-catalog/`) is a separate, deliberate step for a future session — not done here.

## ADR 011 verdict — grain-aware layered model

**Held. No amendment required.** This session was ADR 011's first real exercise, and the grain-aware layered model behaved exactly as predicted:

- `design.md` coexisted with ADRs at a **different grain** — it referenced ADRs 002/005/006/008/009/011 rather than restating them, and recorded only the *change-local* technical decisions (document-as-source-of-truth; per-product audit stream; SKU-as-identity; ProblemDetails rejection; OTel deferral). No competition with the cross-change ADRs.
- `tasks.md` coexisted with the frozen implementation prompt as **execution vs. intent** — the prompt named scope/orientation/out-of-scope and froze; `tasks.md` was the live checkbox list. No duplication.
- `design.md` **earned its place** (it was not skipped as trivial): the document-vs-projection and audit-mechanism decisions were genuine. This validates ADR 011's "skip for trivial slices, author when there are real decisions" clause from the *author* side.

ADR 011's status is unchanged (Accepted); `docs/decisions/README.md` needs no bump.

## Spec-delta — landed?

**Yes (forward-confirmed).** The prompt named a *satisfaction* delta, not an extension:

1. The `product-catalog` capability gains its first satisfying implementation under `src/` — **landed**; both requirements proven by integration tests and a real run.
2. The openspec change gains `design.md` + `tasks.md`, completing all four artifacts — **landed**; `openspec validate --strict` passes.
3. ADR 011 is exercised and confirmed-or-amended — **landed** as confirmed (held; see verdict above).
4. No new requirement or scenario; `proposal.md` and `specs/product-catalog/spec.md` unchanged; Narrative 001 and Workshop 001 unedited — **honored**.

No spec-delta items dropped.

## Process notes

- One PR bundles two commit-subject concerns (`feat: bootstrap Catalog service skeleton`, `feat: implement slice 1.1 PublishProduct`) plus the `docs:` design/intent artifacts, under the **"Skeleton + first slice"** exception.
- No `src/` code existed before this session; the skeleton (`slnx`, `Directory.*.props`, empty `src/`+`tests/`) was pre-bootstrapped in an earlier `tidy:` PR.
- The session was conversationally initiated from a handoff doc; the prompt artifact was authored first (before any code), per the user's instruction and CLAUDE.md § 5.
