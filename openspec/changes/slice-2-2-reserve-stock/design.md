## Context

Slice 2.2 adds the Inventory-side reservation behavior to the existing event-sourced `Stock` aggregate. `StockReserved` is the stream's second event kind (after `StockReceived`). Triggered via HTTP for now; the cross-BC RabbitMQ delivery from Orders is slice 4.2. Inherits the event-sourcing + inline-projection approach from slice 2.1 (ADR 008) and Wolverine.Http (ADR 006).

## Goals / Non-Goals

**Goals:** reserve via `FetchForWriting` with an availability guard; `StockReserved` on the stream; the inline projection adjusts `available`/`reserved`.

**Non-Goals:** no RabbitMQ / cross-BC delivery, no `StockReservationFailed` outbound message, no publishing `StockReserved` back to Orders (all slice 4.2); no release (2.3); no duplicate-delivery idempotency guard (4.2 — an at-least-once concern; no per-order tracking added to `StockLevelView`).

## Decisions

### 1. Reserve via `FetchForWriting` with an availability guard

`POST /stock/{sku}/reservations`: `FetchForWriting<StockLevelView>(sku)`; if `stream.Aggregate is null` (no stock for the SKU) or `stream.Aggregate.Available < quantity` → `409` `ProblemDetails` (`InsufficientStock`) with **no append** (the stream stays unmodified, matching Workshop § 6.1's "not modified"); else `AppendOne(StockReserved)`. The guard reads the loaded aggregate's current `Available`.

### 2. `StockReserved` updates the projection

`StockLevelView.Apply(StockReserved)` → `Available -= Quantity; Reserved += Quantity`. Same inline `SingleStreamProjection` as slice 2.1; just another `Apply` overload.

### 3. Interim HTTP trigger; cross-BC deferred

`ReserveStock` is exposed via HTTP now so the reservation behavior is demoable without Orders. Slice 4.2 will route `ReserveStock` from Orders over RabbitMQ to the same reservation logic and publish `StockReserved` / `StockReservationFailed` back. The duplicate-delivery idempotency guard (Workshop's third 2.2 scenario) is meaningful only under at-least-once messaging → deferred to 4.2.

## Risks / Trade-offs

- **Interim HTTP trigger differs from the workshop's RabbitMQ delivery** → documented; the reservation logic transfers unchanged to 4.2.
- **No duplicate-delivery guard yet** → acceptable for an HTTP trigger (no at-least-once redelivery); 4.2 adds it for the messaging path.
- **`reserved` is now non-zero** → the slice 2.1 read-model placeholder (`Reserved`) becomes live; no migration (events replay into the same projection).
