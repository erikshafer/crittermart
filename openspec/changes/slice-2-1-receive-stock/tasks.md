## 1. Inventory service skeleton

- [x] 1.1 Create `src/CritterMart.Inventory/CritterMart.Inventory.csproj` (refs `WolverineFx.Http`, `WolverineFx.Marten`, `WolverineFx.RuntimeCompilation`)
- [x] 1.2 `Program.cs`: Marten event store, `inventory` schema (ADR 002), `StreamIdentity.AsString`, register `StockLevelViewProjection` inline, Wolverine + Wolverine.Http, AutoApplyTransactions, ApplyAllDatabaseChangesOnStartup; `appsettings.json`
- [x] 1.3 Add `tests/CritterMart.Inventory.Tests` project (Alba, Testcontainers.PostgreSql, xunit, Shouldly)
- [x] 1.4 Add both projects to `CritterMart.slnx`; `dotnet build` succeeds

## 2. Stock event sourcing (slice 2.1)

- [x] 2.1 `StockReceived(string Sku, int Quantity)` event
- [x] 2.2 `StockLevelView` document (`Id`=sku, `Available`, `Reserved`) + `partial class StockLevelViewProjection : SingleStreamProjection<StockLevelView, string>` with `Apply(StockReceived, view)` (`SingleStreamProjection` is in `Marten.Events.Aggregation`)
- [x] 2.3 `ReceiveStock(int Quantity)` command + `POST /stock/{sku}/receipts` (FetchForWriting + AppendOne) — void endpoint returns 204
- [x] 2.4 `GET /stock/{sku}` → `StockLevelView` (LoadAsync, 404 if none)

## 3. Tests

- [x] 3.1 `CritterMart.Inventory.Tests` fixture (Alba + Testcontainers); clean Marten data
- [x] 3.2 Receive new: `crit-001` qty 100 → `StockReceived 100` on stream; `StockLevelView` available 100 / reserved 0
- [x] 3.3 Receive additional: +50 → second `StockReceived`; available 150
- [x] 3.4 `GET /stock/crit-001` reflects the level

## 4. Verify + close

- [x] 4.1 `dotnet build` + `dotnet test` green (3 tests)
- [x] 4.2 Real docker-compose run: receive 100 + 50 → `GET /stock/crit-001` shows available 150; `inventory` schema created; unknown SKU → 404
- [x] 4.3 `openspec validate slice-2-1-receive-stock --strict` passes
- [x] 4.4 Author `docs/retrospectives/implementations/004-slice-2-1-receive-stock.md`
