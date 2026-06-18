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
- [x] 3.2 `src/CritterMart.Identity/Customers/IdentityDbContext.cs` — `e.HasIndex(x => x.Email).IsUnique();` on the `Customer` entity (the race backstop; the stored value is already normalized, so it enforces case-insensitive uniqueness).
- [x] 3.3 `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` — a `registering_a_duplicate_email_is_rejected_case_insensitively` test: register `ada@example.com`, then POST `"  Ada@Example.com  "` → assert `409` + `Title == "CustomerAlreadyRegistered"`, then assert exactly one row for `ada@example.com` from a fresh scope (idempotency).

## 4. Verify

- [ ] 4.1 `dotnet build` clean (only the pre-existing NU1507).
- [ ] 4.2 `dotnet test` — Identity tests green, including the new duplicate-email test (5 tests); existing services' suites unchanged.
- [ ] 4.3 `openspec validate customer-registry --strict` green.
- [ ] 4.4 Live Aspire boot (`docs/demo-runbook.md`): register / read / 404 / duplicate over HTTP; the 4th CritterWatch node; the `identity` schema + `customers` row + unique index on disk; outbox drains to 0.

## 5. Sibling artifacts (in this PR — the consolidated per-slice chain)

- [x] 5.1 `openspec/changes/customer-registry/{proposal.md, specs/customer-registry/spec.md, design.md, tasks.md}` — the machine-readable contract (3 ADDED requirements) + design + this checklist.
- [x] 5.2 `docs/narratives/006-customer-registration.md` (v1.0) + `docs/narratives/README.md` index.
- [x] 5.3 `docs/prompts/implementations/033-slices-5-1-5-2-customer-registry.md` + `docs/prompts/README.md` index.
- [ ] 5.4 `docs/retrospectives/implementations/033-slices-5-1-5-2-customer-registry.md` — spec-delta closure, the `CustomerRegistered`-stays-local flag, the no-new-ADR call; `next-pickup` memory updated; consolidated PR opened.

## 6. Deferred (out of this change)

- [ ] 6.1 `openspec archive customer-registry` (post-merge tidy — syncs the 3 ADDED requirements into `openspec/specs/customer-registry/spec.md`).
- [ ] 6.2 `spike/efcore-identity` branch cleanup (retained as the reference until this lands; deleted as a separate deliberate step post-merge).
- [ ] 6.3 Slices 5.3 (`X-Customer-Id` resolution) + 5.4 (consume `CustomerRegistered` — and graduate it to `CritterMart.Contracts`) — future, when a consuming BC needs customer data.
