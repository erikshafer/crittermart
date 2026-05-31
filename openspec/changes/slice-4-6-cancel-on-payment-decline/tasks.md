## 1. Cross-BC contract (published language, ADR 014; Decision 1)

- [x] 1.1 `CritterMart.Contracts/ReleaseStock.cs` — `ReleaseStock(string OrderId, IReadOnlyList<ReleaseStockLine> Lines)` + `ReleaseStockLine(string Sku, int Quantity)` (symmetric with `ReserveStock`)

## 2. Orders — cancel on decline (4.6; Decision 2)

- [x] 2.1 `Order/OrderCancelled.cs` — add `CancelReason.PaymentDeclined = "payment_declined"`
- [x] 2.2 `PaymentDecisionHandler.Handle` returns `Task<ReleaseStock?>`: decline branch appends `OrderCancelled { reason: payment_declined }` after `PaymentAuthFailed` and cascades `ReleaseStock` built from `Aggregate.Lines`; approve + guard no-op paths return `null`
- [x] 2.3 `dotnet build` (Orders) succeeds

## 3. Inventory — release stock (2.3; Decisions 3, 4)

- [x] 3.1 `Stock/StockReleased.cs` — `StockReleased(string Sku, string OrderId, int Quantity)`
- [x] 3.2 `Features/ReleaseStock.cs` — `ReleaseStockHandler.Handle(Contracts.ReleaseStock, IDocumentSession)`: per line `FetchForWriting<StockLevelView>(sku)`, append `StockReleased` only where `Reservations.Contains(orderId)` (per-SKU no-op otherwise)
- [x] 3.3 `StockLevelViewProjection` gains `Apply(StockReleased)`: `Available += qty`, `Reserved -= qty`, `Reservations.Remove(orderId)`
- [x] 3.4 `dotnet build` (Inventory) succeeds

## 4. Tests

- [x] 4.1 **Inventory unit (pure fold)**: `StockReleased` reverses `StockReserved` on `StockLevelView` (available restored, reserved zeroed, order dropped)
- [x] 4.2 **Orders tracked-session (decline-cancel)**: swapped declining `IPaymentProvider` → stream `OrderPlaced + StockReserved + PaymentAuthFailed + OrderCancelled { payment_declined }`, status `cancelled`, exactly one `ReleaseStock` cascaded carrying the order's lines
- [x] 4.3 **Inventory tracked-session (release)**: reservation present → `StockReleased`, view restored (available up, reserved down, order dropped)
- [x] 4.4 **Inventory tracked-session (idempotent / no-op)**: duplicate `ReleaseStock` and a never-reserved SKU → no `StockReleased`, view unchanged
- [x] 4.5 **Inventory tracked-session (delayed-grant reordering)**: reservation present from a delayed grant → release still correct (keyed on reservation presence, not event order)
- [x] 4.6 **Cross-BC smoke**: extend the round-trip — place → reserve → declined payment → `OrderCancelled` + `ReleaseStock` over the real broker → Inventory `StockReleased`, view restored

## 5. Verify + close

- [x] 5.1 `dotnet build` + `dotnet test` green (full solution)
- [x] 5.2 `openspec validate slice-4-6-cancel-on-payment-decline --strict` passes
- [x] 5.3 Narrative 004 → v1.4 (Moment 5; `slices` adds 4.6 + 2.3; Document History row) — in this PR
- [x] 5.4 Author `docs/{prompts,retrospectives}/implementations/010-slice-4-6-cancel-on-payment-decline.md`; flag 4.7 (payment timeout) as the only remaining cancellation path; `openspec archive` deferred to a post-merge `tidy:` step
