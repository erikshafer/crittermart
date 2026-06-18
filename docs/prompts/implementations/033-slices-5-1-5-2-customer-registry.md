# Prompt: Implementations 033 — Slices 5.1 + 5.2 Customer Registry (land the EF-Core Identity service on `main`)

**Kind**: per-slice implementation (Identity service skeleton + first two slices, consolidated into one PR)
**Files touched**: `docs/prompts/implementations/033-slices-5-1-5-2-customer-registry.md` (new, this file); `openspec/changes/customer-registry/{proposal.md,specs/customer-registry/spec.md}` (authored this session; design.md + tasks.md added here); `docs/narratives/006-customer-registration.md` (authored this session) + `docs/narratives/README.md` (index); `src/CritterMart.Identity/**` (new project, re-applied from the spike + guard); `tests/CritterMart.Identity.Tests/**` (new project, re-applied + new duplicate-email test); `CritterMart.slnx`, `Directory.Packages.props`, `src/CritterMart.AppHost/{Program.cs,CritterMart.AppHost.csproj}` (wiring); `docs/retrospectives/implementations/033-slices-5-1-5-2-customer-registry.md` (forthcoming, authored at session close)
**Mode**: solo implementation re-applying the reference spike (`spike/efcore-identity` @ `0ffe42e`, 15 files) plus the one guard it skipped; tool-backed openspec scaffolding (openspec CLI 1.3.1)
**Commit subject(s)**: `feat: land Identity customer registry on main — slices 5.1 (register) + 5.2 (resolve)` for the code; the design artifacts (proposal, narrative, prompt, design.md, tasks.md) ride under a `docs:` subject. One PR per the consolidate-slice-PRs convention.

## Framing

[Workshop 002](../../workshops/002-identity-event-model.md) promoted Identity from a round-one stub ([ADR 009](../../decisions/009-polecat-deferred-for-round-one.md)) to a kept, deployed **EF-Core customer registry**, named its strategic-design relationship (**Open-Host Service + Published Language**, [context map](../../context-map/README.md)), and modeled four slices. An exploratory spike (`spike/efcore-identity` @ `0ffe42e`) built slices **5.1 (Register)** and **5.2 (Resolve)** end-to-end and was live-verified in-stack. **A spike inverts the pipeline** — it legitimately builds before any design artifact exists. This session re-rights it: the kept code lands on `main` through the normal per-slice chain (OpenSpec proposal → narrative → **this prompt** → code → retro), all tracing back to Workshop 002. This is also the next BC-implementation after the #82 design-return interleave.

**Identity is CritterMart's one non-event-sourced bounded context, and that is the teaching payoff.** Catalog stores documents; Inventory/Orders store event streams; Identity stores a **row**. There is no stream, no projection, no fold — the `Customer` row *is* the read model. Its single `CustomerRegistered` "event" is **not** the source of truth and **not** a stream entry — it is an outbound notification enrolled in the EF-Core **transactional outbox** in the same transaction as the row insert, published after that insert commits. The slice proves Wolverine's handler model (command → handler → cascaded message, one outbox) is **persistence-agnostic**: the same static-endpoint shape over a `DbContext` instead of an `IDocumentSession`.

**The reference implementation is the spike; re-apply it, don't re-derive it.** The 15 files at `0ffe42e` are the proven shape (verified live: 4 services healthy, register/read/404 over HTTP, four `identity`/`catalog`/`inventory`/`orders` schemas coexisting, the outbox drained to 0, the `CustomerRegistered` exchange present with no bindings). `git show 0ffe42e:<path>` is the source of truth for each file. The spike branch is **retained as the reference — it is NOT merged and NOT deleted**.

**The one thing the kept service adds over the spike — a duplicate-email guard.** The spike inserted a new row on every `POST /customers`. Workshop 002 § 6 slice 5.1's failure path models a duplicate rejected with `CustomerAlreadyRegistered`. Two decisions were settled with the owner before this prompt froze:

