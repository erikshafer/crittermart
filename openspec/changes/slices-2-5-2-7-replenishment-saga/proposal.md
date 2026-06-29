## Why

Today an Inventory shortfall is a dead end. When `ReserveStock` cannot fill a line, Inventory refuses the whole order (`StockReservationFailed`) and nothing else happens — there is no record that the SKU ran short and no process to get it restocked. Operationally that is a gap; pedagogically it is a missed opportunity. CritterMart's coordination story currently shows two patterns — Wolverine **cascading messages** (per-hop) and **Process Manager via Handlers** (the Order/Cart aggregates as their own process manager, ADR 007) — but **never the convention `Wolverine.Saga`**, which ADR 007 deliberately forgoes *for the Order*. Slices 2.5–2.7 close both gaps at once with a **`Replenishment` saga**: CritterMart's first convention saga, the third member of the "ways to wait for a deadline" trio (Bruun todo-list projection vs. PMvH-on-the-stream vs. saga-storage-doc), and the saga the team wants to exercise/observe in CritterWatch, demos, and the talk. Modeled in Workshop 001 v1.12 (slices 2.5–2.7); feasibility de-risked in `docs/research/wolverine-saga-feasibility.md`.

## What Changes

**Design A (saga-centric), chosen with Erik.** The saga *is* the backorder state: a Marten-stored saga document keyed by SKU, deleted on `MarkCompleted()`, **never event-sourced**. There are **no new `Stock` stream events** and **slice 2.2's refusal path is unchanged** — the saga is a separate, additive reaction to the same shortfall. Transient coordination state is exactly what a saga is for, and *not* event-sourcing it is the teaching point versus the Order's PMvH (state on the stream).

- **Inventory (2.5 — open).** On the `ReserveStock` refusal path, `ReserveStockHandler` additionally emits a `BackorderDetected { sku, shortfall }` for each short SKU (the concrete saga-start message — the workshop modeled the trigger as "on `ReserveStock` shortfall," the same way slices 2.3/2.4 introduced concrete `ReleaseStock`/`CommitStock` names beyond the model). A `BackorderDetected` **starts** a `Replenishment` saga (`Replenishment.Start`) keyed by the SKU when none is open — recording `Outstanding`, returning a `RequestRestock { sku, quantity }` (supplier-notification stub), and scheduling a `ReplenishTimeout { sku }`. When a saga is already open for the SKU, `BackorderDetected` updates `Outstanding` to `max(current, shortfall)` (see decision below) without starting a second saga.
- **Inventory (2.6 — resolve).** `ReceiveStockHandler` (slice 2.1) additionally publishes a `RestockArrived { sku, quantity }` message on every receipt. An open `Replenishment` saga consumes it: if the quantity covers `Outstanding`, the saga calls `MarkCompleted()` (state deleted); if it partially covers, the saga reduces `Outstanding` and stays open. No open saga for the SKU → silent no-op (saga not-found, the common case). The receipt's `StockReceived` event is unchanged.
- **Inventory (2.7 — escalate).** When `ReplenishTimeout` fires and the saga is still open, it escalates (an operator-facing "unreplenished SKU" alert) and calls `MarkCompleted()`. A timeout delivered after the saga already resolved is a silent no-op (saga not-found) — Wolverine has no scheduled-message cancellation API, the same property slices 3.4/4.7 rely on.
- **Persistence / wiring.** `Replenishment : Saga` is a Marten document type (saga storage drops onto Inventory's existing `IntegrateWithWolverine()`; no daemon — Event **subscriptions** are out per ADR 008, but the saga needs none). `[SagaIdentity]` on the SKU-bearing property of `RestockArrived` / `ReplenishTimeout` correlates them to the saga instance. `BackorderDetected`, `RequestRestock`, and `RestockArrived` are Inventory-local messages (no cross-BC contract; they never leave the service), routed in-process.

### Open-question resolutions (Workshop 001 §8 items 15–19 — recommended, open to redline)

- **#15 restock delivery:** a **dedicated `RestockArrived` message** emitted by `ReceiveStockHandler`, not Marten→Wolverine forwarding of the raw `StockReceived` (avoids annotating a domain event; keeps the daemon-free posture).
- **#16 shortfall aggregation:** `Outstanding := max(current, shortfall)` — chosen for **idempotency** (stable under at-least-once redelivery, where `sum` would double-count).
- **#17 partial restock:** reduce `Outstanding`, stay open, **do not** re-issue `RequestRestock`.
- **#18 timeout policy:** **escalate-and-complete** (mirrors slice 4.7's terminal behavior; re-arm risks an immortal saga).
- **#19 fulfillment:** `RequestRestock` is a logged/published **supplier-notification stub**, satisfied by the Operator's existing `ReceiveStock` path; a configurable auto-restock demo lever (à la `Payment:AuthDelay`) is **out of scope** here, noted for a later demo-affordance slice.

## Capabilities

### New Capabilities

(None — Inventory's single capability `stock-management` gains new behavior; the saga is not a new capability, per one-capability-per-aggregate.)

### Modified Capabilities

- `stock-management`: Inventory gains a replenishment process. A shortfall now opens a `Replenishment` saga (CritterMart's first convention `Wolverine.Saga`) that requests a restock, resolves on a covering receipt, and escalates on a timeout — saga state in saga storage, never on the `Stock` stream, slice 2.2 unchanged. (Three ADDED requirements: open on shortfall, resolve on restock, escalate on timeout.)

## Impact

- **Inventory.** New `Stock/Replenishment.cs` (`Replenishment : Saga`, `Id = Sku`, `Outstanding`). New Inventory-local messages: `Stock/BackorderDetected.cs`, `Stock/RequestRestock.cs`, `Stock/RestockArrived.cs`, `Stock/ReplenishTimeout.cs` (the last a `TimeoutMessage`). `ReserveStockHandler` (`Features/ReserveStock.cs`) emits `BackorderDetected` on the refusal path (additive; the existing return to Orders is unchanged). `ReceiveStockHandler` (`Features/ReceiveStock.cs`) publishes `RestockArrived` on every receipt. A small `RequestRestock` handler (the supplier-notification stub) and an operator-alert sink for escalation.
- **Persistence.** `Replenishment` registered as a Marten document (saga storage). No async daemon, no new projection, no new `Stock` stream event. `StockLevelView` is unchanged.
- **Contracts / topology.** **No** new `CritterMart.Contracts` types and **no** cross-BC message — every saga message is Inventory-local. No new broker exchanges/queues beyond what conventional routing derives in-process.
- **Observability.** CritterWatch gains its first saga: a distinct saga store, an auto-scheduled `TimeoutMessage`, and saga-lifecycle correlation become observable — the original motivation.
- **Tests.** Saga unit tests over `Replenishment` (pure `Start`/`Handle` methods: open, max-update idempotency, partial restock, cover-and-complete, escalate-and-complete); Inventory integration for the shortfall→open and receipt→resolve wiring (Alba/Marten); a not-found no-op test for `RestockArrived`/`ReplenishTimeout` against an absent saga.
- **Downstream artifacts (impl session):** `design.md` + `tasks.md` in this change; a narrative (extend `narratives/003-operator-manage-stock.md` or a new `008-operator-replenish-backorders.md`); Workshop 001 is already at v1.12; prompt `docs/prompts/implementations/035-…` + a retro.
- **Out of scope.** The Identity email-change saga (saga #2, gated on an ADR-009 revisit); the auto-restock demo lever (#19).
