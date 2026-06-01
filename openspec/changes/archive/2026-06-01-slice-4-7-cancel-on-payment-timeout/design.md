## Context

Slice 4.7 closes the last order-cancellation path: the order that never hears back. Slices 4.5 and 4.6 cancel on an *answer* (a stock refusal, a payment decline); 4.7 cancels on *silence* — a deadline set at placement fires, finds the order still non-terminal, and ends it. This is the project's first temporal automation (the Bruun pattern, Workshop § 5/§ 6 slice 4.7): a Wolverine **scheduled self-message** carries the deadline, an inline **`OrdersAwaitingPayment*`** projection makes the waiting observable, and the same stream-state guard idiom every Order handler uses makes the timer harmless when the order settled first.

All five design forks were resolved with the user at the prompt review gate (prompt 011, Locked decisions); this design records them with their rationale. The Inventory side is **deliberately untouched** — slice 4.6 built `ReleaseStock` + `ReleaseStockHandler` with the per-SKU reservation guard precisely so this slice could reuse them unchanged (4.6 design Decision 3).

## Goals / Non-Goals

**Goals:**
- Every placed order gets a payment deadline: `PlaceOrder` cascades a scheduled `OrderPaymentTimeout { orderId }` self-message (config-driven delay).
- A fired timeout on a non-terminal order appends `OrderCancelled { reason: "payment_timeout" }` and cascades `ReleaseStock` (always — see Decision 2).
- A fired timeout on a terminal order is a silent no-op (the guard); duplicates are no-ops.
- An inline `OrdersAwaitingPayment*` projection (row on `OrderPlaced`, conditional delete on terminal) + `GET /orders/awaiting-payment`.
- Tests without real-time waiting: scheduled-envelope assertion + direct handler invocation + pure projection folds.

**Non-Goals:** cart abandonment (3.4 — the other Bruun automation, its own slice). Cart edits (3.2/3.3). `StockCommitted` / commit-on-confirmation (Workshop § 8 future-ADR candidate — not invented here). Async daemon / event subscriptions (ADR 008). Real payment integration (vision.md non-goal). Cancelling/de-scheduling the timeout when an order confirms (see Risks — the no-op guard is the chosen mechanism).

## Decisions

### 1. The deadline is scheduled at order placement, not at stock reservation (resolved with the user)

