---
retrospective: 005
kind: implementations
prompt: docs/prompts/implementations/005-slice-2-2-reserve-stock.md
deliverable: docs/narratives/003-operator-manage-stock.md (→ v1.1); openspec/changes/slice-2-2-reserve-stock/{proposal.md, specs/stock-management/spec.md, design.md, tasks.md} (new); src/CritterMart.Inventory/Stock/StockReserved.cs (new) + StockLevelView.cs (Apply); src/CritterMart.Inventory/Features/ReserveStock.cs (new); tests/CritterMart.Inventory.Tests/ReserveStockTests.cs (new); docs/prompts/implementations/005-...; docs/retrospectives/implementations/005-... (this file)
date: 2026-05-28
mode: solo, consolidated one-PR slice; high-autonomy (act-on-leans)
session-runner: Claude (Opus 4.7)
---

# Retrospective — Implementations 005: Slice 2.2 Reserve Stock (Inventory-side)

## Outcome summary

Implemented the **Inventory-side reservation behavior** for slice 2.2: `ReserveStock` appends a `StockReserved` event (the `Stock` stream's second event kind) when stock is available, and the inline `StockLevelView` decrements `available` / increments `reserved`; insufficient or absent stock is refused (`409`) with the stream left unmodified. Triggered via `POST /stock/{sku}/reservations` (interim). Narrative 003 → v1.1 (Moment 2); `stock-management` gains a **Reserve stock** requirement (happy + insufficient scenarios), passing `--strict`. Five Inventory tests pass; real run confirmed (reserve → 204 with `reserved: 2`; over-reserve → 409). **Scoped deliberately:** the cross-BC RabbitMQ delivery from Orders, the `StockReservationFailed` outbound message, publishing `StockReserved` back to Orders, and the at-least-once duplicate-delivery guard are **deferred to slice 4.2** (Orders doesn't exist yet); release is slice 2.3.

## What worked

- **Scoping to the Inventory side kept it quick.** The reservation *behavior* (event, projection math, refusal) is fully implementable and demoable without Orders or RabbitMQ. The workshop's "from Orders over RabbitMQ" framing is the *delivery*, which is genuinely slice 4.2; separating behavior from delivery let 2.2 ship fast.
- **`FetchForWriting` gives the guard for free.** `stream.Aggregate` is the current `StockLevelView`, so the availability check (`null` or `Available < quantity`) reads live projected state before appending — and returning `409` before `AppendOne` leaves the stream unmodified exactly as § 6.1 requires.
- **The slice-2.1 read-model placeholder paid off.** `StockLevelView.Reserved` was modeled (always 0) in 2.1; 2.2 just made it live via `Apply(StockReserved)` — no view migration, the events replay into the same projection.
- **Pattern reuse** (StockReceived/projection/endpoint/fixture from 2.1) made this a small, fast slice.

## What was harder than expected

- **Narrative fit.** Reservation is triggered by an *order* (Orders BC), not the Operator, so it doesn't cleanly belong to the Operator's stock-management journey (Narrative 003). Resolved by framing Moment 2 as Inventory's *view* of reservation (stock committed to an order; available drops), with an explicit note that the trigger is a direct call now and becomes Orders-over-RabbitMQ in 4.2. The cross-BC customer/order journey gets its own narration when Orders lands.
- **Real-run number looked wrong (`available: 248`).** The docker-compose volume persisted receipts across prior runs, so the absolute available was inflated; the *relative* change (−2 available, +2 reserved) was correct, and the clean-data tests proved the exact `98/2`. Reminder: `docker compose down -v` for a clean demo state.

## Methodology refinements that emerged

1. **Behavior-vs-delivery split is a clean way to slice cross-BC work when one side doesn't exist yet.** Implement the receiving side's behavior + a local trigger now; defer the transport + the other BC to the slice that owns them. Keeps momentum without faking the absent service.
2. **Model read-model fields ahead of the slice that fills them** (the `Reserved` placeholder) — it avoids a later projection/view migration.

## Outstanding items / next-session inputs

1. **Slice 4.2 (cross-BC reserve)** — the deferred half: stand up Orders, wire the Wolverine RabbitMQ transport (Orders sends `ReserveStock`, Inventory publishes `StockReserved`/`StockReservationFailed` back), and add the at-least-once duplicate-delivery guard. This is where the Aspire distributed-trace demo lights up (Orders → RabbitMQ → Inventory).
2. **Slice 2.3 (release stock)** — `StockReleased` on `OrderCancelled`; the `reserved`→`available` compensation.
3. **`openspec archive`** — 1.3, 2.1, 2.2 now complete-but-unarchived. **`tidy: docs`** debt + **NuGet source mapping** unchanged.
4. **Duplicate-delivery guard** would need per-order reservation tracking on the aggregate (not added — deferred to 4.2 with the messaging path).

## Spec-delta — landed?

**Yes.** Narrative 003 → v1.1 (Moment 2); the `stock-management` Reserve-stock requirement (happy + insufficient) is satisfied by code, proven by tests + a real run; `StockReserved` is the stream's second event kind; `--strict` passes. Cross-BC delivery + duplicate guard explicitly deferred to 4.2 (recorded, not dropped).

## Process notes

- One PR: `docs:` (narrative v1.1 + proposal/design/tasks + prompt + retro) and `feat:` (event + projection Apply + endpoint + tests). Branch `feat/slice-2-2-reserve-stock` (created before committing).
- High-autonomy per the user's act-on-leans direction; scoping decision (behavior now, cross-BC at 4.2) made and proceeded without gating.
