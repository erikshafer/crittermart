# Prompt: Implementations 035 — Slices 2.5 + 2.6 + 2.7 Inventory Replenishment Saga (CritterMart's first convention `Wolverine.Saga`)

**Kind**: per-slice implementation (Inventory replenishment saga, consolidated per [[feedback-consolidate-slice-prs]])
**Files touched**: `docs/prompts/implementations/035-slices-2-5-2-7-replenishment-saga.md` (new, this file); `openspec/changes/slices-2-5-2-7-replenishment-saga/{proposal.md,specs/stock-management/spec.md}` (authored in the design-return session) + `{design.md,tasks.md}` (authored this session) + `openspec validate --strict` green; `docs/narratives/008-operator-replenish-backorders.md` (new) + `docs/narratives/README.md` (count 7→8); `src/CritterMart.Inventory/Stock/{Replenishment.cs,BackorderDetected.cs,RequestRestock.cs,RestockArrived.cs,ReplenishTimeout.cs}` (new); `src/CritterMart.Inventory/Features/{ReserveStock.cs,ReceiveStock.cs}` (modified — additive emits); `src/CritterMart.Inventory/Program.cs` (register `Replenishment` as a Marten/saga document); a small `RequestRestock` notification-stub handler + an escalation sink (location TBD in design.md); `tests/CritterMart.Inventory.Tests/ReplenishmentSagaTests.cs` (new) + integration coverage; `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md` (forthcoming)
**Mode**: solo implementation; OpenSpec change `slices-2-5-2-7-replenishment-saga` proposal + spec delta already authored (design-return session); this session adds `design.md` + `tasks.md`, then code
**Commit subject**: `feat: add Inventory replenishment saga (Replenishment) — slices 2.5–2.7`

## Framing

CritterMart shows two coordination patterns today — Wolverine **cascading messages** ([[feedback-cascading-over-pmvh]]) and **Process Manager via Handlers** (Order/Cart as their own process manager, ADR 007) — but **never the convention `Wolverine.Saga`**, which ADR 007 deliberately forgoes *for the Order*. This session lands the repo's **first** convention saga: a `Replenishment` saga in Inventory that opens when stock runs short, requests a restock, resolves when a covering receipt arrives, and escalates on a timeout. It is the third member of the "ways to wait for a deadline" trio (Bruun todo-list projection vs. PMvH-on-the-stream vs. saga-storage-doc), and the saga the team wants to exercise/observe in CritterWatch and the talk.

**Design A (saga-centric), already decided.** The saga *is* the backorder state — a Marten-stored saga document keyed by SKU, deleted on `MarkCompleted()`, **never event-sourced**. There are **no new `Stock` stream events**; slice 2.2's refusal path is **unchanged**. The saga is a *separate, additive* reaction to the same shortfall. Not event-sourcing transient coordination state is the teaching point against the Order's PMvH (state on the stream).

**Build order is 2.6/2.7 mechanics around the saga, then 2.5 wiring** — the saga class and its `Handle(RestockArrived)` / `Handle(ReplenishTimeout)` are pure and unit-testable first; the `ReserveStock`-shortfall start wiring (2.5) closes the loop last.

New mechanism alert: `ReplenishTimeout` is a Wolverine `TimeoutMessage` **auto-scheduled by returning it from the saga's `Start`** — distinct from the manual `DelayedFor` self-scheduling the Bruun slices (3.4/4.7) use. Read the ctx7 saga shape in the research note before writing it.

## Goal

