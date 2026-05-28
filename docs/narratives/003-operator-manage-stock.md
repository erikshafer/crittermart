---
narrative: 003
title: The Operator Manages Stock
actor: Operator
status: draft
version: v1.1
slices: [2.1, 2.2]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 2 Inventory BC, § 4 Inventory vocabulary, § 5 slice 2.1, § 6.1 GWT)
  - docs/context-map/README.md (Inventory is the supplier in the Orders↔Inventory Customer-Supplier relationship)
  - docs/narratives/001-seller-manage-catalog.md (the same one-person operator, here in their stock-keeping role)
---

# Narrative 003 — The Operator Manages Stock

The Operator in CritterMart is the same one-person business owner who runs the Catalog as the *Seller* (Narrative 001) — here wearing their stock-keeping hat. When a shipment of critter merchandise arrives at the door, they record it against the right SKU so the storefront knows how many are on hand. Where the Catalog is a document store (the "CRUD is fine" example), **Inventory is event-sourced**: each SKU has a `Stock` stream, and stock levels are not stored as a mutable number but **derived from the events** that happened to that SKU — received, reserved, released. Workshop 001 § 2 calls this the textbook event-sourcing case.

Workshop 001 names this actor the *operator*; this narrative uses *Operator* — the terms coincide, so no bridge is needed (unlike Narrative 001's *Seller*/*operator*).

## Journey scope

The Operator's stock-management journey threads the Inventory slices:

- **Slice 2.1 — Receive stock.** One Moment: recording a stock receipt.
- **Slice 2.2 — Reserve stock.** Authored in this version (v1.1). Moment 2 below: stock being reserved against an order.

Forthcoming (not authored here): **releasing** a reservation on cancellation (slice 2.3, `StockReleased`), and the **cross-bounded-context delivery** — Orders sending `ReserveStock` over RabbitMQ and consuming Inventory's `StockReserved` / `StockReservationFailed` response (slice 4.2). This slice implements the reservation *behavior*; the Orders↔Inventory wiring lands with the Orders BC.

## Moment 1 — Recording a stock receipt

**Context.** A carton of *Cosmic Critter Plush* (SKU `crit-001`) arrives — 100 units. The storefront already lists the product (the Seller published it, Narrative 001), but Inventory has never tracked stock for `crit-001`: there is no `Stock` stream for it yet.

**Interaction.** The Operator records the receipt: `ReceiveStock { sku: "crit-001", quantity: 100 }`.

**System response.** The Inventory service appends a `StockReceived { quantity: 100 }` event to `crit-001`'s `Stock` stream — **creating the stream**, since this is the first event for that SKU. An inline `StockLevelView` snapshot is updated in the same transaction and now shows `available: 100, reserved: 0`. The available quantity is not a stored field the Operator set; it is **projected from the event**. When the next carton arrives — say 50 more — a second `StockReceived { quantity: 50 }` appends to the same stream and the view recomputes to `available: 150` (or, if 30 were already reserved against an order, `available: 120, reserved: 30`). Nothing is overwritten; the stream is the history, and the view is its current rollup.

This is the contrast the talk draws: Catalog *stores* a product and keeps events for audit; Inventory *is* its events, and the readable stock level is a projection of them.

## Moment 2 — Stock is reserved against an order

**Context.** The `crit-001` Stock stream shows `available: 100, reserved: 0` (from Moment 1's receipt). A customer places an order for 2 units.

**Interaction.** The order reserves stock: `ReserveStock { orderId: "ord-A", sku: "crit-001", quantity: 2 }`.

**System response.** Inventory checks the current `StockLevelView` — 2 of the 100 available — and, since there is enough, appends a `StockReserved { orderId: "ord-A", quantity: 2 }` event to `crit-001`'s Stock stream. The inline projection recomputes to `available: 98, reserved: 2`. No physical stock moved; the event records that 2 units are now **committed** to `ord-A`. Had the order asked for more than is available, Inventory refuses — **no `StockReserved` is appended, the stream is unchanged**, and the reservation is rejected. Reserved units stay reserved until the order completes or is cancelled (release — slice 2.3).

The trigger here is a direct reservation call to Inventory. Once the Orders context exists, checkout will send `ReserveStock` to Inventory over RabbitMQ and consume the `StockReserved` / `StockReservationFailed` response (slice 4.2); the reservation behavior in this Moment is exactly what that cross-BC flow drives.

## What the Operator does *not* yet see

- **No release yet, and no cross-context delivery yet.** Releasing a reservation on cancellation (`StockReleased`, slice 2.3) is not built, and the reservation here is triggered directly rather than by Orders over RabbitMQ (slice 4.2). No cross-context traffic in this slice.
- **No low-stock alerts, reorder suggestions, or supplier integration.** The Operator records what physically arrived; the system draws no inferences. Those are long-road concerns, out of round one.

## Document History

| Version | Date       | Notes                                                                                                                                                                       |
| ------- | ---------- | -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-28 | Initial commit. Covers Workshop 001 slice 2.1 (Receive stock) as one Moment: recording a receipt against a SKU's event-sourced `Stock` stream, with the inline `StockLevelView` snapshot projecting the available quantity. No failure path (the workshop's 2.1 GWT is happy-only). Reserve/release (2.2/2.3) noted as forthcoming. First event-sourced-BC narrative. |
| v1.1    | 2026-05-28 | Threads Workshop 001 slice 2.2 (Reserve stock). Adds Moment 2: stock reserved against an order (`StockReserved`, the stream's second event kind; `available` → 98 / `reserved` → 2), with insufficient stock refused (stream unchanged). `slices` → `[2.1, 2.2]`. Scoped to the Inventory-side reservation behavior, triggered via HTTP; the cross-BC RabbitMQ delivery (Orders → Inventory) is slice 4.2. |
