# Tasks: Customer registry — land the EF-Core Identity service on `main`

## 1. Verify before wiring (skill + reference)

- [x] 1.1 `critterstack-arch-new-project-wolverine-efcore` + `wolverine-http-fundamentals` + `wolverine-testing-alba` / `wolverine-testing-with-testcontainers` skills are the relevant patterns. The reference implementation is `spike/efcore-identity` @ `0ffe42e` (15 files, live-verified). The guard idiom is in-repo: `PublishProduct.ValidateAsync` (`ProblemDetails` → `409` / `WolverineContinue.NoProblems`) + `PublishProductTests` (assert `409`, then verify idempotency from a fresh session).
- [x] 1.2 Confirmed `main` is only 2 commits (docs-only #81/#82) ahead of the spike's merge-base, and none of the 4 shared build files (`Directory.Packages.props`, `CritterMart.slnx`, AppHost `Program.cs` + `.csproj`) changed on `main` — so `git checkout 0ffe42e -- <path>` re-applies all 15 files as a pure additive replay, not a three-way merge.

## 2. Re-apply the spike reference implementation (15 files from `0ffe42e`)

- [x] 2.1 Service: `src/CritterMart.Identity/{Program.cs, CritterMart.Identity.csproj, Properties/launchSettings.json}`, `Customers/{Customer, CustomerRegistered, IdentityDbContext}.cs`, `Features/{RegisterCustomer, GetCustomer}.cs`.
- [x] 2.2 Tests: `tests/CritterMart.Identity.Tests/{CritterMart.Identity.Tests.csproj, IdentityAppFixture.cs, RegisterCustomerTests.cs}`.
- [x] 2.3 Wiring: `CritterMart.slnx` (+2 projects), `Directory.Packages.props` (+`WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql`, `Npgsql.EntityFrameworkCore.PostgreSQL`), `src/CritterMart.AppHost/{Program.cs, CritterMart.AppHost.csproj}` (the `identity` resource on `:5105`, no SPA reference).

## 3. Add the duplicate-email guard (the one increment over the spike)

- [x] 3.1 `src/CritterMart.Identity/Features/RegisterCustomer.cs` — a `Normalize(email) => email.Trim().ToLowerInvariant()` helper; a `ValidateAsync(RegisterCustomer, IdentityDbContext)` returning `409 CustomerAlreadyRegistered` (`ProblemDetails`) when `db.Customers.AnyAsync(c => c.Email == normalized)`, else `WolverineContinue.NoProblems`; `Post` stores the normalized email.
- [x] 3.2 Email unique index — applied as **idempotent startup DDL** (`CREATE UNIQUE INDEX IF NOT EXISTS ux_customers_email ON identity.customers (email)`) from an `ApplicationStarted` hook in `src/CritterMart.Identity/Program.cs`, **not** an EF `HasIndex` (Weasel migrates tables/columns/PKs/FKs but not secondary indexes — a live schema check proved an EF-declared index absent from the DB; see design Decision 3). `IdentityDbContext` carries a pointer comment where the index would otherwise be declared.
- [x] 3.3 `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` — `registering_a_duplicate_email_is_rejected_case_insensitively`: register `ada@example.com`, then POST `"  Ada@Example.com  "` → assert `409` + `Title == "CustomerAlreadyRegistered"`, then assert exactly one row for `ada@example.com` from a fresh scope (the app-guard path).
- [x] 3.4 `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` — `the_email_unique_index_rejects_a_duplicate_inserted_directly`: insert a duplicate **directly through the `DbContext`** (bypassing the HTTP guard) → assert `DbUpdateException` whose inner `PostgresException.SqlState == "23505"`. Proves the DB index backstop independently of the app guard, and locks the Weasel-index gap from regressing.

## 4. Verify

- [x] 4.1 `dotnet build` (full solution) clean — 0 errors (only the pre-existing NU1507).
- [x] 4.2 `dotnet test` — Identity **6/6** green (the 4 re-applied + the case-insensitive duplicate-email HTTP test + the direct-insert index-backstop test); existing services' suites unchanged.
- [x] 4.3 `openspec validate customer-registry --strict` green.
- [x] 4.4 Live Aspire boot (`docs/demo-runbook.md`): all 4 services healthy (Identity `:5105`); register `201`+`Location` / resolve `200` / unknown `404` / duplicate `409 CustomerAlreadyRegistered` (case-insensitive) over HTTP; `catalog`/`identity`/`inventory`/`orders` schemas coexisting; **exactly one** `customers` row despite the duplicate; outbox drained to 0. **The live boot caught the missing index** (EF `HasIndex` not migrated by Weasel) → fixed via startup DDL (3.2) and re-verified live that `ux_customers_email` is present.

## 5. Sibling artifacts (in this PR — the consolidated per-slice chain)

- [x] 5.1 `openspec/changes/customer-registry/{proposal.md, specs/customer-registry/spec.md, design.md, tasks.md}` — the machine-readable contract (3 ADDED requirements) + design + this checklist.
- [x] 5.2 `docs/narratives/006-customer-registration.md` (v1.0) + `docs/narratives/README.md` index.
- [x] 5.3 `docs/prompts/implementations/033-slices-5-1-5-2-customer-registry.md` + `docs/prompts/README.md` index.
- [ ] 5.4 `docs/retrospectives/implementations/033-slices-5-1-5-2-customer-registry.md` — spec-delta closure, the `CustomerRegistered`-stays-local flag, the no-new-ADR call; `next-pickup` memory updated; consolidated PR opened.

## 6. Deferred (out of this change)

- [ ] 6.1 `openspec archive customer-registry` (post-merge tidy — syncs the 3 ADDED requirements into `openspec/specs/customer-registry/spec.md`).
- [ ] 6.2 `spike/efcore-identity` branch cleanup (retained as the reference until this lands; deleted as a separate deliberate step post-merge).
- [ ] 6.3 Slices 5.3 (`X-Customer-Id` resolution) + 5.4 (consume `CustomerRegistered` — and graduate it to `CritterMart.Contracts`) — future, when a consuming BC needs customer data.
