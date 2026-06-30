---
narrative: 008
title: The Operator Replenishes Backorders
actor: Operator
status: draft
version: v1.0
slices: [2.5, 2.6, 2.7]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 4 saga state and saga messages, § 6 slices 2.5–2.7, § 8 items 15–19)
  - openspec/changes/slices-2-5-2-7-replenishment-saga/ (sibling OpenSpec proposal + stock-management SHALL delta)
  - docs/narratives/003-operator-manage-stock.md (the same Operator; this picks up the "no reorder suggestions yet" deferral 003 named)
  - docs/decisions/007-process-manager-via-handlers-for-order.md (the PMvH contrast — why the Order does not use a saga)
  - docs/research/wolverine-saga-feasibility.md (the convention-saga feasibility spike)
---

# Narrative 008 — The Operator Replenishes Backorders

The Operator is the same one-person business owner from Narrative 003 (managing stock) and Narrative 001 (the Seller). In 003 the journey ended honestly incomplete: *"No low-stock alerts, reorder suggestions, or supplier integration. The Operator records what physically arrived; the system draws no inferences."* This narrative is part of that deferral coming due. When an order cannot be filled because a SKU ran short, the system no longer just refuses and forgets — it opens a **replenishment process** that chases the restock and nags if it never comes.

That process is a **`Replenishment` saga**: CritterMart's *first* `Wolverine.Saga`. Everywhere else the storefront coordinates work with cascading messages (per hop) or with **Process Manager via Handlers** — the `Order` aggregate is its own process manager, its state living on its event stream (ADR 007). The saga is the deliberate third pattern, and the contrast *is* the teaching point: where the Order keeps its in-flight state **on the stream**, the `Replenishment` saga keeps its state in **saga storage** — a small Marten document keyed by SKU, created when the backorder opens and **deleted** when it resolves. Transient coordination state that exists only until the restock arrives is exactly what a saga is for, and *not* event-sourcing it is the point.

## Journey scope

The Operator's replenishment journey threads three Inventory slices:

- **Slice 2.5 — Open.** A reservation shortfall opens a replenishment.
- **Slice 2.6 — Resolve.** A covering receipt closes it; a partial receipt narrows it.
- **Slice 2.7 — Escalate.** A deadline with the SKU still short raises an operator alert.

## Moment 1 — A shortfall opens a replenishment

**Context.** SKU `crit-001` (*Cosmic Critter Plush*) shows `available: 1`. A customer's order asks for 2.

**Interaction.** Inventory handles the order's `ReserveStock` and finds `crit-001` short.

**System response.** The reservation is refused exactly as before (Narrative 003, Moment 2): no `Stock` stream is modified and a `StockReservationFailed` goes back to Orders — that behavior is untouched. **Additionally**, Inventory emits a `BackorderDetected { sku: "crit-001", shortfall: 1 }`, which **opens a `Replenishment` saga** keyed by `crit-001` with `outstanding: 1`. Opening the saga does two things: it sends a `RequestRestock { sku: "crit-001", quantity: 1 }` — a supplier-notification *stub* (here, a logged signal; the Operator's own receiving dock is what actually fulfils it) — and it schedules a `ReplenishTimeout` for the SKU. The saga's state lives in saga storage, **not** on the `crit-001` stream; the stream still shows only what physically happened (the earlier receipts). If a *second* order also finds `crit-001` short while the saga is open, the saga simply raises `outstanding` to the greater of the two shortfalls — it does not open a second saga or schedule a second deadline.

## Moment 2 — A covering restock resolves it

**Context.** The `Replenishment` saga for `crit-001` is open with `outstanding: 1`.

**Interaction.** A carton arrives and the Operator records it: `ReceiveStock { sku: "crit-001", quantity: 100 }` — the same receipt action as Narrative 003, Moment 1.

**System response.** The receipt behaves exactly as slice 2.1 specifies: a `StockReceived { quantity: 100 }` appends to the `crit-001` stream and the inline `StockLevelView` recomputes `available`. **Additionally**, the receipt announces itself with a `RestockArrived { sku: "crit-001", quantity: 100 }`. The open saga sees that 100 covers its outstanding 1, marks itself complete, and its state is **deleted** from saga storage — the backorder is closed. Had only a few units arrived (say 4 against an outstanding 10), the saga would reduce `outstanding` to 6 and **stay open**, waiting for more — without re-pestering the supplier. A `RestockArrived` for a SKU with no open saga — the ordinary case, since most receipts are routine restocking — is a silent no-op.

## Moment 3 — An unreplenished SKU escalates

**Context.** The `Replenishment` saga for `crit-001` is open, and no covering restock arrives before its deadline.

**Interaction.** None from the Operator — this Moment is driven by time, not a command. The scheduled `ReplenishTimeout` fires.

**System response.** The saga is still open, so it **escalates**: it publishes a `ReplenishmentEscalated { sku: "crit-001" }` — an operator-facing "this SKU went unreplenished" alert that flows on the bus (and so is visible on the CritterWatch console, not just in a log) — and then marks itself complete and is deleted. The deadline is configurable (`Inventory:ReplenishTimeout`), short for a live demo and long in production, mirroring how the payment window is tuned in Orders. If the restock *had* arrived first (Moment 2), the saga would already be gone, and the late-firing timeout finds nothing and does nothing — a silent no-op, because the messaging runtime offers no way to cancel an already-scheduled message (the same property the Order's payment- and cart-timeouts rely on).

## The third way to wait

CritterMart now shows three ways a process waits for something to happen later, side by side:

- **Bruun temporal projection** — a read model that lists what is due (e.g., orders awaiting payment).
- **Process Manager via Handlers** — the `Order` aggregate guards on its own stream state when a timeout fires (ADR 007).
- **The `Replenishment` saga** — a purpose-built coordinator whose whole state is the wait itself, in saga storage, gone the moment the wait ends.

The Operator never sees which mechanism is used; the distinction is for the people *building* CritterMart, and for the talk.

## What the Operator does *not* yet see

- **No automatic restocking.** `RequestRestock` is a stub: it notifies, it does not order. A configurable auto-restock demo lever (a supplier that fills the order after a delay, à la the payment-decline toggle) is noted for a later demo-affordance slice (Workshop § 8 item 19), not built here.
- **No second saga yet.** The Identity email-change-confirmation saga — the EF-Core-backed counterpart that proves the saga store is swappable — is a separate journey, gated on revisiting ADR 009.
- **No new cross-context traffic.** Every replenishment message is Inventory-local; nothing about this journey crosses a bounded-context boundary or changes the context map.

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-06-30 | Initial commit. Covers Workshop 001 slices 2.5 (open), 2.6 (resolve), 2.7 (escalate) as three Moments of the Operator's replenishment journey, threaded around CritterMart's first convention `Wolverine.Saga`. Picks up Narrative 003's explicitly-deferred "reorder/supplier" thread. Draws the saga-vs-PMvH (ADR 007) contrast — saga state in saga storage vs. state on the Order stream — and names the "third way to wait" trio. Sibling of the `slices-2-5-2-7-replenishment-saga` OpenSpec change. Out-of-scope deferrals (auto-restock lever, Identity saga) named, not built. |
