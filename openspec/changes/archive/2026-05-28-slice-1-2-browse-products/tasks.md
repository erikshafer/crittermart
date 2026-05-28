## 1. Browse endpoint

- [x] 1.1 Verify Wolverine 6.1 (`[WolverineGet]`, collection-as-JSON response) and Marten 9.2 (`IQuerySession.Query<T>().ToListAsync()`) API via ctx7
- [x] 1.2 Add `src/CritterMart.Catalog/Features/BrowseProducts.cs`: static `[WolverineGet("/products")]` endpoint taking an injected `IQuerySession`, querying `Product`, projecting each to `ProductCatalogView`, returning the list
- [x] 1.3 Confirm `dotnet build` succeeds

## 2. Test

- [x] 2.1 Add `tests/CritterMart.Catalog.Tests/BrowseProductsTests.cs` reusing `CatalogAppFixture` and `[Collection("catalog")]`; clean Marten data first
- [x] 2.2 Publish `crit-001` "Cosmic Critter Plush" `24.99` and `crit-002` "Nebula Newt" `18.00`; `GET /products`; assert both returned with SKU, name, description, and current price (plus an empty-catalog → empty-list case)

## 3. Verify + close

- [x] 3.1 `dotnet build` + `dotnet test` green (4 tests)
- [x] 3.2 Run the service; `GET /products` over real HTTP returns the listing (with `sku`) against the docker-compose Postgres
- [x] 3.3 `openspec validate slice-1-2-browse-products --strict` passes
- [x] 3.4 Author `docs/retrospectives/implementations/002-slice-1-2-browse-products.md`
