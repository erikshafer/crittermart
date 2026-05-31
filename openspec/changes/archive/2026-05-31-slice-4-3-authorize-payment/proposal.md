## Why

Slice 4.3 is the **second gate** of the Order aggregate's two-gate process manager (ADR 007). With the stock gate closed (slice 4.2), the order must clear payment before it can be confirmed. This change authorizes payment against a **stubbed in-process provider** and records the decision as a **Klefter local commit** on the Order stream — the same translation-decision pattern slice 4.2 used for Inventory's reply, but reached in-process rather than across the broker. Narrative 004's Moment 4 is this proposal's human-readable sibling.

This change **bundles slice 4.4** (confirm when both gates close) so the happy path reaches its terminal success state — `OrderConfirmed`, status `confirmed` — the payoff of the whole aggregate-as-process-manager arc. The bundle is clean: 4.4 is a **pure in-process aggregate decision** on stream state (both `StockReserved` and `PaymentAuthorized` present ⇒ confirm), so it pulls in no cross-BC traffic and no new dependency. Payment is always the second gate to close (it only starts after stock is reserved), so confirmation deterministically follows the authorization in the same transaction.

The **failure branch** (provider declines ⇒ `PaymentAuthFailed`) is recorded but **left non-terminal**: turning a declined order terminal requires `OrderCancelled { reason: "payment_declined" }` **and** the cross-BC release of the already-reserved stock (Inventory slice 2.3) — that is slice **4.6**, deliberately deferred to avoid pulling an Inventory slice into this PR. Until 4.6 lands, a declined order records `PaymentAuthFailed` and its visible status stays `stock_reserved`.

## What Changes

- When a `StockReserved` Klefter commit is recorded (slice 4.2's stock-gate handler), Orders cascades a single `AuthorizePayment { orderId, amount }` message — `amount` is the order total read from the Order stream. It has a **local handler**, so Wolverine's conventional routing keeps it **in-process** (local routing takes precedence over the RabbitMQ convention); it does not travel over the broker and uses no `CritterMart.Contracts` type (those are reserved for genuine cross-BC traffic, ADR 014).
- A stubbed `IPaymentProvider` decides the request. The round-one stub **always approves** with a synthetic `stub-…` auth code (payment is a vision.md non-goal); the provider is injected so tests swap a declining implementation to exercise the failure branch — no magic values in the domain payload.
- The provider's transient `PaymentDecision` is translated into a **Klefter local commit**: approve ⇒ append `PaymentAuthorized { orderId, authCode, amount }`, decline ⇒ append `PaymentAuthFailed { orderId, reason }`. The provider response is never read again — the Klefter event is the source of truth.
- On approval, because the stock gate is already closed, the same handler appends `OrderConfirmed` in the same transaction (the bundled slice 4.4 aggregate decision); `OrderStatusView` settles on `confirmed` (with `payment_authorized` as the transient intermediate the fold passes through).
- **Idempotency via a stream-state guard** (mirrors 4.2): the translation acts only while the order is at the payment gate (`stock_reserved`); a duplicate decision, or one for an order already confirmed / terminal / unknown, is a silent no-op. A duplicate `StockReserved` likewise cascades no second payment (its handler's existing guard returns null).
- `OrderStatusView` gains the `payment_authorized` and `confirmed` statuses and `Apply` methods for `PaymentAuthorized` and `OrderConfirmed`. `PaymentAuthFailed` carries no status change (like `StockReservationFailed`).
- **Out of scope (named deferrals):** cancellation on payment decline (4.6) and the cross-BC stock release it needs (Inventory 2.3); cancellation on payment timeout (4.7) and its `OrderPaymentTimeout` scheduling + `OrdersAwaitingPayment*` projection. No real payment integration (stubbed — vision.md). No async daemon / Marten event subscriptions (ADR 008).

## Capabilities

### Modified Capabilities

- `order-lifecycle`: the Order aggregate gains payment-gate behavior on its own stream — it authorizes payment when stock is reserved, records the stubbed provider's decision as a Klefter `PaymentAuthorized` (auth code + amount) or `PaymentAuthFailed`, and, with both gates closed, confirms (`OrderConfirmed`, status `confirmed`) — the terminal success state. The decision is idempotent under duplicate delivery via a stream-state guard. (Two ADDED requirements: authorize payment for a reserved order; confirm an order when both gates close.)

No new capability — the Orders BC's one-capability-per-aggregate shape (`shopping-cart` + `order-lifecycle`) is unchanged. Inventory is untouched (payment is wholly within Orders).

## Impact

- **Orders only.** New events `Order/PaymentAuthorized.cs`, `Order/PaymentAuthFailed.cs`, `Order/OrderConfirmed.cs`. New provider seam `Order/PaymentProvider.cs` (`AuthorizePayment`, `PaymentDecision`, `IPaymentProvider`, `StubPaymentProvider`). New handlers `Order/PaymentHandlers.cs` (`AuthorizePaymentHandler` calls the provider and cascades the decision; `PaymentDecisionHandler` records the Klefter commit and, on approval, confirms). `StockReservedHandler` now returns `AuthorizePayment?` to open the payment gate. `OrderStatusView` + projection extended. `Program.cs` registers `IPaymentProvider → StubPaymentProvider`.
- **No new packages, no new project, no broker topology.** The payment hops are local Wolverine messages.
- **Tests:** two pure-fold unit tests (`PaymentAuthorized` → `payment_authorized`; `OrderConfirmed` → `confirmed`) and a new `PaymentAuthorizationTests` tracked-session suite — happy (stub approves → `PaymentAuthorized` + `OrderConfirmed`, `confirmed`), decline (swapped declining `IPaymentProvider` → `PaymentAuthFailed`, no confirm), idempotent (duplicate `StockReserved` re-runs no payment). Two pre-existing tests that pinned `stock_reserved` as terminal were updated to reflect that 4.3 makes it transient: the 4.2 `StockReservationOutcomeTests` grant test now asserts only that the grant is recorded, and the cross-BC smoke now asserts the grant landed (its order proceeds to `confirmed` in-process).
- **Downstream artifacts:** `design.md` + `tasks.md` authored in this same consolidated PR; Narrative 004 → v1.3 (Moment 4).
