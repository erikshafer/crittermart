# Prompt: Implementations 010 — Slice 4.6 Cancel on Payment Decline (+ Inventory 2.3 release stock, one PR)

**Kind**: per-slice implementation, consolidated one PR (narrative bump + OpenSpec change + implementation + prompt/retro), per the consolidate-slice-prs convention. **Bundles Inventory slice 2.3** (release reserved stock) — 4.6 is unverifiable end-to-end without it. The project's first cancellation that crosses a BC boundary *back*.
**Files touched**: this prompt; `openspec/changes/slice-4-6-cancel-on-payment-decline/{proposal.md, design.md, tasks.md, specs/order-lifecycle/spec.md, specs/stock-management/spec.md}` (new); `docs/narratives/004-customer-purchase.md` (→ v1.4, Moment 5); `src/CritterMart.Contracts/ReleaseStock.cs` (new); `src/CritterMart.Orders/Order/OrderCancelled.cs` (+ `CancelReason.PaymentDeclined`); `src/CritterMart.Orders/Order/PaymentHandlers.cs` (decline branch → cancel + cascade `ReleaseStock`); `src/CritterMart.Inventory/Stock/StockReleased.cs` (new); `src/CritterMart.Inventory/Features/ReleaseStock.cs` (new); `src/CritterMart.Inventory/Stock/StockLevelView.cs` (+ `Apply(StockReleased)`); `tests/CritterMart.Orders.Tests/PaymentAuthorizationTests.cs` (decline test → cancel + release); `tests/CritterMart.Inventory.Tests/{StockLevelViewProjectionTests.cs, ReleaseStockTests.cs}` (new); `tests/CritterMart.CrossBc.Tests/{CrossBcFixture.cs (configurable provider), CrossBcReleaseStockSmokeTests.cs (new)}`; `docs/retrospectives/implementations/010-slice-4-6-cancel-on-payment-decline.md` (forthcoming)
**Mode**: solo, consolidated one-PR cross-BC slice; collaborative on genuine forks (present options + recommendation, user decides — memory `feedback-collaborate-on-decisions`, `feedback-options-with-previews`)
**Commit subject**: `feat: slice 4.6 cancel on payment decline (+ Inventory 2.3 release stock)`

## Framing

Slice 4.3 recorded a declined payment as a `PaymentAuthFailed` Klefter commit and **deliberately stopped** — non-terminal, stock still held. Slice 4.6 turns that terminal: the Order aggregate cancels itself on decline and, because stock *was* reserved (the payment gate is reached only after the stock gate closed), it must **release** that reservation back to Inventory. This is the mirror of slice 4.5's stock-failure cancel — but where 4.5 released nothing (an all-or-nothing refusal reserved nothing), 4.6 must hand real stock back. That cross-BC release is **Inventory slice 2.3**, bundled here.

## Goal

When `PaymentDecisionHandler` declines at the payment gate (status `stock_reserved`), it appends `PaymentAuthFailed` **then** `OrderCancelled { reason: "payment_declined" }` in the same commit (status `cancelled`) and **cascades a single `ReleaseStock { orderId, lines }`** (lines read from the Order stream) to Inventory. Inventory's new `ReleaseStockHandler` consumes `ReleaseStock`, appends `StockReleased` per SKU **only where the order holds a reservation** (`Reservations.Contains(orderId)`), and the `StockLevelView` fold restores available/reserved and drops the order from reservations. Both sides idempotent (Orders: payment-gate guard; Inventory: per-SKU reservation presence). Proven by tracked-session tests both sides + a cross-BC decline→release smoke over the real broker; `openspec validate --strict` passes; full solution green.

## Spec delta

A new OpenSpec change `slice-4-6-cancel-on-payment-decline` with **two** capability deltas (no new capability):
- **`order-lifecycle`** (one ADDED requirement): *Cancel an order when payment is declined* — at the payment gate, append `OrderCancelled { payment_declined }` and publish `ReleaseStock` with the order's lines; idempotent.
- **`stock-management`** (one ADDED requirement): *Release reserved stock on cancellation* — on `ReleaseStock`, release each held line (`StockReleased`, view restored, order dropped), per-SKU idempotent under at-least-once + reordering.

Narrative 004 gains **Moment 5** (→ v1.4, `slices` adds 4.6 + 2.3). Workshop § 2.3 + 4.6 GWT are satisfied behaviorally; the **message shape diverges** from the workshop's `OrderCancelled`-event wording to a `ReleaseStock` command (design.md Decision 1) — a faithfulness note, amended on archive.

## Locked decisions (collaborative forks, resolved with the user this session)

