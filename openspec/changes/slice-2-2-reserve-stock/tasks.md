## 1. Reserve stock (slice 2.2)

- [x] 1.1 `StockReserved(string Sku, string OrderId, int Quantity)` event
- [x] 1.2 `StockLevelView.Apply(StockReserved)` → `Available -= Quantity; Reserved += Quantity`
- [x] 1.3 `ReserveStock(string OrderId, int Quantity)` command + `POST /stock/{sku}/reservations`: `FetchForWriting<StockLevelView>(sku)`; `409` if `Aggregate is null` or `Available < quantity` (no append); else `AppendOne(StockReserved)`
- [x] 1.4 `dotnet build` succeeds

## 2. Tests

- [x] 2.1 Happy: receive `crit-001` 100, reserve `{orderId: ord-A, quantity: 2}` → `StockReserved` on stream (after `StockReceived`); `StockLevelView` available `98` / reserved `2`
- [x] 2.2 Insufficient: receive 1, reserve 2 → `409`; stream has only `StockReceived`; view still available `1` / reserved `0`

## 3. Verify + close

- [x] 3.1 `dotnet build` + `dotnet test` green (5 Inventory tests)
- [x] 3.2 Real docker-compose run: receive then reserve → 204; `GET` reflects reserved 2 / available decremented; over-reserve → `409`
- [x] 3.3 `openspec validate slice-2-2-reserve-stock --strict` passes
- [x] 3.4 Author `docs/retrospectives/implementations/005-slice-2-2-reserve-stock.md`
