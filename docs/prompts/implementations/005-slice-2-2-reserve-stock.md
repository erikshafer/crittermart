# Prompt: Implementations 005 — Slice 2.2 Reserve Stock (Inventory-side, one PR)

**Kind**: per-slice, consolidated (narrative extension + proposal + implementation, one PR)
**Files touched**: this prompt; `docs/narratives/003-operator-manage-stock.md` (→ v1.1); `openspec/changes/slice-2-2-reserve-stock/{.openspec.yaml, proposal.md, specs/stock-management/spec.md, design.md, tasks.md}` (new); `src/CritterMart.Inventory/Stock/StockReserved.cs` (new) + `StockLevelView.cs` (Apply); `src/CritterMart.Inventory/Features/ReserveStock.cs` (new); `tests/CritterMart.Inventory.Tests/ReserveStockTests.cs` (new); `docs/retrospectives/implementations/005-slice-2-2-reserve-stock.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; high-autonomy (act-on-leans); ctx7 as needed
**Commit subject(s)**: `docs: slice 2.2 narrative + reserve-stock proposal/design` + `feat: slice 2.2 reserve stock (Inventory-side)`

## Framing

Slice 2.2's workshop framing is "Inventory receives `ReserveStock` from Orders over RabbitMQ." **Orders does not exist yet**, so this slice implements the **Inventory-side reservation behavior** — the `StockReserved` event, the `available`/`reserved` projection math, and the insufficient-stock refusal — **triggered via an HTTP endpoint** for now (demoable: receive → reserve → `available` drops). The **cross-BC RabbitMQ delivery, the `StockReservationFailed` outbound message, publishing `StockReserved` back to Orders, and at-least-once duplicate-delivery guards are deferred to slice 4.2** (the Orders-side cross-BC reserve), when Orders exists. The reservation *logic* implemented here is what 4.2 will drive over RabbitMQ.

One-PR mode (memory `feedback-consolidate-slice-prs`); divergence from ADR 011 kept informal. Extends the existing `stock-management` capability + Narrative 003. No new service.

## Goal

`POST /stock/{sku}/reservations { orderId, quantity }` reserves stock against an order: appends `StockReserved` to the SKU's stream, and the inline `StockLevelView` shows `available` decremented + `reserved` incremented. Insufficient/absent stock is refused (409) with the stream unchanged. Proven by tests + a real run; `openspec validate --strict` passes.

## Spec delta

Narrative 003 → v1.1 (a reservation Moment, from Inventory's side). The `stock-management` capability gains a **Reserve stock** requirement (happy + insufficient scenarios). `StockReserved` is the Stock stream's second event kind (after `StockReceived`).

## Orientation

1. **Workshop 001** § 5 (slice 2.2 row), § 6.1 (2.2 GWT — happy: available 100 → reserve 2 → available 98/reserved 2; insufficient: stream **not** modified, refusal). § 4 (`StockReserved`).
2. **`src/CritterMart.Inventory/`** — slice 2.1 patterns: `StockReceived`/`StockLevelView`/`StockLevelViewProjection` (add `Apply(StockReserved)`), `ReceiveStock` endpoint (mirror for `ReserveStock` — `FetchForWriting`, but with a guard).
3. **`openspec/specs/stock-management/spec.md`** — the live capability the delta ADDs to (Receive stock is there from 2.1).

## Working pattern

1. This prompt; Narrative 003 → v1.1 (reservation Moment); openspec change `slice-2-2-reserve-stock` (proposal `stock-management` Modified Capability; spec ADDED Reserve stock + 2 scenarios; design + tasks). Validate `--strict`.
2. `StockReserved(string Sku, string OrderId, int Quantity)` event; `StockLevelView.Apply(StockReserved)` → `Available -= Quantity; Reserved += Quantity`.
3. `ReserveStock(string OrderId, int Quantity)` command + `POST /stock/{sku}/reservations`: `FetchForWriting<StockLevelView>(sku)`; if `Aggregate is null` or `Aggregate.Available < quantity` → `409` ProblemDetails (no append — stream unchanged); else `AppendOne(StockReserved)`.
4. Tests (reuse `InventoryAppFixture`): happy (receive 100, reserve 2 → `available 98/reserved 2`, `StockReceived`+`StockReserved` on stream); insufficient (receive 1, reserve 2 → `409`, stream has only `StockReceived`, view unchanged).
5. Verify: build + test green; real docker-compose run (receive then reserve; GET reflects 98/2; over-reserve → 409); `openspec validate --strict`. Retro.

## Out of scope

- **No RabbitMQ / cross-BC** — no `WithReference(rabbitmq)` consumption, no Wolverine RabbitMQ transport, no publishing `StockReserved`/`StockReservationFailed` to Orders. All slice 4.2.
- **No duplicate-delivery idempotency guard** (the workshop's third 2.2 scenario) — an at-least-once/messaging concern → slice 4.2. (No per-order tracking added to `StockLevelView`.)
- **No release** (slice 2.3). **No Orders service.** **No `openspec archive`.** **No `tidy: docs`.**
- Stay faithful to Workshop § 6.1's happy + insufficient paths.