1. **Uniqueness key = normalized email** (trimmed + lowercased, case-insensitive). `Ada@Example.com` and `ada@example.com` are the same customer. The email is normalized once and used everywhere — the guard query, the stored row, and the published event all see the same value.
2. **Enforced both at the app layer and in the database.** A railway-style `ValidateAsync` → `ProblemDetails` guard returns the friendly `409 CustomerAlreadyRegistered` (mirroring `PublishProduct.ValidateAsync` exactly), **and** a DB **unique index** on the email column is the true backstop that closes the check-then-insert race. Catalog never needs this index because a product's SKU *is* its Marten document id (the primary key enforces uniqueness for free); Identity keys uniqueness on email, which is **not** the primary key, so the index is the faithful mirror of Catalog's actual guarantee.

**Version note.** The codebase is on the 2026 Critter Stack line (Wolverine 6.8.0 / Marten 9 / JasperFx 2; `Directory.Packages.props`). The spike was authored on that exact line, so its APIs are current — no migration-skill caveats apply. EF Core / Npgsql is the .NET 10 / EF Core 10 line.

## Goal

Ship a running `CritterMart.Identity` service on `main` that satisfies the `customer-registry` capability's three requirements (register a customer; emails are unique; resolve by id), proven by integration tests over every Workshop 002 § 6 slice-5.1/5.2 GWT scenario — including the **new** duplicate-email failure the spike skipped — with the openspec change's `design.md` + `tasks.md` authored and `openspec validate customer-registry --strict` green.

Concretely:

- `src/CritterMart.Identity`: Wolverine over EF Core / Npgsql on the shared Postgres under the `identity` schema (ADR 002), Aspire-wired on `:5105`, a 4th CritterWatch node, Wolverine health checks.
- `RegisterCustomer` (`POST /customers`) end-to-end: command → `Customer` row inserted → `CustomerRegistered` cascaded to the EF-Core outbox in the same transaction → `201 Created` with `Location`.
- Duplicate (normalized) email rejected with `CustomerAlreadyRegistered` (`409`), **idempotently** — no row, no event — enforced by the `ValidateAsync` guard **and** a unique index.
- `GetCustomer` (`GET /customers/{id}`): `200` with `{ id, email, displayName, registeredAt }`, or `404` — the Open-Host Service read.
- All Identity integration tests green under Alba + Testcontainers; the service verified to actually boot and serve the endpoints in a live Aspire stack (not just tests passing).

## Spec delta

This session lands the **`customer-registry`** capability's first satisfying implementation under `src/`, and the openspec change gains its two deferred artifacts (`design.md` + `tasks.md`), making `customer-registry` a complete four-artifact change ready to `openspec archive` post-merge. The proposal + `specs/customer-registry/spec.md` (three ADDED requirements) and Narrative 006 are authored as this PR's siblings and are the source of truth — code and tests must agree with both. The **net-new spec content over the spike** is the duplicate-email requirement (*Customer emails are unique in the registry*) and its case-insensitive rejection scenario. No edit to Workshop 002 — it is the source of truth; if the code surfaces a contradiction, **stop and raise it**.

## Orientation

Read these in this order before writing anything:

