## Why

Inventory is CritterMart's **event-sourced** bounded context — the textbook contrast to Catalog's document store (Workshop 001 § 2). Stock levels are not a stored mutable number; they are **derived from events** on a per-SKU `Stock` stream. Workshop 001 slice 2.1 (`ReceiveStock`) is the genesis operation: recording stock arrivals. Narrative 003 (the Operator's stock journey) is this proposal's human-readable sibling. This is the **first event-sourced aggregate and inline projection** in the project.

## What Changes

- Introduce the `ReceiveStock` command: the Operator records a stock receipt for a SKU.
- Append a `StockReceived` event to the SKU's `Stock` stream — **creating the stream on the first receipt**, appending on subsequent receipts.
- An **inline** `StockLevelView` snapshot projects the available quantity per SKU from the stream (per ADR 008; the read model's `reserved` field exists but stays `0` until reservations land in slice 2.2).
- Expose the level via a read-only `GET /stock/{sku}`.
- No cross-bounded-context traffic, no reservations (2.2) or releases (2.3).

## Capabilities

### New Capabilities

- `stock-management`: tracking physical stock per SKU as an event-sourced `Stock` stream with an inline `StockLevelView` read model. Slice 2.1 introduces **receive**; later slices add reserve (2.2) and release (2.3). Inventory's first capability — parallel to Catalog's `product-catalog`, one capability per bounded context.

### Modified Capabilities

<!-- None. -->

## Impact

- **New service:** `CritterMart.Inventory` (the second service), Marten **event store** on the shared PostgreSQL under an `inventory` schema (ADR 002), `StreamIdentity.AsString` (streams keyed by SKU).
- **First event sourcing in the codebase:** an inline single-stream projection (`StockLevelView`, `SnapshotLifecycle`/`ProjectionLifecycle.Inline` per ADR 008) — no async daemon.
- **HTTP surface:** `POST /stock/{sku}/receipts` (`ReceiveStock`) and `GET /stock/{sku}` (read the view); no synchronous service-to-service calls; no RabbitMQ in this slice.
- **Faithfulness note:** Workshop 001 § 6.1's *second* 2.1 scenario has a pre-existing `StockReserved { 30 }` (→ `available 120, reserved 30`). Reservations are slice 2.2, so this proposal's second scenario covers the receive-accumulation behavior without a reservation (`available 150, reserved 0`); the reserved variant is deferred to slice 2.2 when `StockReserved` exists.
- **Downstream artifacts:** `design.md` + `tasks.md` are authored in this same PR (consolidated one-PR slice mode; an informally-kept divergence from ADR 011's session split, recorded in the retro).
