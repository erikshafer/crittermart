# Prompt: Implementations 014 — Slice 2.4 Commit Reserved Stock on Order Confirmation

**Kind**: per-slice implementation, consolidated one PR (workshop amendment + OpenSpec change + implementation + narrative bump + prompt/retro), per the consolidate-slice-prs convention. Covers **one workshop slice** (2.4 commit reserved stock on order confirmation) — the final backend gap in the Stock stream's reservation lifecycle.
**Files touched**: this prompt; `docs/workshops/001-crittermart-event-model.md` (→ v1.6, slice 2.4 added to § 5, GWT scenarios added to § 6, § 8 Q2 + Q3 resolved); `docs/context-map/README.md` (Orders→Inventory edge gains `CommitStock`); `openspec/changes/slice-2-4-commit-stock/{proposal.md, design.md, tasks.md, specs/stock-management/spec.md, specs/order-lifecycle/spec.md}` (new — two capability deltas); `src/CritterMart.Contracts/CommitStock.cs` (new), `src/CritterMart.Inventory/Stock/StockCommitted.cs` (new event), `src/CritterMart.Inventory/Stock/StockLevelView.cs` (Committed counter + Apply fold), `src/CritterMart.Inventory/Features/CommitStock.cs` (new handler), `src/CritterMart.Orders/Order/PaymentHandlers.cs` (return type → nullable tuple, CommitStock cascade); `tests/CritterMart.Inventory.Tests/{StockLevelViewProjectionTests.cs (amend), CommitStockTests.cs (new)}`, `tests/CritterMart.Orders.Tests/PaymentAuthorizationTests.cs` (amend), `tests/CritterMart.CrossBc.Tests/CrossBcCommitStockSmokeTests.cs` (new); `docs/narratives/004-customer-purchase.md` (→ v1.8, Moment 4 amended); `docs/retrospectives/implementations/014-slice-2-4-commit-stock.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; the roundtable discussion unanimously validated the decision to model StockCommitted before any code was written.
**Commit subject**: `feat: slice 2.4 commit reserved stock on order confirmation`

## Framing

Slices 2.1–2.3 built the Stock stream's lifecycle: receive stock (2.1), reserve on order placement (2.2, replaced by 4.2's cross-BC path), release on cancellation (2.3). But the success path had a gap — when an order *confirms* (slice 4.4), its reserved stock stays reserved forever. The Stock stream can't tell its own story (was this reservation in-flight or permanently consumed?), the `StockLevelView` conflates in-flight holds with sold goods, and the invariant `Available + Reserved + Committed = ΣStockReceived` is unassertable. Slice 2.4 closes this gap by mirroring the release path: every reservation now reaches a terminal event — `StockReleased` on cancellation or `StockCommitted` on confirmation.

## Goal

When `PaymentDecisionHandler` confirms an order (approve path), it cascades a `CommitStock { orderId, lines }` message to Inventory over RabbitMQ. Inventory's `CommitStockHandler` consumes it, loading each line's SKU stream via `FetchForWriting<StockLevelView>`, and — only if the order holds a reservation on that SKU — appends `StockCommitted`. The projection fold: `Reserved -= qty`, `Committed += qty`, order dropped from `Reservations`. Per-SKU idempotent (duplicate = no-op). The `StockLevelView` gains a `Committed` counter enabling the `Available + Reserved + Committed = ΣStockReceived` invariant. `openspec validate --strict` passes; all non-Catalog tests green.

## Spec delta

A new OpenSpec change `slice-2-4-commit-stock` with **two** capability deltas:

1. **`stock-management`** (1 ADDED requirement, 3 scenarios): *Commit reserved stock on confirmation* — happy path (reservation → committed), duplicate no-op (no reservation found), no-reservation no-op (order never reserved this SKU).
2. **`order-lifecycle`** (1 MODIFIED requirement, 1 scenario): *Confirm when both gates close* — gains the CommitStock cascade as an Inventory consequence of confirmation.

Narrative 004 gains amended Moment 4 (→ v1.8, `slices` adds 2.4). Workshop § 5 gains slice 2.4 row; § 6 gains GWT scenarios; § 8 Q2 resolved as "no" (no symmetric cancel on stock failure), Q3 resolved as "yes" (commit on confirmation). Context map gains `CommitStock` on the Orders→Inventory edge.

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** § 5 slice table (2.4 row), § 6 GWT scenarios, § 8 open questions Q2 + Q3.
2. **`docs/narratives/004-customer-purchase.md`** (v1.7) — Moment 4 (confirmation) is what gains the Inventory consequence; "What the Customer does not yet see" has the "no committing" bullet to retire.
3. **`openspec/specs/stock-management/spec.md`** and `openspec/specs/order-lifecycle/spec.md` — the durable specs this change extends.
4. **The 2.3/4.6 implementation as the mirror template**: `src/CritterMart.Inventory/Features/ReleaseStock.cs` (handler pattern), `src/CritterMart.Contracts/ReleaseStock.cs` (contract shape), `src/CritterMart.Orders/Order/PaymentHandlers.cs` (the decline path's return).
5. **ADR 014** (published language — cross-BC commands in Contracts, not domain events).
6. **ADR 007** (aggregates as process managers — the confirmation decision is the aggregate's own).

## Working pattern

Author on branch `feat/slice-2-4-commit-stock`: (1) this frozen prompt; (2) workshop amendment (§ 5 + § 6 + § 8); (3) OpenSpec change via CLI; (4) context map update; (5) implementation (contract → event → view fold → handler → PaymentDecisionHandler cascade → tests green); (6) narrative 004 → v1.8; (7) retro. One consolidated PR; the user merges.

## Out of scope

- **No async daemon or event subscriptions** — ADR 008 constraint.
- **No new Order stream events** — the commit is an Inventory concern; the Order already has its terminal `OrderConfirmed`.
- **No Catalog test fixes** — the 7 pre-existing `BrokerInitializationException` failures are from CritterWatch (commit `2b127f4`), not this slice.
- **No fulfilment beyond "committed"** — there is no picking, packing, shipping, or delivery; committed is the terminal.
- **No `openspec archive`** — post-merge concern.
