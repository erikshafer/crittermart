# Proposal: Enrich `OrderStatusView` — `placedAt` + cancellation reason

## Why

W4 (Order Status / Tracking) renders the order's `OrderStatusView`, but two things the wireframe drew are not yet *bound* to data. The shipped view shape is `{ id, customerId, status, lines, total }`: it carries **no placement timestamp** and **no cancellation reason**. So W4's `Placed 2026-06-14 14:02 UTC` line and its per-reason cancellation copy (`stock_unavailable` / `payment_declined` / `payment_timeout`) are *aspirational* — W4 (PR #64) shipped an honest generic "Cancelled" with no placed-at line and logged both as backend gaps.

The workshop already fenced the fix. The § 5.1 v1.10 amendment names a **future "enrich `OrderStatusView`" slice** — "surface a `placedAt` from `OrderPlaced` and a reason carried on `OrderCancelled`" — and Narrative 005 (v1.6) carries the same forward pointer. This change is that slice: a pure read-model enrichment, **no new event and no new command**.

The data already exists on the streams. `OrderCancelled` **already carries** its `Reason` (with a `CancelReason` constants class), but `OrderStatusView.Apply(OrderCancelled …)` drops it and folds to a bare `Status = "cancelled"` (`OrderStatusView.cs:44`). And the placement time is the append time of the genesis `OrderPlaced` event — already recorded by Marten as event metadata, the same `IEvent<T>.Timestamp` the Cart side already surfaces as `LastActivityAt` (`CartView.cs:23`). The enrichment reads both from what the streams already hold; it changes no event contract.

## What Changes

- `OrderStatusView` gains two fields: **`placedAt`** (a `DateTimeOffset`, set at genesis from the `OrderPlaced` event's append timestamp) and **`cancelReason`** (a nullable string, null until the order is cancelled, then carrying the `OrderCancelled` event's reason).
- The existing wire shape `{ id, customerId, status, lines, total }` is preserved as a **superset** — the two fields are *added*, nothing renamed or removed — so the W3 place-order read and the W4 tracking screen keep working unchanged while gaining the new data.
- `placedAt` is sourced from event **metadata** (`Create(IEvent<OrderPlaced> e)` → `e.Timestamp`), mirroring the established `CartView` convention; the `OrderPlaced` event payload is **not** changed.
- `cancelReason` folds the `Reason` the `OrderCancelled` event already carries — a one-line change to the existing `Apply(OrderCancelled …)`.
- W4 (`client/src/orders/`) binds the two new fields: a real placed-at timestamp and per-reason cancellation copy, replacing the hardcoded sketch / generic "Cancelled."

## Capabilities

### New Capabilities

*(none — the delta extends the existing `order-lifecycle` capability, per the one-capability-per-aggregate shape)*

### Modified Capabilities

- `order-lifecycle`: 1 ADDED requirement (*Surface placement time and cancellation reason in the order view*). The existing place-order and cancellation requirements are unchanged in behavior — the view's status, lines, and total still fold exactly as before; this requirement adds the two new read-model fields alongside them.

## Impact

- **Code**: `src/CritterMart.Orders/Ordering/OrderStatusView.cs` (add `PlacedAt` + `CancelReason`; `Create` takes `IEvent<OrderPlaced>`; `Apply(OrderCancelled …)` folds the reason). No change to `OrderPlaced`, `OrderCancelled`, any handler, or `Program.cs`.
- **Tests**: `tests/CritterMart.Orders.Tests/OrderProjectionTests.cs` (assert `placedAt` is set at genesis and `cancelReason` is null until cancellation, then carries the reason — across the three cancel routes).
- **Frontend**: `client/src/orders/` — extend the `OrderStatusView` zod schema with `placedAt` + `cancelReason`; render the timestamp and per-reason copy on W4. Vitest specs updated.
- **No cross-BC impact**, no contract changes, no broker topology changes, no new packages, no new projection or index. The enrichment stays inside Orders and inside the existing inline projection.
- **Out of band (post-merge tidy)**: workshop § 5.1 v1.10 amendment updated to record the enrichment as *shipped* (the timestamp + per-reason copy become bound, no longer aspirational); `openspec archive`.