1. **`openspec/changes/customer-registry/proposal.md`** + **`specs/customer-registry/spec.md`** — the machine-readable contract. Three requirements: register; email uniqueness; resolve.
2. **`docs/narratives/006-customer-registration.md`** — the human-readable sibling; Moment 1 (register), Moment 2 (duplicate email, case-insensitive), Moment 3 (resolve + 404). Code and tests must agree with both it and the proposal.
3. **`docs/workshops/002-identity-event-model.md`** — § 2 (Identity BC: EF-Core registry; the row is the read model; `CustomerRegistered` is an outbox notification, not a stream event), § 4 (event vocabulary), § 5 (slice table 5.1/5.2), § 6 (the authoritative GWT scenarios, anchor data `ada@example.com` / "Ada Lovelace"), § 7 (no projections).
4. **The spike reference implementation** — `git show 0ffe42e --stat` for the 15-file inventory; `git show 0ffe42e:<path>` for each file. Re-apply faithfully; the **Weasel-vs-EF lowercase-column reconciliation** in `IdentityDbContext` is load-bearing — keep it.
5. **`src/CritterMart.Catalog/Features/PublishProduct.cs`** — the `ValidateAsync` → `ProblemDetails` / `WolverineContinue.NoProblems` guard idiom to mirror for the duplicate-email check; **`tests/CritterMart.Catalog.Tests/PublishProductTests.cs`** — the duplicate-rejection test idiom (assert `409`, then verify idempotency from a fresh session).
6. **`docs/context-map/README.md`** — Identity → Open-Host Service + Published Language; the OHS-for-frontend / PL-for-backends split; `CustomerRegistered` declared but unconsumed.
7. **ADRs**: **009** (Identity is a data store, not auth — no Polecat), **002** (shared Postgres, schema-per-service), **001** + **003** (no sync service-to-service HTTP; RabbitMQ transport), **019** (Wolverine health checks).
8. **Skills**: `critterstack-arch-new-project-wolverine-efcore` (the EF-Core + Wolverine outbox patterns — proven in the spike), `wolverine-http-fundamentals` (the endpoints + the `ValidateAsync` guard), `wolverine-testing-alba` + `wolverine-testing-with-testcontainers` (the integration tests), and the openspec CLI for `design.md`/`tasks.md`. Defer framework mechanics to the upstream JasperFx skills; verify any uncertain API with `ctx7`.

## Working pattern

