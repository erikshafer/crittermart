## 1. Published-Language Contract

- [x] 1.1 Add `CommitStock.cs` to `CritterMart.Contracts` (`CommitStock`, `CommitStockLine` records, mirroring `ReleaseStock`/`ReleaseStockLine`)

## 2. Inventory — Event and Projection

- [x] 2.1 Add `StockCommitted.cs` event record to `CritterMart.Inventory/Stock/` (`StockCommitted(string Sku, string OrderId, int Quantity)`)
- [x] 2.2 Add `Committed` property to `StockLevelView`
- [x] 2.3 Add `Apply(StockCommitted)` fold to `StockLevelViewProjection` (reserved -= qty, committed += qty, remove order from Reservations)

## 3. Inventory — Handler

- [x] 3.1 Add `CommitStockHandler` in `CritterMart.Inventory/Features/CommitStock.cs` (mirrors `ReleaseStockHandler` — per-SKU, idempotent via `Reservations.Contains`)

## 4. Orders — Cascade Amendment

- [x] 4.1 Change `PaymentDecisionHandler.Handle` return type from `Task<Contracts.ReleaseStock?>` to `Task<(Contracts.CommitStock?, Contracts.ReleaseStock?)>` (nullable tuple — see design.md Decision 2)
- [x] 4.2 Approve path: return `(new Contracts.CommitStock(orderId, lines), null)` instead of `null`

## 5. Context Map

- [x] 5.1 Add `CommitStock` to the Orders→Inventory edge in `docs/context-map/README.md` (mermaid diagram + integration table)

## 6. Tests

- [x] 6.1 Unit test: `StockCommitted` fold on `StockLevelView` (assert the `Available + Reserved + Committed = ΣReceived` invariant)
- [x] 6.2 Integration test: `CommitStockHandler` happy path (reservation committed, view updated)
- [x] 6.3 Integration test: `CommitStockHandler` idempotent no-op (duplicate commit, no reservation)
- [x] 6.4 Orders tracked-session test: approve path cascades `CommitStock` with correct order ID and lines
- [x] 6.5 Amend cross-BC approve smoke: verify `CommitStock` arrives at Inventory and stock is committed

## 7. Narrative and Workshop

- [x] 7.1 Workshop 001 → v1.6 (slice 2.4 in table + GWT scenarios + § 8 Q2/Q3 resolved)
- [x] 7.2 Narrative 004 → v1.8 (amend Moment 4 to note the Inventory consequence of confirmation)
