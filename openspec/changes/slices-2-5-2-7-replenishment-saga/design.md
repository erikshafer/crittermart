# Design: slices-2-5-2-7-replenishment-saga (slices 2.5–2.7)

CritterMart's first convention `Wolverine.Saga`. A `Replenishment` saga, keyed by SKU, opens when a
`ReserveStock` shortfall is detected, requests a restock, resolves on a covering receipt, and escalates on
a timeout. Design A (saga-centric) was chosen with Erik: the saga *is* the backorder state. See the
proposal for the Why/What and the `stock-management` spec delta for the SHALLs; this document records the
implementation decisions and the two non-obvious ones (`NotFound`, the escalation message) in particular.

## Decisions

1. **Design A — the saga is the backorder state.** `Replenishment : Saga` is a Marten-stored saga document
   (`Id = Sku`, `int Outstanding`), deleted on `MarkCompleted()`, **never event-sourced**. There are **no
   new `Stock` stream events** and slice 2.2's refusal path is **unchanged** — the saga is a separate,
   additive reaction to the same shortfall. Not event-sourcing transient coordination state is the teaching
   contrast against the Order's PMvH (state on the stream, ADR 007).

2. **`NotFound` is mandatory, not automatic — the load-bearing finding.** Verified against the Wolverine
   sagas guide: by default Wolverine **throws** when a non-`Start` saga message arrives for a saga that
   cannot be found (missing or already completed). The spec's "silent no-op" for `RestockArrived` (no open
   saga for the SKU) and `ReplenishTimeout` (saga already resolved) is therefore **not** free — it requires
   explicit static `NotFound(RestockArrived)` and `NotFound(ReplenishTimeout)` methods on the saga, "even if
   [they are] empty, do-nothing method[s]." There is no global flag; it is per-message-type, per-saga. The
   timeout-after-resolution no-op relies on this — the runtime provides no scheduled-message cancellation,
   exactly the property slices 4.7/3.4 already lean on.

3. **`BackorderDetected` is dual-routed; the idempotent update is free.** The saga carries **both** a static
   `Start(BackorderDetected, ReplenishDeadline)` (no saga open → open one) and an instance
   `Handle(BackorderDetected)` (saga already open → `Outstanding = Math.Max(Outstanding, e.Shortfall)`).
   Wolverine selects `Start` vs `Handle` by whether the correlated saga exists, so the spec's
   "update an open saga, don't start a second, don't schedule a second timeout" idempotency rule
   (stable under at-least-once redelivery) falls out of the routing. No `NotFound(BackorderDetected)` is
   needed — `Start` covers the absent case. (`max`, not `sum`: resolution #16, idempotent under redelivery.)

4. **Restock reaches the saga as a dedicated `RestockArrived` message (resolution #15), published by
   `ReceiveStockEndpoint`** — not Marten→Wolverine forwarding of the raw `StockReceived` stream event. This
   avoids annotating a domain event and keeps Inventory's daemon-free posture (no `UseFastEventForwarding`,
   no async daemon — ADR 008). The endpoint **injects `IMessageBus` and `PublishAsync`es** the message after
   the append rather than *returning* it: returning a value would flip the endpoint's `204 No Content`
   (asserted by `ReceiveStockTests`/`ReserveStockTests`). The publish is outbox-enlisted by
   `IntegrateWithWolverine()` + `AutoApplyTransactions()`, so it dispatches only after the receipt commits.

5. **Escalation is a published `ReplenishmentEscalated` message + a log sink (a deliberate 5th message).**
   The spec allows "recording *or* publishing" the operator alert; we publish so the alert flows on the bus
   and surfaces in CritterWatch — the saga's whole motivation. `Handle(ReplenishTimeout)` returns a cascaded
   `ReplenishmentEscalated(Sku, Outstanding)` and calls `MarkCompleted()`; a `ReplenishmentEscalatedHandler`
   logs the operator-facing alert. This adds a **5th** Inventory-local message beyond the four the prompt's
   deliverable plan enumerated (`BackorderDetected`, `RequestRestock`, `RestockArrived`, `ReplenishTimeout`).
   The prompt anticipated it ("an escalation sink, location TBD in design.md"); recording the expansion here
   keeps it explicit rather than silent (no opportunistic-edit drift).

6. **The timeout window is config-driven via a `ReplenishDeadline` singleton (resolution #18:
   escalate-and-complete).** `record ReplenishDeadline(TimeSpan Duration)` with a `static readonly Default`
   of 2 minutes, bound in `Program.cs` from `Inventory:ReplenishTimeout` via the
   `GetValue<TimeSpan?>(...) ?? Default` + `AddSingleton` pattern, injected into `Start`. This mirrors the
   repo's established `PaymentDeadline` / `CartActivityDeadline` / `PaymentAuthDelay` config-singletons
   (`src/CritterMart.Orders/Program.cs`). The short default keeps the escalate path demoable live; a
   production value would be hours. This is the *deadline* lever only — **not** the out-of-scope #19
   auto-restock supplier-delay lever. Tests deliver `ReplenishTimeout` directly, so they never wait on wall time.

7. **Shortfall is computed per short line and emitted with the unchanged failure.** On the existing
   refusal branch, a line is short when `Aggregate is null || Aggregate.Available < Line.Quantity`;
   `shortfall = Line.Quantity - (Aggregate?.Available ?? 0)`. The handler returns the **unchanged**
   `StockReservationFailed` reply together with one `BackorderDetected` per short line via Wolverine
   `OutgoingMessages`. The happy and idempotent-duplicate paths are untouched and still return a single
   `StockReserved`.

8. **Saga storage rides the existing `IntegrateWithWolverine()`; no extra Marten registration.** Wolverine's
   Marten integration provides saga persistence; `ApplyAllDatabaseChangesOnStartup()` creates the saga table.
   No `opts.Schema.For<Replenishment>()` / `Snapshot<…>` call is added (the saga is not a projection). If
   implementation proves a registration is required, it is added in `Program.cs` and this decision amended.

9. **`RequestRestock` is a logged supplier-notification stub (resolution #19); fulfilment is the Operator's
   existing `ReceiveStock` path.** `RequestRestockHandler` logs the supplier notification. A configurable
   auto-restock demo lever (à la `Payment:AuthDelay`) is **out of scope** — noted for a later demo-affordance slice.

10. **Every saga message is Inventory-local.** `BackorderDetected`, `RequestRestock`, `RestockArrived`,
    `ReplenishTimeout`, and `ReplenishmentEscalated` are routed in-process and never cross the BC boundary —
    **no** new `CritterMart.Contracts` type and **no** new broker exchange/queue beyond what conventional
    routing derives. `[SagaIdentity]` annotates the `Sku` property of the three saga-correlated messages
    (`BackorderDetected`, `RestockArrived`, `ReplenishTimeout`), since `Sku` is not named `ReplenishmentId`/`SagaId`/`Id`.
