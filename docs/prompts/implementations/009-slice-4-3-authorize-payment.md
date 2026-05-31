# Prompt: Implementations 009 — Slice 4.3 Authorize Payment (stubbed) (+ 4.4 confirm, one PR)

**Kind**: per-slice implementation, consolidated one PR (narrative bump + OpenSpec change + implementation + prompt/retro), per the consolidate-slice-prs convention. **Bundles slice 4.4** (confirm when both gates close) so the happy path reaches its terminal success state. Decline-cancel (4.6) is deferred.
**Files touched**: this prompt; `openspec/changes/slice-4-3-authorize-payment/{proposal.md, specs/order-lifecycle/spec.md, design.md, tasks.md}` (new); `docs/narratives/004-customer-purchase.md` (→ v1.3, Moment 4); `src/CritterMart.Orders/Order/{PaymentAuthorized.cs, PaymentAuthFailed.cs, OrderConfirmed.cs, PaymentProvider.cs, PaymentHandlers.cs}` (new); `src/CritterMart.Orders/Order/OrderStatusView.cs` (+ statuses, + folds); `src/CritterMart.Orders/Order/StockReservationOutcomeHandlers.cs` (`StockReservedHandler` → cascade `AuthorizePayment`); `src/CritterMart.Orders/Program.cs` (register `IPaymentProvider`); `tests/CritterMart.Orders.Tests/{OrderStatusViewProjectionTests.cs, PaymentAuthorizationTests.cs, StockReservationOutcomeTests.cs}`; `tests/CritterMart.CrossBc.Tests/CrossBcReserveStockSmokeTests.cs` (assertion update — `stock_reserved` now transient); `docs/retrospectives/implementations/009-slice-4-3-authorize-payment.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; collaborative on genuine forks (present options + recommendation, user decides — memory `feedback-collaborate-on-decisions`, `feedback-options-with-previews`)
**Commit subject**: `feat: slice 4.3 authorize payment (stubbed) (+ 4.4 confirm)`

## Framing

Slice 4.3 is the Order aggregate's **second gate** (ADR 007). With stock reserved (4.2), the order authorizes payment before it can be confirmed. It is the **in-process mirror** of 4.2's cross-BC reserve hop: the same Klefter translation-decision pattern, but the external party is a **stubbed payment provider** reached in-process, not Inventory reached over RabbitMQ. Bundled slice 4.4 (a pure aggregate decision on stream state — both gates closed ⇒ confirm) carries the happy path to its terminal `OrderConfirmed` (status `confirmed`) — the payoff of the whole two-gate process manager.

## Goal

When the Order-stream `StockReserved` Klefter commit is recorded, Orders cascades an in-process `AuthorizePayment { orderId, amount }` (amount = order total). A stubbed `IPaymentProvider` decides; its transient `PaymentDecision` is translated into a Klefter local commit — approve → `PaymentAuthorized { authCode, amount }`, decline → `PaymentAuthFailed { reason }`. On approval, with both gates closed, the same handler appends `OrderConfirmed`; `OrderStatusView` settles on `confirmed`. Decline records the failure but does not confirm (status stays `stock_reserved`; cancellation is the deferred 4.6). Idempotent via a stream-state guard at the payment gate. Proven by pure-fold unit tests + tracked-session tests (happy / decline-via-swapped-provider / idempotent); `openspec validate --strict` passes; full solution green.

## Spec delta

A new OpenSpec change `slice-4-3-authorize-payment` with **one** capability delta (no new capability):
- **`order-lifecycle`** (two ADDED requirements): *Authorize payment for a reserved order* (send `AuthorizePayment` on `StockReserved`; record `PaymentAuthorized`/`PaymentAuthFailed` Klefter commit; idempotent at the payment gate) and *Confirm an order when both gates close* (`OrderConfirmed`, status `confirmed`, terminal — slice 4.4).

Narrative 004 gains **Moment 4** (→ v1.3, `slices` adds 4.3 + 4.4). Workshop § 4.3 + 4.4 GWT are satisfied as written (happy + the provider-declines failure path); no workshop amendment expected (the slice table already lists 4.3/4.4).

## Locked decisions (collaborative forks, resolved with the user this session)

1. **Scope = 4.3 + 4.4 (confirm); defer 4.6.** Carry the happy path to terminal `confirmed` (4.4 is a free in-process aggregate decision). Leave the decline branch recorded-but-non-terminal — cancellation + the cross-BC stock release it needs (Inventory 2.3) is slice 4.6. Asymmetry with 4.2 (which bundled its *failure*-cancel) is deliberate: 4.2's failure-cancel was free; 4.3's success-confirm is the free terminal here.
2. **Stub policy = always approve + swappable `IPaymentProvider`.** `StubPaymentProvider` always approves with a `stub-{guid}` auth code; the decline branch is exercised by registering a declining `IPaymentProvider` on a one-off test host — no magic sentinel value in the domain payload.
3. **Trigger = cascading message from `StockReservedHandler`** (same shape as 4.2, `feedback-cascading-over-pmvh`), kept **in-process** — `AuthorizePayment`/`PaymentDecision` have local handlers, so Wolverine's conventional *local* routing keeps them off the broker (verified against the Wolverine docs; `ConventionalLocalRoutingIsAdditive()` deliberately not called). Marten event subscriptions ruled out again (need the daemon, ADR 008).
4. **Three hops, mirroring 4.2:** `StockReservedHandler` (cascade request) → `AuthorizePaymentHandler` (call provider, no stream access) → `PaymentDecisionHandler` (Klefter commit + confirm). Separates the external-call boundary from the durable commit.
5. **Amount = order total** read from `stream.Aggregate.Total` at the commit hop; `PaymentDecision` stays a thin yes/no envelope.

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** §§ 3 (Place Order storyboard — the `Orders → Pay : AuthorizePayment` / `Pay → Orders : PaymentDecision` hops + the Klefter local-commit note), 4 (Order vocabulary — `PaymentAuthorized`/`PaymentAuthFailed`/`OrderConfirmed`), 5 (slice rows 4.3/4.4), 6.1 §§ 4.3 + 4.4 (GWT incl. the provider-declines failure path).
2. **`docs/narratives/004-customer-purchase.md`** (v1.2) — Moment 3 leaves the order at `stock_reserved`; Moment 4 continues from there.
3. **`openspec/specs/order-lifecycle/spec.md`** — the durable spec this change extends (place + reserve + cancel-on-stock-failure).
4. **`src/CritterMart.Orders/Order/StockReservationOutcomeHandlers.cs`** — the 4.2 inbound handler that records `StockReserved`; it now also cascades `AuthorizePayment`. The shape to mirror.
5. **`src/CritterMart.Orders/Order/OrderStatusView.cs`** — the projection + status enum to extend.
6. **`src/CritterMart.Inventory/Features/ReserveStock.cs`** — the 4.2 reserve handler (returns a cascading outcome `object`); the in-process payment hop mirrors its shape.
7. **ADRs 007 (PMvH for Order), 008 (inline projections, no daemon), 014 (`CritterMart.Contracts` is cross-BC published language — payment is in-process, so **no** Contracts type).** `docs/rules/structural-constraints.md` § Cross-service messaging.
8. **Stack reality**: `Directory.Packages.props` (Wolverine 6.1 / Marten 9.2 / .NET 10). **No new package** — payment is in-process Wolverine messaging.
9. **Skills**: `wolverine-handlers-fundamentals`, `wolverine-messaging-message-routing` (local vs broker routing precedence), `marten-aggregate-handler-workflow`, `wolverine-testing-integration-marten`. Use `find-docs` (ctx7 `/jasperfx/wolverine`) to **verify conventional-local-vs-broker routing precedence** before wiring the cascade — the in-process guarantee depends on it.

## Working pattern

Author on branch `feat/slice-4-3-authorize-payment`: (1) this frozen prompt [review gate]; (2) OpenSpec change + `validate --strict`; (3) implementation (events → provider seam → handlers → projection/statuses → cascade from `StockReservedHandler` → register provider); (4) tests green (incl. updating the two pre-existing tests that pinned `stock_reserved` as terminal); (5) narrative 004 → v1.3; (6) retro. Verify Wolverine routing precedence against current docs before wiring. One consolidated PR; the user merges. `openspec archive` is a post-merge `tidy:` step.

## Out of scope

- **No slice 4.6 (cancel on payment decline) or its cross-BC stock release (Inventory 2.3).** The decline path records `PaymentAuthFailed` and stops — non-terminal by design this slice.
- **No slice 4.7 (payment timeout)** — no `OrderPaymentTimeout` scheduling, no `OrdersAwaitingPayment*` projection.
- **No real payment integration** — provider is stubbed (vision.md non-goal).
- **No `CritterMart.Contracts` change** — payment is in-process; Contracts is cross-BC published language only (ADR 014).
- **No README/index refresh** and **no `openspec archive`** — post-merge `tidy: docs` concerns (no opportunistic edits). The lingering `product-catalog` spec `## Purpose` TBD also stays for that `tidy:` pass.
- **No async daemon / Event Subscriptions** (ADR 008).
