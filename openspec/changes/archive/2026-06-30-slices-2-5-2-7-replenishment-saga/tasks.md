# Tasks: slices-2-5-2-7-replenishment-saga (slices 2.5–2.7)

## Implementation #035 — branch `feat/inventory-replenishment-saga`

### Saga + messages (build first — pure, unit-testable)

- [ ] Add `src/CritterMart.Inventory/Stock/BackorderDetected.cs` — `record BackorderDetected([property: SagaIdentity] string Sku, int Shortfall)`
- [ ] Add `src/CritterMart.Inventory/Stock/RequestRestock.cs` — `record RequestRestock(string Sku, int Quantity)`
- [ ] Add `src/CritterMart.Inventory/Stock/RestockArrived.cs` — `record RestockArrived([property: SagaIdentity] string Sku, int Quantity)`
- [ ] Add `src/CritterMart.Inventory/Stock/ReplenishTimeout.cs` — `record ReplenishTimeout([property: SagaIdentity] string Sku, TimeSpan Delay) : TimeoutMessage(Delay)`
- [ ] Add `src/CritterMart.Inventory/Stock/ReplenishmentEscalated.cs` — `record ReplenishmentEscalated(string Sku, int Outstanding)` (5th message)
- [ ] Add `src/CritterMart.Inventory/Stock/ReplenishDeadline.cs` — `record ReplenishDeadline(TimeSpan Duration)` + `static readonly Default = 2 min`
- [ ] Add `src/CritterMart.Inventory/Stock/Replenishment.cs` — `Replenishment : Saga` with `Start`/`Handle(BackorderDetected)`/`Handle(RestockArrived)`/`Handle(ReplenishTimeout)` + static `NotFound(RestockArrived)` and `NotFound(ReplenishTimeout)`

### Sink handlers

- [ ] Add `src/CritterMart.Inventory/Features/ReplenishmentNotifications.cs` — `RequestRestockHandler` (supplier-notification log stub) + `ReplenishmentEscalatedHandler` (operator-alert `LogWarning`)

### Wiring (slices 2.5 + 2.6)

- [ ] Modify `src/CritterMart.Inventory/Features/ReserveStock.cs` — on the unchanged refusal branch, return the existing `StockReservationFailed` together with one `BackorderDetected` per short line via `OutgoingMessages`
- [ ] Modify `src/CritterMart.Inventory/Features/ReceiveStock.cs` — inject `IMessageBus`, `PublishAsync(new RestockArrived(sku, quantity))` after the append; response stays `204`
- [ ] Modify `src/CritterMart.Inventory/Program.cs` — bind `ReplenishDeadline` from `Inventory:ReplenishTimeout` (`GetValue<TimeSpan?>(...) ?? Default` + `AddSingleton`); confirm saga storage needs no extra Marten call

### Tests

- [ ] Add `tests/CritterMart.Inventory.Tests/ReplenishmentSagaTests.cs` (pure unit) — open, max-update idempotency, cover-and-complete, partial-reduce-stay-open, escalate-and-complete (asserts `ReplenishmentEscalated` returned)
- [ ] Add integration coverage — shortfall→open (unchanged `StockReservationFailed` + `RequestRestock` sent + saga doc with `Outstanding`), receipt→resolve (saga deleted, `StockReceived` unchanged), not-found no-ops for `RestockArrived`/`ReplenishTimeout`
- [ ] `dotnet build` zero errors; `dotnet test` — existing Inventory tests stay green + new tests pass

### Artifacts

- [ ] `docs/narratives/008-operator-replenish-backorders.md` (v1.0) + `docs/narratives/README.md` count 7→8
- [ ] `docs/prompts/README.md` implementations count 34→35 (prompt 035 already committed)
- [ ] `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md` (spec-delta closure: 3 ADDED `stock-management` requirements landed)
- [ ] `openspec archive slices-2-5-2-7-replenishment-saga -y` — **post-merge tidy PR**, not this PR (per customer-data precedent)