1. **Author `design.md`** via `openspec instructions design --change customer-registry`. Keep it change-local and reference cross-cutting ADRs rather than restating them. Record the genuine technical decisions this slice forces:
   - **EF Core over Wolverine, schema-per-service `identity`** — `PersistMessagesWithPostgresql(conn, "identity")` + `AddDbContextWithWolverineIntegration<IdentityDbContext>` + `UseEntityFrameworkCoreWolverineManagedMigrations` + `AutoApplyTransactions`; the outbox and the row commit in one transaction.
   - **The Weasel-vs-EF column-casing reconciliation** — explicit lowercase column names so Weasel's unquoted DDL and EF's quoted identifiers agree (the load-bearing spike fix).
   - **The duplicate-email guard** — normalized email (trim + lowercase) used in the guard query, the stored row, and the event; `ValidateAsync` → `409 CustomerAlreadyRegistered`; **plus** a unique index on the email column as the race backstop. Name why both (app guard = friendly error; index = true guarantee, the mirror of Catalog's free SKU-PK uniqueness).
   - **`CustomerRegistered` stays Identity-local** — not in `CritterMart.Contracts`; published unconsumed (PL declared, not trafficked); graduates to Contracts only when slice 5.4 lands a consumer.
2. **Author `tasks.md`** via `openspec instructions tasks` — the live checkbox list.
3. **Re-apply the 15 spike files** from `0ffe42e` (service: `Program.cs`, `CritterMart.Identity.csproj`, `Customers/{Customer,CustomerRegistered,IdentityDbContext}.cs`, `Features/{RegisterCustomer,GetCustomer}.cs`, `Properties/launchSettings.json`; tests: `CritterMart.Identity.Tests.csproj`, `IdentityAppFixture.cs`, `RegisterCustomerTests.cs`; wiring: `CritterMart.slnx`, `Directory.Packages.props`, `src/CritterMart.AppHost/{Program.cs,CritterMart.AppHost.csproj}`).
4. **Add the guard** to the re-applied code:
   - `RegisterCustomer.cs`: a `Normalize(email) => email.Trim().ToLowerInvariant()` helper; a `ValidateAsync(RegisterCustomer, IdentityDbContext)` returning `409 CustomerAlreadyRegistered` ProblemDetails when `db.Customers.AnyAsync(c => c.Email == normalized)`, else `WolverineContinue.NoProblems`; `Post` normalizes the email before building the row + event.
   - `IdentityDbContext.cs`: `e.HasIndex(x => x.Email).IsUnique();` on the `Customer` entity.
5. **Add the duplicate-email integration test** to `RegisterCustomerTests.cs`: register `ada@example.com`, then `POST` `"  Ada@Example.com  "` → assert `409`, then assert from a fresh `IdentityDbContext` scope that exactly one row exists for `ada@example.com` (idempotency; mirrors `PublishProductTests`).
6. **Verify**: `dotnet build` + `dotnet test` (Identity tests green, incl. the new duplicate test); `openspec validate customer-registry --strict` green; then **boot the full Aspire stack** (`docs/demo-runbook.md`) and exercise register / read / 404 / duplicate over HTTP, confirming the 4th node on CritterWatch and the `identity` schema on disk.
7. **Author the retrospective** at session close: spec-delta closure (the duplicate-email requirement is the net-new content; the rest *satisfies* the spike-proven contract), the **`CustomerRegistered`-stays-local** flag (a non-terminal PL edge slice 5.4 completes — named, not silently shipped), and the **no-new-ADR** call (the data-store-not-auth boundary is carried by ADR 009 + context map + Workshop 002; promote to an ADR only if this session found it re-litigated — it did not). Update the `next-pickup` memory.

## Deliverable plan

| Deliverable | Path | Notes |
| --- | --- | --- |
| This prompt | `docs/prompts/implementations/033-slices-5-1-5-2-customer-registry.md` | Frozen session intent |
| Design doc | `openspec/changes/customer-registry/design.md` | Grain-aware; the EF-outbox + guard decisions |
| Task checklist | `openspec/changes/customer-registry/tasks.md` | Live checkbox list |
| Identity service | `src/CritterMart.Identity/` | Re-applied from `0ffe42e` + the duplicate-email guard (normalize + `ValidateAsync` 409 + unique index) |
| Test project | `tests/CritterMart.Identity.Tests/` | Re-applied + the new duplicate-email test |
| Solution wiring | `CritterMart.slnx` | Add both projects |
| Package versions | `Directory.Packages.props` | `WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql`, `Npgsql.EntityFrameworkCore.PostgreSQL` |
| AppHost wiring | `src/CritterMart.AppHost/{Program.cs,CritterMart.AppHost.csproj}` | `identity` resource on `:5105`; no SPA reference |
| Retrospective | `docs/retrospectives/implementations/033-slices-5-1-5-2-customer-registry.md` | Authored at session close |

## Out of scope

- **No `CustomerRegistered` move to `CritterMart.Contracts`.** It stays Identity-local and publishes unconsumed — the Published-Language edge is declared, not trafficked. It graduates to Contracts only when slice 5.4 lands a consumer. (Flag this in the retro: a deliberate non-terminal edge, not a half-finished path.)
- **No `X-Customer-Id` resolution (slice 5.3).** The registry is not wired to the seam; round-one identity stays stubbed (ADR 009).
- **No slice 5.4** (a cross-BC consumer). It lands when a BC genuinely needs customer data (likely Orders enriching `OrderStatusView`).
- **No authentication / authorization / Polecat** (ADR 009). Identity is a data store. No login, no claims, no sessions.
- **No frontend / SPA changes.** Identity gets no SPA `WithReference` and no Identity URL — it has no frontend-driven flow this slice.
- **No new ADR.** The data-store-not-auth boundary is carried by ADR 009 + the context map + Workshop 002. Author an ADR only if this session finds the boundary re-litigated (it is not expected to). Record the call in the retro.
- **No edits to Workshop 002.** It is the source of truth. A surfaced contradiction is raised, not silently fixed.
- **No deletion of `spike/efcore-identity`.** It is retained as the reference implementation until this lands; its post-merge cleanup is a separate, deliberate step.
- **No `openspec archive`.** Archiving `customer-registry` (syncing the delta into `openspec/specs/`) is the standard post-merge tidy.
- **No opportunistic edits** outside the named deliverables. Carry-forward chores (the POST-TALK `Payment__DeclineOverAmount` removal, NU1507, etc.) stay out.