- `Replenishment : Saga` (`Id = Sku`, `Outstanding`) exists, registered as a Marten document; saga storage active on Inventory's existing `IntegrateWithWolverine()` (no daemon).
- A refused `ReserveStock` emits `BackorderDetected` per short SKU; it starts a `Replenishment` saga (or updates an open one's `Outstanding` to `max(current, shortfall)`), returns `RequestRestock`, and schedules `ReplenishTimeout` — **slice 2.2's existing refusal behavior unchanged**.
- Every `ReceiveStock` publishes `RestockArrived`; an open saga completes when covered, reduces-and-stays-open on partial, no-ops when absent.
- A still-open saga at `ReplenishTimeout` escalates (operator alert) and completes; a timeout after resolution is a silent no-op.
- All existing Inventory tests remain green (confirm the baseline count at session start) plus new saga unit + integration tests; `dotnet build` zero errors.

## Spec delta

This session satisfies the **three ADDED requirements** in the `stock-management` capability — *open a replenishment saga on shortfall* (2.5), *resolve on restock* (2.6), *escalate on timeout* (2.7) — authored in `openspec/changes/slices-2-5-2-7-replenishment-saga/specs/stock-management/spec.md`. Workshop 001 v1.12 § 6 carries the GWT scenarios 2.5–2.7 (modeled-not-built); this session satisfies them. The OpenSpec change is the machine-readable contract; Narrative 008 is the human-readable companion. **No spec delta is added in this session** — the requirements were authored in the design-return session; this session lands `design.md` + `tasks.md` and the code that satisfies them.

## Orientation files

1. **`docs/workshops/001-crittermart-event-model.md` § 4 "Saga state and saga messages" + § 6 slices 2.5–2.7 + § 8 items 15–19** — the source of truth and the resolved policy decisions.
2. **`openspec/changes/slices-2-5-2-7-replenishment-saga/proposal.md`** — the "What Changes" build map + the open-question resolutions baked into the SHALLs.
3. **`docs/research/wolverine-saga-feasibility.md`** — the convention-saga shape (ctx7), `[SagaIdentity]`, `TimeoutMessage`, `MarkCompleted`, and the EF-vs-Marten saga-store note. **No upstream ai-skill covers sagas** — lean on this + ctx7.
4. **`src/CritterMart.Inventory/Features/ReserveStock.cs`** — the refusal path (`anyShort` at the insufficient branch) where `BackorderDetected` is emitted, additively; the existing `StockReservationFailed` return is untouched.
5. **`src/CritterMart.Inventory/Features/ReceiveStock.cs`** — `ReceiveStockHandler`, where `RestockArrived` is published on every receipt; `StockReceived` append unchanged.
6. **`src/CritterMart.Inventory/Program.cs`** — the Marten registration block (`opts.Projections.Snapshot<…>`); register `Replenishment` as a document type near the existing registrations; confirm saga storage needs no extra call beyond `IntegrateWithWolverine()`.
7. **`src/CritterMart.Orders/Ordering/PaymentTimeoutHandler.cs` + `Shopping/CartAbandonmentHandler.cs`** — the *contrast* references: how the repo does timeouts the PMvH way (manual `DelayedFor`, stream-state guard). The saga does it the convention way — read these to articulate the difference, not to copy them.
8. **`tests/CritterMart.Inventory.Tests/`** — existing fold + handler test patterns (Alba/Marten); the saga unit tests follow the pure-`Start`/`Handle` style.

## Working pattern

1. **Create feature branch** `feat/inventory-replenishment-saga`.
2. **Author `design.md` + `tasks.md`** in the openspec change; `openspec validate --strict` green before code.
3. **Saga + messages first** — `Replenishment.cs` (`Start`/`Handle` pure methods), the four message records; unit-test the saga in isolation (open, max-update idempotency, cover-and-complete, partial-reduce-stay-open, escalate-and-complete).
4. **Wire 2.6/2.5** — `ReceiveStockHandler` publishes `RestockArrived`; `ReserveStockHandler` emits `BackorderDetected` on the refusal path; register `Replenishment` in `Program.cs`.
5. **Escalation sink + `RequestRestock` stub** — a logged/published operator notification (design.md names the shape).
6. **Integration tests** — shortfall→open, receipt→resolve, not-found no-ops; `dotnet build` / `dotnet test` green.
7. **Narrative 008** + README count; **retro** at close + prompts README count 34→35.
8. **Offer live-stack verification** post-PR per [[feedback-live-verify-after-changes]] — drive a real shortfall→restock→resolve and watch the saga in CritterWatch ([[feedback-drive-demo-flows]]).

## Deliverable plan (in order)

| File | Status |
|---|---|
| `openspec/changes/slices-2-5-2-7-replenishment-saga/design.md` | new (this session) |
| `openspec/changes/slices-2-5-2-7-replenishment-saga/tasks.md` | new (this session) |
| `src/CritterMart.Inventory/Stock/Replenishment.cs` | new (`Replenishment : Saga`) |
| `src/CritterMart.Inventory/Stock/BackorderDetected.cs` | new (start message) |
| `src/CritterMart.Inventory/Stock/RequestRestock.cs` | new (supplier-notification stub) |
| `src/CritterMart.Inventory/Stock/RestockArrived.cs` | new (`[SagaIdentity]` on Sku) |
| `src/CritterMart.Inventory/Stock/ReplenishTimeout.cs` | new (`: TimeoutMessage`) |
| `src/CritterMart.Inventory/Features/ReserveStock.cs` | modify (emit `BackorderDetected` on refusal) |
| `src/CritterMart.Inventory/Features/ReceiveStock.cs` | modify (publish `RestockArrived`) |
| `src/CritterMart.Inventory/Program.cs` | modify (register `Replenishment` document) |
| `tests/CritterMart.Inventory.Tests/ReplenishmentSagaTests.cs` | new (unit) |
| `tests/CritterMart.Inventory.Tests/*` | add integration coverage |
| `docs/narratives/008-operator-replenish-backorders.md` | new + README count 7→8 |
| `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md` | new (at close) |

## Out of scope

- **The Identity email-change saga (saga #2)** — gated on an ADR-009 revisit; a separate session.
- **The auto-restock demo lever** (Workshop §8 item 19) — a config'd supplier delay à la `Payment:AuthDelay`; a later demo-affordance slice.
- **Any new `CritterMart.Contracts` type or cross-BC message** — every saga message is Inventory-local by design; if the implementation finds a reason to cross the boundary, stop and raise it (it would change the context map).
- **New `Stock` stream events or `StockLevelView` changes** — Design A keeps the saga off the stream; if a stream event feels necessary, that is the Design-B fork the workshop explicitly rejected — raise it, don't silently add it ([[feedback-flag-deferred-state-on-completion]]).
- **Async daemon / event subscriptions** — ruled out (ADR 008); the saga needs neither.
