## Context

Slice 4.6 closes the payment gate's failure branch that slice 4.3 deliberately left open. A declined payment is recorded (`PaymentAuthFailed`) but the order's status stays `stock_reserved` and its reserved stock stays held; 4.6 makes the order terminal (`OrderCancelled { reason: "payment_declined" }`) and releases the stock. Because stock *was* reserved (the payment gate is reached only after the stock gate closed), this is the project's first **compensating** cross-BC hop — and the first cancellation that crosses a BC boundary *back*. It **bundles Inventory slice 2.3** (release) because 4.6 cannot be verified end-to-end without it.

One design point was resolved with the user this session (the cross-BC message-shape fork, Decision 1). The scope (one consolidated PR; 4.7 out) and guard placement (mirror 4.2/4.5) forks were confirmed as the standing leans.

## Goals / Non-Goals

**Goals:**
- Orders: `PaymentAuthFailed` at the payment gate → append `OrderCancelled { reason: "payment_declined" }` (status `cancelled`) **and** cascade one `ReleaseStock { orderId, lines }` to Inventory.
- Inventory: on `ReleaseStock`, release each line's reservation on its SKU stream (`StockReleased`), restoring available/reserved and dropping the order from `Reservations`; idempotent per-SKU no-op when no reservation is held.
- Idempotency via stream-state guards on both sides (Orders: payment gate; Inventory: reservation presence per SKU).
- Tracked-session tests both sides + a cross-BC smoke for the decline→release round-trip.

**Non-Goals:** payment timeout (4.7) + `OrderPaymentTimeout` scheduling + `OrdersAwaitingPayment*`. Committing reserved stock on confirmation (no `StockCommitted`; reserved stock stays reserved on `OrderConfirmed` — a Workshop § 8 future-ADR candidate). No async daemon / Marten event subscriptions (ADR 008). No real payment integration (stubbed — vision.md).

## Decisions

### 1. Cross-BC message is a `ReleaseStock` command, not a published `OrderCancelled` event (resolved with the user)

Resolved at this session's message-shape fork. The release is carried as **`CritterMart.Contracts.ReleaseStock { orderId, lines: [{ sku, quantity }] }`** — symmetric with `ReserveStock`. Chosen over the Workshop's literal wording (§ 2.3 and § 4.6 both say Orders *publishes `OrderCancelled { orderId }`* and Inventory consumes it) for three reasons:

- **Inventory must know which SKUs and quantities to release.** The Workshop's `OrderCancelled { orderId }` carries only the order id, and `StockLevelView.Reservations` stores only order-id strings (no per-order SKU/qty map) — Inventory cannot look the lines up. Slice 4.2 already hit this exact wall and refined `ReserveStock` to carry the order's lines (4.2 design Decision 2); `ReleaseStock` carries the same shape for the same reason.
- **Anti-corruption / published language (ADR 014).** Inventory's wire vocabulary stays about *stock* (`ReserveStock` / `ReleaseStock`), not about *orders*. Orders translates its own cancellation into a stock request at its boundary, rather than leaking an Orders concept ("an order was cancelled") into Inventory.
- **Symmetry.** A `ReserveStock` / `ReleaseStock` pair is the obvious, teachable reserve/compensate shape; a generic `OrderCancelled` event awkwardly carrying SKU quantities is not.

**Faithfulness note (divergence from Workshop 001).** This is a deliberate divergence from § 2.3 / § 4.6 wording, recorded here so the Workshop and code do not silently drift. The Workshop's *behavior* (release the reservation on cancellation, idempotently) is honored exactly; only the message name/shape differs. The Workshop is amended via this slice's Document History on archive.

### 2. The trigger is an in-handler aggregate decision in the decline branch, cascading the release (mirrors 4.5)

`PaymentDecisionHandler`'s decline branch appends `PaymentAuthFailed` **then** `OrderCancelled` in the same commit — identical to slice 4.5's `StockReservationFailedHandler` (append the failure commit, then the cancellation) — and returns the `ReleaseStock` cascade. The handler signature becomes `Task<ReleaseStock?>`: the decline path returns the message, while the approve path and the guard no-op path return `null`. This is the cascading-messages-from-handlers shape the project prefers over Process-Manager-via-Handlers per hop ([[feedback-cascading-over-pmvh]]). Marten event subscriptions were ruled out again — they need the async daemon (ADR 008).

**Wolverine routing verified against the docs.** A `null` return suppresses the cascade (confirmed: `static SecondMessage? Handle(...) => …; return null;`). `ReleaseStock` has **no Orders-local handler**, so conventional *local* routing never claims it and conventional *broker* routing carries it to Inventory — the same precedence rule that makes `ReserveStock` work, and the reason the local-vs-broker trap that bit 4.3 (a locally-handled type) does not apply here.

### 3. Inventory release is per-SKU idempotent on reservation presence (mirrors 4.2's guard)

`ReleaseStockHandler` mirrors `ReserveStockHandler`: load each line's `StockLevelView` via `FetchForWriting`, and append `StockReleased` **only for SKUs where `Reservations.Contains(orderId)`**. Unlike reserve (all-or-nothing across lines), release is **per-SKU independent** — a line with no live reservation is skipped, not a failure. This keeps the handler correct under at-least-once delivery (a duplicate `ReleaseStock` finds the reservations already gone and no-ops) and under reordering.

**Why the reservation is guaranteed present on the 4.6 decline path.** Orders reaches the payment gate (`stock_reserved`) only *after* Inventory's `StockReserved` reply landed — so when 4.6 publishes `ReleaseStock`, Inventory has already reserved. The per-SKU no-op guard is therefore exercised in 4.6 only by *duplicate* `ReleaseStock` delivery; the "no reservation yet" no-op (Workshop § 2.3 failure path) is a **4.7-timeout** concern (a timeout can fire before a grant). Building the guard now means 4.7 plugs into the same handler unchanged — the reuse the proposal promises.

### 4. The release fold restores the view and forgets the reservation

`Apply(StockReleased)` on `StockLevelViewProjection` is the inverse of `Apply(StockReserved)`: `available += quantity`, `reserved -= quantity`, `Reservations.Remove(orderId)`. Removing the order id is what makes a duplicate release no-op (the guard then finds no reservation) and keeps `Reservations` an accurate live-holds list.

## Risks / Trade-offs

- **Workshop-wording divergence (Decision 1).** Accepted and documented as a faithfulness note; behavior is faithful, only the contract name differs. The alternative (a lines-carrying `OrderCancelled` event) was weighed and rejected with the user.
- **Reserved stock is never committed.** On `OrderConfirmed` (4.4) the reservation simply stays reserved; there is no `StockCommitted`. 2.3 handles only the *cancellation* release path. Out of scope and unchanged here (Workshop § 8 future-ADR candidate) — explicitly *not* invented in this slice.
- **The delayed-`StockReserved` race (Workshop § 4.7 cross-cutting).** Release is keyed on the reservation *existing*, not on event order, so it is correct under at-least-once + reordering. Covered by a test. On the 4.6 path the race window is closed by Decision 3's ordering argument; the test pins the general guarantee for 4.7's sake.

## Open Questions

- **Payment timeout (4.7)** — `OrderPaymentTimeout` scheduling + the `OrdersAwaitingPayment*` projection remain unbuilt; it will publish the same `ReleaseStock` this slice introduces. After 4.6, timeout is the **only** remaining order-cancellation path.
- **Committing reserved stock on confirmation** — still deferred; no `StockCommitted` event exists. A future ADR if a "fulfilled/shipped" beat is ever modeled.