`PlaceOrder` schedules the timeout in the same action that starts the Order stream. Workshop-faithful (slice 4.1's writes-to column names the schedule; it was deferred to this slice). The alternative — starting the timer when the payment gate opens (`StockReservedHandler`) — reads more literally as a "payment" timeout but leaves a hole: an order whose `ReserveStock` reply is lost sits at `awaiting_confirmation` with **no deadline at all**. One deadline at placement covers every way an order can fail to settle. The reason stays `payment_timeout` (Workshop vocabulary) regardless of which gate the order was stuck at; the slight semantic stretch is documented here rather than inventing a new reason string.

### 2. The timeout-cancel always cascades `ReleaseStock` — even when Orders never recorded a stock grant (resolved with the user)

This inverts 4.6's guard logic, deliberately. 4.6 releases only at the payment gate because the guard there *proves* a reservation exists (the gate is unreachable without one). A timeout can fire at `awaiting_confirmation`, where Orders **cannot know** whether Inventory granted — the reply may be in flight, lost, or never coming. So instead of proving, the handler delegates: release unconditionally, and let Inventory's per-SKU reservation guard (the thing that *does* know) decide. This is exactly what survives the Workshop § 4.7 delayed-grant race (failure path 4): Inventory granted, the reply was lost, Orders cancels at `awaiting_confirmation` — and Inventory still holds stock that must come back. The alternative (release only when `stock_reserved`) leaks that reservation forever.

The unconditional release exercises the "no reservation → per-SKU no-op" branch of `ReleaseStockHandler` that 4.6 built but could never reach on its own path — the designed-in reuse paying off.

### 3. `OrdersAwaitingPayment*` is an observable todo-list, not load-bearing for the handler (resolved with the user)

The projection is an inline single-stream projection with a **conditional delete**: a document is created when `OrderPlaced` folds (order id, customer id, total, deadline) and deleted when any terminal event folds. The timeout handler guards on the Order stream via `FetchForWriting<OrderStatusView>` — the same single-source-of-truth idiom every other Order handler uses — and never reads the projection. The alternatives: making the projection load-bearing (handler checks row existence) introduces a second source of truth answering the same question; skipping it entirely (Wolverine scheduling makes it unnecessary for correctness) diverges from the Workshop (§ 5 reads-from, § 7 "must be inline") and loses the Bruun teaching beat the talk cites. As a read model it earns its place: the conditional delete is a new Marten projection technique in the codebase, and `GET /orders/awaiting-payment` gives the demo an observable list of in-flight orders.

### 4. The deadline is config-backed: `Orders:PaymentTimeout`, default 10 minutes (resolved with the user)

Read in `Program.cs` from configuration with a compiled default. Tests never wait for real time (Decision 5); the Aspire demo can set the value short (e.g. 30 seconds) via env var to show an order cancel itself live. A hard-coded constant was rejected: re-pacing the demo would mean recompiling.

### 5. Tests prove the schedule and the handler separately — no real-time waiting, no new cross-BC smoke (resolved with the user at the prompt review gate)

Three layers: (a) pure unit folds for the `OrdersAwaitingPayment*` projection (create / conditional delete); (b) a tracked-session assertion that placing an order **schedules** the `OrderPaymentTimeout` envelope (captured, not executed); (c) tracked-session handler tests that **invoke `OrderPaymentTimeout` directly** — the deadline having passed is the message arriving, so direct invocation *is* the fired timer (non-terminal → cancel + release cascade; confirmed → no-op; duplicate → no-op). The cross-BC `ReleaseStock` wire path is not re-proven: 4.6's decline→release smoke already covers the identical contract and the identical Inventory handler over the real broker. What a timeout smoke would add (proof that a *scheduled* message traverses the broker hop after firing) was weighed and judged not worth a flaky time-dependent test; the scheduled→fired transition is Wolverine's own tested behavior.

### 6. `OrderPaymentTimeout` is an Orders-local message, not a published-language contract

It lives in `src/CritterMart.Orders/Order/`, not `CritterMart.Contracts` — it never crosses a service boundary. It has a local handler, so Wolverine's conventional **local** routing keeps it in-process (the same local-over-broker precedence verified for `AuthorizePayment`/`PaymentDecision` in 4.3). Scheduling does not change routing — only delivery time.

## Risks / Trade-offs

- **[Scheduled-message durability across restart is unverified]** → The demo story ("place an order, restart the service, watch it still cancel") depends on whether Wolverine persists scheduled local messages with Marten persistence, and what configuration that requires (durable local queues?). Mitigation: this is the first verify-before-wiring item (Open Questions); if durability needs config we add it; if it cannot be had cheaply, the demo constraint is documented in the retro and the deadline simply does not survive a restart in round one.
- **[A timer fires for every order, including every successfully confirmed one]** → Noise: each confirmed order still gets its timeout delivered later, no-opped by the guard. This is the accepted trade-off of fire-and-check over cancel-the-timer; de-scheduling on confirmation is more machinery (tracking envelope ids) than round one earns. The Workshop models the same shape (its § 6.1 cart-abandonment alternative names fire-and-check explicitly).
- **[`payment_timeout` as the reason for an order that never cleared the *stock* gate]** → Slight vocabulary stretch (the order may have been stuck before payment ever started). Kept for Workshop fidelity; Decision 1 documents it. Inventing a second reason string (`confirmation_timeout`) would diverge from the model for marginal precision.
- **[The `OrdersAwaitingPayment*` row exists even for orders cancelled by 4.5/4.6 paths]** → Not a risk but a property: the row is deleted by *any* terminal event, so the todo-list stays accurate no matter which path ends the order. The conditional-delete folds must therefore handle `OrderCancelled` generically (not per-reason).

## Migration Plan

Additive only — no schema migration, no contract change, no broker topology change. `ApplyAllDatabaseChangesOnStartup` creates the new projection's document table; existing Order streams that predate the slice simply have no scheduled timeout (acceptable: round-one data is demo data).

## Open Questions

Resolved at implementation time against current docs (ctx7 `/jasperfx/wolverine`, `/jasperfx/marten`) before wiring — prompt 011 Orientation 9:

1. **Cascading a scheduled message**: the exact Wolverine API for delaying a cascaded message (`DelayedFor` / `ScheduledAt` on cascading return values) and how it composes with `PlaceOrder`'s existing `(IResult, ReserveStock?)` tuple return.
2. **Durability**: whether scheduled local messages survive a service restart under Marten persistence, and what configuration that needs.
3. **Test surface**: how Wolverine tracked sessions expose scheduled-but-not-executed envelopes for assertion.
4. **Conditional delete**: Marten 9's convention for conditional deletes on `SingleStreamProjection` partial classes (`ShouldDelete` signature).
