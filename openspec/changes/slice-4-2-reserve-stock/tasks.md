## 1. Shared contracts project (Decision 4)

- [x] 1.1 New `src/CritterMart.Contracts/CritterMart.Contracts.csproj` (classlib, net10.0; no Wolverine/Marten dependency — plain records)
- [x] 1.2 Records: `ReserveStock(string OrderId, IReadOnlyList<ReserveStockLine> Lines)` + `ReserveStockLine(string Sku, int Quantity)`; `StockReserved(string OrderId)`; `StockReservationFailed(string OrderId, string Reason)`
- [x] 1.3 Add to `CritterMart.slnx`; `ProjectReference` from `CritterMart.Orders` and `CritterMart.Inventory`

## 2. RabbitMQ wiring (Decision 6)

- [x] 2.1 `Directory.Packages.props`: add `WolverineFx.RabbitMQ` at `6.1.0`
- [x] 2.2 `CritterMart.Orders.csproj` + `CritterMart.Inventory.csproj`: `PackageReference WolverineFx.RabbitMQ`
- [x] 2.3 Both `Program.cs`: `opts.UseRabbitMqUsingNamedConnection("rabbitmq").AutoProvision().UseConventionalRouting()`; also `opts.ApplicationAssembly = typeof(Program).Assembly` (deterministic discovery when service assemblies share a process — surfaced by the cross-BC smoke)
- [x] 2.4 `AppHost/Program.cs`: `.WithReference(rabbitmq).WaitFor(rabbitmq)` on `orders` and `inventory`; fixed the stale "slice 2.2" comment

## 3. Inventory — reserve via message (Decisions 2, 5, 7, 8)

- [x] 3.1 `Handle(ReserveStock)` message handler: `FetchForWriting` each line's `StockLevelView`; all lines sufficient → append `Stock.StockReserved(sku, orderId, qty)` per SKU in one transaction + cascade `Contracts.StockReserved(orderId)`; any short → modify nothing + cascade `Contracts.StockReservationFailed(orderId, "insufficient")`
- [x] 3.2 Idempotency guard: `StockLevelView.Reservations` tracks reserved order ids; a SKU already reserved for the order is not reserved again (re-publishes the grant)
- [x] 3.3 Retired `POST /stock/{sku}/reservations`; logic now lives in the handler; per-SKU `Stock.StockReserved` shape kept

## 4. Orders — cascade + record outcome (Decisions 2, 3, 5; slice 4.5)

- [x] 4.1 `PlaceOrder` endpoint returns `(IResult, ReserveStock?)` — cascade the whole-order `ReserveStock` (null on the 409 paths)
- [x] 4.2 Events: `Order/StockReserved.cs`; `Order/StockReservationFailed.cs`; `Order/OrderCancelled.cs` (+ `CancelReason.StockUnavailable`)
- [x] 4.3 `StockReservedHandler.Handle(Contracts.StockReserved)`: `FetchForWriting<OrderStatusView>`; acts only while `awaiting_confirmation` (else no-op) → append `Order.StockReserved`
- [x] 4.4 `StockReservationFailedHandler.Handle(Contracts.StockReservationFailed)`: guard → append `Order.StockReservationFailed` then `OrderCancelled(stock_unavailable)`
- [x] 4.5 `OrderStatusView`: `OrderStatus.StockReserved` + `OrderStatus.Cancelled`; `Apply(StockReserved)`, `Apply(OrderCancelled)` folds
- [x] 4.6 `dotnet build` succeeds

## 5. Tests (tracked-session both sides + the two-host broker smoke)

- [x] 5.1 **Unit (pure fold)**: `OrderStatusViewProjection` folds for `StockReserved` (→ `stock_reserved`) and `OrderCancelled` (→ `cancelled`)
- [x] 5.2 **Inventory tracked-session**: `ReserveStock` all-available → per-SKU `StockReserved` + cascaded `Contracts.StockReserved`; any short → no stream change + cascaded `StockReservationFailed`
- [x] 5.3 **Inventory tracked-session (idempotent)**: duplicate `ReserveStock` for an already-reserved order → no second reservation, re-publishes grant
- [x] 5.4 **Orders tracked-session**: `Contracts.StockReserved` → Order `StockReserved` + `stock_reserved`; `Contracts.StockReservationFailed` → `StockReservationFailed` + `OrderCancelled` + `cancelled`
- [x] 5.5 **Orders tracked-session (idempotent)**: late grant on a terminal (cancelled) order → no further event
- [x] 5.6 **Two-host broker smoke**: new `CritterMart.CrossBc.Tests` project (RabbitMQ + shared Postgres, both hosts via `extern alias`); place an order → `StockReserved` lands as a Klefter commit on the Order stream and Inventory reserved the SKU
- [x] 5.7 **Orders tracked-session**: placing an order cascades a whole-order `ReserveStock` carrying both lines

## 6. Verify + close

- [x] 6.1 `dotnet build` + `dotnet test` green (Inventory 6, Catalog 7, Orders 18, CrossBc 1 — 32 total, 0 failures)
- [x] 6.2 `openspec validate slice-4-2-reserve-stock --strict` passes
- [x] 6.3 Narrative 004 → v1.2 (Moment 3; `slices [3.1, 4.1, 4.2, 4.5]`; Document History row) — done in this PR
- [x] 6.4 Author `docs/retrospectives/implementations/008-slice-4-2-reserve-stock.md`; flag the published-language ADR follow-up; `openspec archive` deferred to a post-merge `tidy:` step
