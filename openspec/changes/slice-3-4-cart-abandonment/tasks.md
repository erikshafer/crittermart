# Tasks: Slice 3.4 — Cart Abandonment

## 1. Verify before wiring (ctx7, numbered facts)

- [x] 1.1 `/jasperfx/wolverine` — scheduled-message cancellation: **NOT supported** (no API to cancel/remove a pending scheduled envelope). Verified at session start; shaped design Decision 1 (fire-and-check).
- [x] 1.2 `/jasperfx/marten` — async projection rebuild without a daemon: **supported** via `store.BuildProjectionDaemonAsync()` → `daemon.RebuildProjectionAsync<T>(ct)`; Marten's projection-testing docs demonstrate exactly this (register async, no daemon, rebuild, assert). Shaped design Decision 5.
- [x] 1.3 `/jasperfx/marten` — multi-stream date grouping: **supported** first-class via `Identity<IEvent<T>>(e => e.Timestamp.ToString("yyyy-MM-dd"))` in the projection constructor; no custom grouper needed. Shaped design Decision 4.

## 2. Events and messages

- [x] 2.1 `Cart/CartAbandoned.cs` — the fat terminal event: `{ Reason, Lines (List<CartLine>), TotalValue }` + `CartAbandonReason.InactivityTimeout` constant (mirror `CancelReason`).
- [x] 2.2 `Cart/CartActivityTimeout.cs` — the scheduled self-message `{ CartId }` + the `CartActivityDeadline(TimeSpan Duration)` config record (default 2 hours), mirroring `OrderPaymentTimeout` + `PaymentDeadline`.

## 3. The CartView fold gains time and a second terminal

- [x] 3.1 `Cart/CartView.cs` — add `LastActivityAt` property; switch the four activity Apply methods to `IEvent<T>` wrappers (timestamps fold into `LastActivityAt`); add `Apply(CartAbandoned)` → `IsOpen = false`, lines retained.
- [x] 3.2 `tests/CritterMart.Orders.Tests/CartViewProjectionTests.cs` — update existing fold tests to `Event<T>` wrappers; add `LastActivityAt` and abandoned-fold cases.

## 4. The abandonment automation

- [x] 4.1 `Cart/CartAbandonmentHandler.cs` — fire-and-check handler: `FetchForWriting<CartView>` → closed/unknown guard (no-op) → activity check (reschedule via cascaded `DeliveryMessage<CartActivityTimeout>`) → abandon (append fat `CartAbandoned`).
- [x] 4.2 `Features/AddToCart.cs` — return type gains `DeliveryMessage<CartActivityTimeout>?`; scheduled only on the cart-creation branch (subsequent adds cascade null).
- [x] 4.3 `Program.cs` — bind `Orders:CartActivityTimeout` → `CartActivityDeadline` singleton.

## 5. The todo-list projection (inline)

- [x] 5.1 `Cart/CartsAwaitingActivity.cs` — `CartAwaitingActivity` view (Id/CustomerId/Deadline) + `CartsAwaitingActivityProjection` (instance-registered, `IEvent<T>` folds advancing the deadline, `ShouldDelete` on both terminal events).
- [x] 5.2 `Features/AddToCart.cs` (same file as 4.2) — `GET /carts/awaiting-activity` endpoint, soonest deadline first.
- [x] 5.3 `Program.cs` (same file as 4.3) — register `CartsAwaitingActivityProjection` (instance, inline).
- [x] 5.4 `tests/CritterMart.Orders.Tests/CartsAwaitingActivityProjectionTests.cs` — pure fold tests (create/advance/delete).

## 6. The async report projection (rebuild-only)

- [x] 6.1 `Cart/CartAbandonmentReport.cs` — `CartAbandonmentDailyReport` doc (Id = `yyyy-MM-dd`, count, total value, per-SKU counts) + `CartAbandonmentReportProjection : MultiStreamProjection` with date-keyed `Identity<IEvent<CartAbandoned>>`.
- [x] 6.2 `Program.cs` (same file) — register with `ProjectionLifecycle.Async`; **no** `AddAsyncDaemon`.
- [x] 6.3 `tests/CritterMart.Orders.Tests/CartAbandonmentReportProjectionTests.cs` — pure fold tests (count/value/SKU accumulation; two same-day events fold into one doc).

## 7. Integration proof

- [x] 7.1 `tests/CritterMart.Orders.Tests/AddToCartTests.cs` — creating a cart schedules exactly one `CartActivityTimeout` (`tracked.Scheduled.SingleMessage<T>`); a subsequent add schedules none.
- [x] 7.2 `tests/CritterMart.Orders.Tests/CartAbandonmentTests.cs` — the abandonment paths by direct invocation (no real-time waits): inactive cart → abandoned + row removed + customer freed; activity intervened → rescheduled, no event; checked-out cart → no-op; already-abandoned cart (duplicate) → no-op.
- [x] 7.3 `CartAbandonmentTests.cs` — the rebuild test (mirror Marten's documented pattern): abandon carts → report is empty → `BuildProjectionDaemonAsync` + `RebuildProjectionAsync` → daily report materialized with correct count/value/SKUs.
- [x] 7.4 Full solution build + test run green (`dotnet test`), including Inventory/Catalog/CrossBc untouched suites.

## 8. Sibling artifacts

- [x] 8.1 `docs/narratives/004-customer-purchase.md` → v1.7: Moment 1B (the cart left behind) + frontmatter slices + Document History row.
- [x] 8.2 `docs/skills/DEBT.md` — the Marten-pattern third-use row (instance-registered projection / `IEvent<T>` fold / conditional delete, now 3× each; candidate local skill, deferred to a `tidy: skills` session).
- [x] 8.3 `docs/retrospectives/implementations/013-slice-3-4-cart-abandonment.md` — outcome, refinements, spec-delta confirmation.
- [x] 8.4 `openspec validate slice-3-4-cart-abandonment --strict` green; consolidated PR opened.
