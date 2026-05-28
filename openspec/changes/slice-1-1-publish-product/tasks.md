## 1. Catalog service skeleton

- [x] 1.1 Create `src/CritterMart.Catalog/CritterMart.Catalog.csproj` (net10.0, references `WolverineFx.Http`, `WolverineFx.Marten` — `WolverineFx` arrives transitively)
- [x] 1.2 Add `Program.cs`: Marten document+event store on the shared Postgres, `DatabaseSchemaName`/`Events.DatabaseSchemaName` = `catalog` (ADR 002), `StreamIdentity.AsString`, `ApplyAllDatabaseChangesOnStartup`; integrate Wolverine + Wolverine.Http; AutoApplyTransactions
- [x] 1.3 Add `docker-compose.yml` at repo root for a local Postgres; wire the connection string into the service
- [x] 1.4 Add `tests/CritterMart.Catalog.Tests/CritterMart.Catalog.Tests.csproj` (Alba, Testcontainers.PostgreSql, xunit, Shouldly, Microsoft.NET.Test.Sdk)
- [x] 1.5 Add both projects to `CritterMart.slnx`
- [x] 1.6 Confirm `dotnet build` succeeds

## 2. PublishProduct slice

- [x] 2.1 Define `Product` document (`Id = sku`, name, description, price) — source of truth
- [x] 2.2 Define `ProductPublished` audit event (sku, name, description, price, publishedBy, publishedAt)
- [x] 2.3 Define `PublishProduct` command (sku, name, description, price; seller/actor stubbed per ADR 009)
- [x] 2.4 Implement the endpoint: `ValidateAsync` existence check by SKU → on new, `Post` stores `Product` + `StartStream` `ProductPublished` in one transaction; on duplicate, return `ProblemDetails` (`ProductAlreadyPublished`), no document, no event
- [x] 2.5 Expose the Wolverine.Http endpoint for `PublishProduct`
- [x] 2.6 Define `ProductCatalogView` read shape (query over `Product` documents) — only as far as slice 1.1 needs to observe the published product

## 3. Tests (both GWT scenarios)

- [x] 3.1 Alba + Testcontainers test fixture; clean Marten data between runs
- [x] 3.2 Happy path: publish `crit-001` "Cosmic Critter Plush" @ `24.99` → product visible in `ProductCatalogView`; one `ProductPublished` on the stream
- [x] 3.3 Duplicate-SKU: re-publish `crit-001` → rejected with `ProductAlreadyPublished`; no second document; no second `ProductPublished`; existing document unchanged

## 4. Verify + close

- [x] 4.1 `dotnet build` + `dotnet test` green
- [x] 4.2 Run the service; exercise `PublishProduct` end-to-end (happy + duplicate) against the docker-compose Postgres
- [x] 4.3 `openspec validate slice-1-1-publish-product --strict` passes
- [x] 4.4 Author `docs/retrospectives/implementations/001-slice-1-1-publish-product.md` (spec-delta closure; ADR 011 verdict — held)
- [x] 4.5 Update the `next-pickup-slice-1-1` memory to point at the next work
