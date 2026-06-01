## 1. Verify before wiring (design Open Questions; ctx7 `/jasperfx/wolverine`, `/jasperfx/marten`)

- [x] 1.1 Verify the Wolverine API for cascading a **scheduled** message (`DelayedFor` / `ScheduledAt` on cascading return values) and how it composes with `PlaceOrder`'s existing `(IResult, ReserveStock?)` tuple return
- [x] 1.2 Verify whether scheduled local messages are durable across a service restart under Marten persistence, and what configuration that requires; record the answer (it shapes the demo story + Program.cs config)
- [x] 1.3 Verify how tracked sessions expose scheduled-but-not-executed envelopes for test assertion
- [x] 1.4 Verify Marten 9's conditional-delete convention on `SingleStreamProjection` partial classes (`ShouldDelete` signature)

## 2. Orders — the scheduled timeout and the cancel handler (design Decisions 1, 2, 6)

- [x] 2.1 `Order/OrderPaymentTimeout.cs` — `OrderPaymentTimeout(string OrderId)`, an Orders-local self-message (not a Contracts type)
- [x] 2.2 `Order/OrderCancelled.cs` — add `CancelReason.PaymentTimeout = "payment_timeout"`
- [x] 2.3 `Order/PaymentTimeoutHandler.cs` — `Handle(OrderPaymentTimeout, IDocumentSession)` returning `Task<Contracts.ReleaseStock?>`: `FetchForWriting<OrderStatusView>`; terminal or unknown → `null` (no append, no cascade); non-terminal → append `OrderCancelled { payment_timeout }` and return `ReleaseStock` built from `Aggregate.Lines` (always — Decision 2)
- [x] 2.4 `Features/PlaceOrder.cs` — cascade the scheduled `OrderPaymentTimeout` (delay from config) alongside the existing `ReserveStock` cascade, using the API verified in 1.1
- [x] 2.5 `dotnet build` (Orders) succeeds

## 3. Orders — the Bruun todo-list projection + endpoint (design Decision 3)

- [x] 3.1 `Order/OrdersAwaitingPayment.cs` — `OrderAwaitingPayment` document (Id = orderId, CustomerId, Total, Deadline) + `OrdersAwaitingPaymentProjection` (inline single-stream): create on `OrderPlaced`, conditional-delete on `OrderConfirmed` and on `OrderCancelled` (any reason)
- [x] 3.2 `Features/PlaceOrder.cs` (or sibling) — `GET /orders/awaiting-payment` returning the current rows
- [x] 3.3 `Program.cs` — register `OrdersAwaitingPaymentProjection` inline (ADR 008)

## 4. Configuration (design Decision 4)

- [x] 4.1 `appsettings.json` — `"Orders": { "PaymentTimeout": "00:10:00" }`
- [x] 4.2 `Program.cs` — read `Orders:PaymentTimeout` (default 10 minutes) and surface it to the `PlaceOrder` schedule + the projection's deadline computation
- [x] 4.3 Apply any durability configuration required by the answer to 1.2

## 5. Tests (design Decision 5 — no real-time waiting, no new cross-BC smoke)

- [x] 5.1 **Orders unit (pure folds)**: `OrdersAwaitingPaymentProjection` — `OrderPlaced` creates the row (id, customer, total, deadline); `OrderConfirmed` deletes; `OrderCancelled` deletes (any reason)
- [x] 5.2 **Orders tracked-session (schedule)**: placing an order captures a scheduled `OrderPaymentTimeout` envelope for the new orderId (amend `PlaceOrderTests`)
- [x] 5.3 **Orders tracked-session (timeout cancels at the payment gate)**: order at `stock_reserved` → invoke `OrderPaymentTimeout` → stream appends `OrderCancelled { payment_timeout }`, status `cancelled`, exactly one `ReleaseStock` cascaded with the order's lines
- [x] 5.4 **Orders tracked-session (timeout cancels an unanswered order)**: order at `awaiting_confirmation` → invoke `OrderPaymentTimeout` → cancelled + `ReleaseStock` still cascaded (the delayed-grant race, spec scenario 3)
- [x] 5.5 **Orders tracked-session (no-op on confirmed)**: confirmed order → invoke `OrderPaymentTimeout` → no event appended, no cascade
- [x] 5.6 **Orders tracked-session (no-op on duplicate)**: order already cancelled by timeout → invoke `OrderPaymentTimeout` again → no event appended, no cascade
- [x] 5.7 **Orders integration (todo-list endpoint)**: `GET /orders/awaiting-payment` lists a placed order's row; the row disappears after the order reaches a terminal state

## 6. Verify + close

- [x] 6.1 `dotnet build` + `dotnet test` green (full solution; Inventory + Catalog + CrossBc untouched and still green)
- [x] 6.2 `openspec validate slice-4-7-cancel-on-payment-timeout --strict` passes
- [x] 6.3 Narrative 004 → v1.5 (Moment 6; `slices` adds 4.7; Document History row) — in this PR
- [x] 6.4 Author `docs/retrospectives/implementations/011-slice-4-7-cancel-on-payment-timeout.md`; record the durability answer (1.2) and whether the Order lifecycle is now complete for round one; `openspec archive` deferred to a post-merge `tidy:` step
