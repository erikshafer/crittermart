# Tasks: Enrich OrderStatusView — placedAt + cancellation reason

## 1. Backend — the projection

- [x] 1.1 `OrderStatusView.cs` — add `DateTimeOffset PlacedAt` and `string? CancelReason` to the record; change `Create(OrderPlaced e)` → `Create(IEvent<OrderPlaced> e)` reading `e.Timestamp` for `PlacedAt` (and `e.Data` for the existing fields), mirroring `CartView`; change `Apply(OrderCancelled e, view)` to fold `CancelReason = e.Reason` alongside `Status = cancelled`. Other folds unchanged. No edit to `OrderPlaced`, `OrderCancelled`, handlers, or `Program.cs`.

## 2. Backend — the proof

- [x] 2.1 `OrderProjectionTests.cs` — assert `PlacedAt` is set at genesis (equals the `OrderPlaced` event timestamp) and survives later folds; assert `CancelReason` is null pre-cancellation and carries the reason across all three cancel routes (`stock_unavailable`, `payment_declined`, `payment_timeout`); assert a confirmed order keeps `CancelReason` null.
- [x] 2.2 Full backend suite green (`dotnet test`); Catalog / Inventory / CrossBc untouched.

## 3. Frontend — W4 binding

- [x] 3.1 `client/src/orders/` — extend the `OrderStatusView` zod schema with `placedAt` (datetime) + `cancelReason` (nullable); render the placed-at timestamp and per-reason cancellation copy on the W4 tracking screen, replacing the hardcoded line / generic "Cancelled". Update vitest specs.
- [x] 3.2 `client/` build + vitest green (local — no frontend CI job yet).

## 4. Live proof

- [x] 4.1 Boot the stack (LIVE BOOT RITUAL), seed a place→confirm and a cancel route, confirm W4 renders the real placed-at + the right per-reason copy; tear down.

## 5. Sibling artifacts

- [x] 5.1 `docs/narratives/005-customer-storefront.md` — bump version + Document History row (W4 placed-at + per-reason copy now bound).
- [x] 5.2 `docs/prompts/implementations/025-enrich-order-status-view.md` (frozen at session start) + `docs/retrospectives/implementations/025-enrich-order-status-view.md` (outcome, spec-delta closure, deferred-state call-outs).
- [x] 5.3 `openspec validate enrich-order-status-view --strict` green; consolidated PR opened.
- [x] 5.4 Post-merge tidy (NOT this PR): workshop § 5.1 v1.10 amendment → record placedAt + per-reason copy as shipped; `openspec archive`.
