## 1. Payment events (Order stream)

- [x] 1.1 `Order/PaymentAuthorized.cs` — `PaymentAuthorized(string OrderId, string AuthCode, decimal Amount)` (Klefter local commit)
- [x] 1.2 `Order/PaymentAuthFailed.cs` — `PaymentAuthFailed(string OrderId, string Reason)` (Klefter local commit; no status change)
- [x] 1.3 `Order/OrderConfirmed.cs` — `OrderConfirmed(string OrderId)` (terminal success)

## 2. Provider seam (Decisions 3, 4, 5)

- [x] 2.1 `Order/PaymentProvider.cs`: `AuthorizePayment(string OrderId, decimal Amount)` request; `PaymentDecision(string OrderId, bool Approved, string? AuthCode, string? Reason)` transient reply
- [x] 2.2 `IPaymentProvider.AuthorizeAsync(AuthorizePayment) → Task<PaymentDecision>`; `StubPaymentProvider` always approves with a `stub-{guid}` auth code
- [x] 2.3 `Program.cs`: register `IPaymentProvider → StubPaymentProvider`

## 3. Handlers — authorize + translate + confirm (Decisions 2, 3, 6; slice 4.4)

- [x] 3.1 `StockReservedHandler` returns `AuthorizePayment?` — cascade the payment request when the stock gate clears (null on the idempotent no-op)
- [x] 3.2 `AuthorizePaymentHandler.Handle(AuthorizePayment, IPaymentProvider)` → cascade `PaymentDecision` (provider call only; no stream access)
- [x] 3.3 `PaymentDecisionHandler.Handle(PaymentDecision, IDocumentSession)`: guard `status == stock_reserved`; approve → append `PaymentAuthorized` (amount = `Aggregate.Total`) + `OrderConfirmed`; decline → append `PaymentAuthFailed`
- [x] 3.4 `OrderStatusView`: `OrderStatus.PaymentAuthorized` + `OrderStatus.Confirmed`; `Apply(PaymentAuthorized)`, `Apply(OrderConfirmed)` folds (no `Apply(PaymentAuthFailed)`)
- [x] 3.5 `dotnet build` succeeds

## 4. Tests

- [x] 4.1 **Unit (pure fold)**: `PaymentAuthorized` → `payment_authorized`; `OrderConfirmed` → `confirmed`
- [x] 4.2 **Orders tracked-session (happy)**: `Contracts.StockReserved` → stream `OrderPlaced + StockReserved + PaymentAuthorized + OrderConfirmed`, status `confirmed`, authCode `stub-…`, amount = total
- [x] 4.3 **Orders tracked-session (decline)**: swapped declining `IPaymentProvider` on a one-off host → `PaymentAuthFailed { reason: "declined" }`, no `OrderConfirmed`, status stays `stock_reserved`
- [x] 4.4 **Orders tracked-session (idempotent)**: duplicate `StockReserved` re-runs no payment (stream still 4 events, `confirmed`)
- [x] 4.5 Update the two pre-existing tests that pinned `stock_reserved` as terminal: 4.2 `StockReservationOutcomeTests` grant test asserts only the grant is recorded; cross-BC smoke asserts the grant landed (order now proceeds to `confirmed`)

## 5. Verify + close

- [x] 5.1 `dotnet build` + `dotnet test` green (Catalog 7, Inventory 6, Orders 23, CrossBc 1 — 37 total, 0 failures)
- [x] 5.2 `openspec validate slice-4-3-authorize-payment --strict` passes
- [x] 5.3 Narrative 004 → v1.3 (Moment 4; `slices` adds 4.3 + 4.4; Document History row) — done in this PR
- [x] 5.4 Author `docs/{prompts,retrospectives}/implementations/009-slice-4-3-authorize-payment.md`; flag the 4.6 decline-cancel follow-up; `openspec archive` deferred to a post-merge `tidy:` step
