## 1. ChangeProductPrice

- [x] 1.1 Verify Wolverine 6.1 (route-param binding, `[Entity]`) and Marten 9.2 (`Events.Append` to an existing stream, `LoadAsync`) via ctx7 — used explicit `LoadAsync` (the `[Entity]` HTTP+Marten integration lives in `WolverineFx.Http.Marten`, which we don't reference)
- [x] 1.2 Add `src/CritterMart.Catalog/Products/ProductPriceChanged.cs` (audit event: sku, oldPrice, newPrice, changedBy, changedAt)
- [x] 1.3 Add `src/CritterMart.Catalog/Features/ChangeProductPrice.cs`: `POST /products/{sku}/price` — load `Product` by SKU (unknown → 404), append `ProductPriceChanged` (old + new) to the SKU stream, set `Product.Price = newPrice`, one transaction
- [x] 1.4 Confirm `dotnet build` succeeds

## 2. Tests

- [x] 2.1 `tests/CritterMart.Catalog.Tests/ChangeProductPriceTests.cs` reusing `CatalogAppFixture`; clean Marten data first
- [x] 2.2 Happy path: publish `crit-001` @ `24.99`, change to `19.99` → one `ProductPriceChanged` (old `24.99`, new `19.99`) appended after `ProductPublished` (stream has 2 events); document price `19.99`; `GET /products` shows `crit-001` at `19.99`
- [x] 2.3 Not-found: change price for an unknown SKU → `404`

## 3. Verify + close

- [x] 3.1 `dotnet build` + `dotnet test` green (7 tests)
- [x] 3.2 Run the service; change a price then `GET /products` over real HTTP reflects it (crit-777 50.00 → 39.99)
- [x] 3.3 `openspec validate slice-1-3-change-price --strict` passes
- [x] 3.4 Author `docs/retrospectives/implementations/003-slice-1-3-change-price.md`