1. **Cross-BC message = `ReleaseStock { orderId, lines }` command, not a published `OrderCancelled { orderId }` event.** Symmetric with `ReserveStock`; Inventory needs the SKUs/quantities (its `Reservations` store only order-id strings) and its wire language stays about *stock* not *orders* (anti-corruption / published language, ADR 014). Deliberate divergence from Workshop § 2.3/§ 4.6 wording — recorded as a faithfulness note (design.md Decision 1).
2. **Scope = 4.6 + 2.3 in one consolidated PR; 4.7 out.** 4.6 is unverifiable without the Inventory release. Payment timeout (4.7) needs Wolverine scheduling + `OrdersAwaitingPayment*` — a separate slice; it will reuse this `ReleaseStock` unchanged.
3. **Guards mirror 4.2/4.5.** Orders cancels only at the payment gate (`stock_reserved`), else no-op (no cascade). Inventory releases a SKU only if it holds a reservation for the order, else per-SKU no-op. Correct under at-least-once + the delayed-`StockReserved` reordering race (release keyed on reservation presence, not event order).

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** § 2.3 (lines ~250–265: release happy path + the two idempotent no-ops), § 4.6 (lines ~386–391: the aggregate-decision cancel + publish), § 4.7 cross-cutting race (lines ~410–416), event list `StockReleased` / `OrderCancelled` reasons.
2. **`docs/narratives/004-customer-purchase.md`** (v1.3) — Moment 4 ends at the declined-but-not-cancelled state; Moment 5 continues from there.
3. **`openspec/specs/{order-lifecycle,stock-management}/spec.md`** — the two durable specs this change extends.
4. **`src/CritterMart.Orders/Order/PaymentHandlers.cs`** (`PaymentDecisionHandler` decline branch = the 4.6 hook) and **`StockReservationOutcomeHandlers.cs`** (slice 4.5 = the "append failure then `OrderCancelled`" shape to mirror).
5. **`src/CritterMart.Inventory/Features/ReserveStock.cs`** — the handler 2.3 mirrors (`FetchForWriting` per SKU, per-SKU guard). **`Stock/{StockReserved.cs, StockLevelView.cs}`** — the event + fold to mirror/inverse.
6. **`src/CritterMart.Contracts/{ReserveStock.cs, StockReserved.cs}`** — where the new `ReleaseStock` contract goes (published language).
7. **ADRs 003 (RabbitMQ), 007 (PMvH/Order), 008 (inline, no daemon), 014 (Contracts published language).** `docs/rules/structural-constraints.md` § Cross-service messaging.
8. **Stack reality**: `Directory.Packages.props` (Wolverine 6.1 / Marten 9.2 / .NET 10). **No new package, no new broker topology** — `ReleaseStock` rides existing conventional routing.
9. **Skills**: `marten-aggregate-handler-workflow`, `wolverine-handlers-fundamentals`, `wolverine-messaging-message-routing` (local-vs-broker precedence), `wolverine-integrations-rabbitmq`, `marten-projections-single-stream`, `wolverine-testing-integration-marten` + `wolverine-testing-with-testcontainers`. Use `find-docs` (ctx7 `/jasperfx/wolverine`) to **verify the cascade routes to the broker (no local handler) and a `null` return suppresses the cascade** before wiring.

## Working pattern

Author on branch `slice-4-6-cancel-on-payment-decline`: (1) this frozen prompt [review gate]; (2) OpenSpec change + `validate --strict`; (3) implementation (Contracts `ReleaseStock` → Inventory `StockReleased` + handler + fold → Orders `CancelReason.PaymentDeclined` + decline branch); (4) tests green (incl. updating the 4.3 decline test that pinned the non-terminal decline); (5) narrative 004 → v1.4; (6) retro. Verify Wolverine cascade routing against current docs before wiring. One consolidated PR; the user merges. `openspec archive` is a post-merge `tidy:` step.

## Out of scope

- **No slice 4.7 (payment timeout)** — no `OrderPaymentTimeout` scheduling, no `OrdersAwaitingPayment*` projection. After 4.6, timeout is the only remaining cancellation path.
- **No `StockCommitted` / commit-on-confirmation semantics** — reserved stock stays reserved on `OrderConfirmed` (Workshop § 8 future-ADR candidate). 2.3 handles only the cancellation release.
- **No real payment integration** — provider stubbed (vision.md non-goal).
- **No README/index refresh** and **no `openspec archive`** — post-merge `tidy: docs` concerns (no opportunistic edits). The lingering `product-catalog` spec `## Purpose` TBD stays for that pass.
- **No async daemon / Event Subscriptions** (ADR 008).
