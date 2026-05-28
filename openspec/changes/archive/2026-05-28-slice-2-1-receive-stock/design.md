## Context

The first event-sourced bounded context and a new `CritterMart.Inventory` service. Each SKU has a `Stock` event stream (`StreamIdentity.AsString`); an **inline** `StockLevelView` snapshot projects the current level. `ReceiveStock` is a create-or-append genesis operation. Cross-cutting decisions inherited by reference: schema-per-service (ADR 002), inline projections / no async daemon (ADR 008), Wolverine.Http (ADR 006), stack + codegen posture (ADR 012). Authored in the consolidated one-PR slice mode.

## Goals / Non-Goals

**Goals:** `ReceiveStock` appends `StockReceived` (creating the stream on first receipt); an inline `StockLevelView` projects available stock; `GET /stock/{sku}` reads it.

**Non-Goals:** no reserve (2.2) or release (2.3); no cross-BC / RabbitMQ; no async daemon. `StockLevelView.Reserved` exists for read-model shape but is never set in this slice (stays `0`).

## Decisions

### 1. `StockLevelView` as an inline single-stream projection (separate `partial` class)

`StockLevelView` is a plain document (`Id` = sku, `Available`, `Reserved`). A `partial class StockLevelViewProjection : SingleStreamProjection<StockLevelView, string>` with `Apply(StockReceived e, StockLevelView view) => view.Available += e.Quantity`, registered `ProjectionLifecycle.Inline` (ADR 008).

*Why X over Y:* the Marten 9 source-generator-friendly pattern is the `partial` `SingleStreamProjection<T, TId>` subclass (runtime codegen was removed in Marten 9). Marten constructs the empty view for the genesis event and sets its `Id` to the stream key (the sku), so `Apply` only accrues quantity. Keeps the read-model doc a plain POCO. (Self-aggregating `Apply` on the doc itself is the alternative; the separate projection class is the clearer, demonstrably-Marten-9 form.)

### 2. `ReceiveStock` via explicit `FetchForWriting` (create-or-append)

`POST /stock/{sku}/receipts`; handler: `session.Events.FetchForWriting<StockLevelView>(sku)` → `AppendOne(new StockReceived(sku, quantity))`; committed in one transaction by `AutoApplyTransactions`.

*Why X over Y:* `ReceiveStock` must handle **both** the first receipt (create the stream) and later receipts (append). `[Aggregate]` 404s when the stream is missing (can't create); `MartenOps.StartStream` throws when it already exists. `FetchForWriting` handles both transparently and needs only `WolverineFx.Marten` — no `WolverineFx.Http.Marten`. (The skill flags manual `FetchForWriting` as an anti-pattern *when `[Aggregate]` would suffice* — here it would not.)

### 3. `GET /stock/{sku}` reads the inline snapshot

Explicit `IQuerySession.LoadAsync<StockLevelView>(sku)`; null → `404`. The inline snapshot persists as a queryable document. (Demo + test-read convenience; the slice 2.1 GWT asserts the view state.)

## Risks / Trade-offs

- **Marten 9 projection registration / namespaces** (`SingleStreamProjection`, `ProjectionLifecycle`/`SnapshotLifecycle`) → verified via ctx7 and the build.
- **`StockLevelView.Reserved` is a placeholder (`0`)** until slice 2.2 — the read-model shape matches the workshop now; the reserve path fills it later.
- **Workshop § 6.1 scenario 2's reservation precondition deferred to 2.2** (see the proposal's faithfulness note); this slice tests receive-accumulation (`available 150`).
