## Why

After slice 4.6, an order whose payment is *declined* cancels itself — but an order that **never hears anything at all** waits forever: if Inventory's reply is lost crossing the broker, or the payment provider never answers, the order sits non-terminal indefinitely and its reserved stock stays held. Slice 4.7 closes this last cancellation path with a deadline: every placed order schedules an `OrderPaymentTimeout` self-message, and an order still non-terminal when it fires is cancelled and its stock released. This is the project's **first temporal automation** (Bruun pattern, Workshop § 5/§ 6 slice 4.7) — time passing, not an arriving message, is the trigger — and it completes the Order lifecycle for round one.

## What Changes

- **Orders — schedule the deadline (4.1's deferred clause).** `PlaceOrder` additionally cascades a **scheduled** `OrderPaymentTimeout { orderId }` self-message, delivered back to Orders after a config-driven delay (`Orders:PaymentTimeout`, default 10 minutes). The Workshop's slice 4.1 writes-to column named this schedule; it was explicitly deferred to 4.7 (the same deferral pattern as `CartActivityTimeout` → 3.4).
- **Orders — cancel on timeout.** A new `PaymentTimeoutHandler` consumes the fired `OrderPaymentTimeout`. Terminal-state guard first: an order already `confirmed` or `cancelled` is a **silent no-op** (the timer lost the race — that is its normal, expected fate for every successfully confirmed order). A non-terminal order — whether stuck at `awaiting_confirmation` (no Inventory reply) or `stock_reserved` (no provider answer) — appends `OrderCancelled { reason: "payment_timeout" }` (status `cancelled`) and **cascades `ReleaseStock { orderId, lines }`** to Inventory.
- **`CancelReason.PaymentTimeout`** (`"payment_timeout"`) is added to the Orders `CancelReason` constants; the existing `OrderCancelled` event and its `Apply` fold are reused unchanged.
- **Orders — the Bruun todo-list projection.** A new inline `OrdersAwaitingPayment*` read model: a row per order awaiting its terminal state (order id, customer id, total, deadline), **created** when `OrderPlaced` folds and **deleted** when a terminal event (`OrderConfirmed` / `OrderCancelled`) folds — Marten conditional delete, a new teaching beat. Exposed at `GET /orders/awaiting-payment`. The timeout handler does **not** read it (the Order stream is the single source of truth for the guard); it is the observable face of the automation.
- **Inventory — no changes.** The release side reuses slice 4.6's `CritterMart.Contracts.ReleaseStock` and `ReleaseStockHandler` **unchanged** — the reuse 4.6's design promised (its Decision 3 built the per-SKU "no reservation → no-op" guard precisely for this slice). The timeout-cancel releases **unconditionally** (even when Orders never recorded a stock grant) and lets Inventory's guard decide — this is what survives the Workshop § 4.7 delayed-grant race.
- **Out of scope (named deferrals):** cart abandonment (3.4 — the other Bruun automation), cart edits (3.2/3.3), `StockCommitted` / commit-on-confirmation (Workshop § 8 future-ADR candidate), async daemon / event subscriptions (ADR 008), real payment integration (vision.md non-goal).

## Capabilities

### New Capabilities

*(none — the Orders BC keeps its two capabilities, `shopping-cart` + `order-lifecycle`)*

### Modified Capabilities

- `order-lifecycle`: gains the temporal cancellation path — placing an order schedules a payment-deadline self-message; an order still non-terminal when it fires is cancelled with reason `payment_timeout` and its stock released; an order that settled first makes the timer a no-op. Also gains the orders-awaiting-payment todo-list read model (row per non-terminal order, removed on terminal). (Two ADDED requirements: *cancel an order on payment timeout*; *track orders awaiting payment*.)

*(`stock-management` is **not** modified — the release requirement shipped in 4.6 already covers the per-SKU idempotent no-op this slice relies on.)*

## Impact

- **Orders.** New `Order/OrderPaymentTimeout.cs` (the scheduled self-message — a local message, not a published-language contract; it never crosses the wire). New `Order/PaymentTimeoutHandler.cs` (terminal guard → cancel + unconditional `ReleaseStock` cascade). `Order/OrderCancelled.cs` gains `CancelReason.PaymentTimeout`. New `Order/OrdersAwaitingPayment.cs` (view + inline single-stream projection with conditional delete). `Features/PlaceOrder.cs` gains the scheduled cascade and the `GET /orders/awaiting-payment` endpoint. `Program.cs` registers the projection and reads `Orders:PaymentTimeout`; `appsettings.json` carries the default.
- **Inventory.** Untouched. `ReleaseStock` contract, `ReleaseStockHandler`, `StockLevelView` fold all reused as shipped in 4.6.
- **No new packages, no new project, no new broker topology.** `OrderPaymentTimeout` has a local handler, so conventional local routing keeps it in-process (the same precedence verified in 4.3); only the reused `ReleaseStock` crosses the wire.
- **Tests:** Orders pure-fold units for the `OrdersAwaitingPayment*` projection (create on placed, delete on confirmed/cancelled); tracked-session assertion that placing an order schedules the timeout envelope; tracked-session timeout-handler tests by direct invocation (non-terminal → cancel + release cascade; confirmed → no-op; already-cancelled/duplicate → no-op); `GET /orders/awaiting-payment` integration. **No real-time waiting and no new cross-BC smoke** (4.6's decline→release smoke already proves the `ReleaseStock` wire path; prompt 011 decision 5).
- **Downstream artifacts:** `design.md` + `tasks.md` in this consolidated PR; Narrative 004 → v1.5 (Moment 6); prompt + retro `011`.
