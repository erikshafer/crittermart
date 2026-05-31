## Context

Slice 4.3 is the Order aggregate's **second gate**: with stock reserved (4.2), the order authorizes payment before it can be confirmed. It is the in-process mirror of 4.2's cross-BC reserve hop — same Klefter translation-decision pattern, but the external party (a payment provider) is stubbed and reached in-process rather than over RabbitMQ. It runs as **one consolidated PR** and **bundles slice 4.4** (confirm when both gates close) so the happy path reaches its terminal success state.

Two design points were resolved with the user this session (the scope and stub-policy forks, Decisions 1 and 4 below): how much of the payment gate this slice covers, and how the stubbed provider decides.

## Goals / Non-Goals

**Goals:**
- `StockReserved` (Order stream) → cascade one in-process `AuthorizePayment { orderId, amount }` (amount = order total).
- A stubbed `IPaymentProvider` decides; its decision is translated into a Klefter `PaymentAuthorized` (auth code + amount) or `PaymentAuthFailed`.
- On approval, with both gates closed, append `OrderConfirmed` (status `confirmed`) — the bundled slice 4.4.
- Idempotency via a stream-state guard at the payment gate.
- Unit folds + tracked-session tests (happy via the stub, decline via a swapped provider, idempotent).

**Non-Goals:** cancellation on payment decline (4.6) and the cross-BC stock release it needs (Inventory 2.3); cancellation on payment timeout (4.7) + `OrderPaymentTimeout` scheduling + `OrdersAwaitingPayment*`. No async daemon / Marten event subscriptions (ADR 008). No real payment integration (stubbed — vision.md).

## Decisions

### 1. Scope bundles slices 4.3 + 4.4 in one PR; the decline-cancel (4.6) is deferred

Resolved at this session's scope fork. The happy path is carried to its terminal `OrderConfirmed` (status `confirmed`) — the payoff of the two-gate process manager — because slice 4.4 is a **pure in-process aggregate decision** (both gates closed ⇒ confirm) that pulls in no cross-BC traffic. The **decline** branch is recorded (`PaymentAuthFailed`) but left non-terminal: turning it terminal needs `OrderCancelled { reason: "payment_declined" }` **and** a cross-BC `OrderCancelled` to Inventory to release the already-reserved stock (Inventory slice 2.3) — materially heavier than slice 4.2's bundled 4.5, where a refusal had reserved nothing to release. Bundling 4.6 here would drag an Inventory slice into a payment PR; deferring it keeps this slice in-process and focused. **Asymmetry with 4.2 is deliberate:** 4.2 bundled its *failure*-cancel because it was free (nothing reserved); 4.3 bundles its *success*-confirm because *that* is the free, in-process terminal.

### 2. Trigger is a cascading message from the stock-gate handler — in-process, not over the broker

`StockReservedHandler` (slice 4.2's inbound handler) now returns `AuthorizePayment?`: when it records the `StockReserved` Klefter commit it also cascades the payment request (null on the idempotent no-op path). This is the same cascading-messages-from-handlers shape 4.2 established ([[feedback-cascading-over-pmvh]]), kept in-process. **Wolverine routing was verified against the docs:** conventional *local* routing takes precedence over conventional *broker* routing, so a message type with a local handler (`AuthorizePayment`, `PaymentDecision`) is handled in-process and never reaches RabbitMQ — `ConventionalLocalRoutingIsAdditive()` (the opt-in to do both) is deliberately **not** called. Marten event subscriptions were ruled out again (they need the async daemon, ADR 008).

### 3. Three hops, mirroring 4.2: request → provider → translate

The gate is three handlers, symmetric with 4.2's reserve→reply→commit:
- `StockReservedHandler` cascades `AuthorizePayment` (the request).
- `AuthorizePaymentHandler` injects `IPaymentProvider`, calls it, and cascades the transient `PaymentDecision` (the "reach the provider" boundary — **no stream access**, so the provider call holds no write lock).
- `PaymentDecisionHandler` translates the decision into the Klefter commit on the Order stream (the only DB-touching hop), and on approval appends `OrderConfirmed`.
This keeps the external-call boundary (hop 2) cleanly separate from the durable Klefter commit (hop 3), exactly as the Workshop § 3 storyboard draws it (`Orders → Pay : AuthorizePayment`, `Pay → Orders : PaymentDecision`, then the local commit). The `PaymentDecision` is transient and read exactly once.

### 4. Stub policy: always approve, with a swappable `IPaymentProvider` for the decline branch

Resolved at this session's stub-policy fork. `StubPaymentProvider` always approves with a synthetic `stub-{guid}` auth code — the realistic default (payments usually pass) and a vision.md non-goal stubbed honestly. The failure branch is exercised by **registering a declining `IPaymentProvider`** on a one-off test host, so no magic sentinel value (a threshold amount, a special customer id) leaks into the domain payload. Chosen over a magic-amount threshold (couples the decision to amount semantics) and a sentinel customer id (a magic id in provider logic). A real gateway integration would replace only this one registration.

### 5. The authorized amount is the order's own total

`PaymentAuthorized.Amount` is read from `stream.Aggregate.Total` at the translation hop, not threaded through `PaymentDecision`. The order total is the single source of truth for what was charged; the provider's transient reply carries only approve/decline + auth code. This keeps `PaymentDecision` a thin yes/no envelope and avoids a second copy of the amount that could drift.

### 6. Idempotency via a stream-state guard at the payment gate

Consistent with ADR 007 and the 4.2 outcome handlers: `PaymentDecisionHandler` acts only while the order sits at `stock_reserved`. A duplicate decision, or one for an order already confirmed / terminal / unknown, is a silent no-op. A duplicate `StockReserved` cascades no second `AuthorizePayment` (its handler's existing `awaiting_confirmation` guard returns null). The guard is visible domain code, not transport-level dedup — the teaching point 4.2 established.

## Risks / Trade-offs

- **The decline branch is non-terminal until 4.6.** A declined order records `PaymentAuthFailed` but its visible status stays `stock_reserved`, and the reserved stock is not released. Accepted and documented; 4.6 (with Inventory 2.3) closes it. The decline path is still asserted at the event level (the `PaymentAuthFailed` commit lands) so the behavior is pinned now.
- **Provider call inside the message pipeline.** Hop 2 calls the provider with no stream lock held (Decision 3), so a slow real provider would not block a Marten write; but a duplicate `AuthorizePayment` still calls the stub before hop 3's guard no-ops it. Harmless for the stub; a real provider would use an idempotency key.
- **`stock_reserved` is now transient on the happy path.** Two pre-existing tests that pinned it as terminal were updated (the 4.2 grant test and the cross-BC smoke). This is the spec-delta closure loop surfacing in tests — the 4.2 behavior was a temporary truth 4.3 supersedes.

## Open Questions

- **Cancellation on payment decline (4.6)** — deferred with its cross-BC stock release (Inventory slice 2.3). The `PaymentAuthFailed` commit this slice records is its precondition.
- **Payment timeout (4.7)** — `OrderPaymentTimeout` scheduling + the `OrdersAwaitingPayment*` Bruun projection remain unbuilt; no timer is scheduled yet.
