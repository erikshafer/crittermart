## Context

The Stock stream today has three event types (`StockReceived`, `StockReserved`, `StockReleased`) and two terminal outcomes for a reservation: released on cancellation, or... nothing on confirmation. This leaves the success path incomplete — reserved stock on confirmed orders stays reserved forever, making the stream non-self-describing and the `StockLevelView` unable to distinguish in-flight holds from permanently consumed stock.

The release path (slice 2.3/4.6) established the cross-BC messaging pattern: a published-language command in `CritterMart.Contracts` (ADR 014), conventional routing over RabbitMQ, per-SKU idempotent handler guarded by the `Reservations` list. The commit path mirrors this exactly.

## Goals / Non-Goals

**Goals:**
- Complete the Stock stream's reservation lifecycle: every `StockReserved` reaches a terminal event (`StockReleased` or `StockCommitted`)
- Add a `Committed` counter to `StockLevelView` enabling the invariant `Available + Reserved + Committed = ΣStockReceived`
- Follow the established cross-BC messaging pattern (ADR 014, `CritterMart.Contracts`, conventional routing)

**Non-Goals:**
- No new ADR — this follows the exact pattern ADR 014 established (third message type, not a new architectural decision)
- No changes to the Order stream — the commit is an Inventory concern; the Order aggregate already has its terminal `OrderConfirmed`
- No async daemon or event subscriptions (ADR 008)
- No § 8 Q2 implementation (symmetric cancel on stock-failure) — resolved as "no" in the workshop amendment

## Decisions

### 1. CommitStock command shape mirrors ReleaseStock

`CommitStock { orderId, lines: [{ sku, quantity }] }` — symmetric with `ReleaseStock`. Same rationale: Inventory needs the lines (its `Reservations` store only order-id strings), anti-corruption (Inventory's wire language is about stock, not orders), and reserve/release/commit symmetry.

**Alternative considered:** A lighter `CommitStock { orderId }` without lines, having Inventory look up which SKUs the order reserved. Rejected: the `Reservations` list stores order IDs, not quantities, so Inventory would need to scan streams to find quantities — the same wall that motivated lines on `ReserveStock` and `ReleaseStock`.

### 2. PaymentDecisionHandler returns a nullable tuple `(CommitStock?, ReleaseStock?)`

The handler's return type changes from `Task<Contracts.ReleaseStock?>` to `Task<(Contracts.CommitStock?, Contracts.ReleaseStock?)>`. Approve → `(commitStock, null)`, decline → `(null, releaseStock)`, guard/no-op → `(null, null)`. Wolverine sees both types at code-gen time and provisions outbound routing for each; a null tuple member simply doesn't cascade.

**Alternative considered:** Returning `object?`. While the Wolverine docs show this as valid for conditional cascading, Wolverine's *conventional routing* provisions outbound exchanges/queues based on types it discovers at startup/code-gen time. With `object?`, it cannot infer that `CommitStock` needs an outbound route — the message would fail to route over the broker. The tuple gives Wolverine compile-time visibility of both message types.

### 3. StockLevelView gains `Committed` (not just "clear from Reservations")

The fold for `StockCommitted` does three things: `Reserved -= qty`, `Committed += qty`, remove order from `Reservations`. This enables the `Available + Reserved + Committed = ΣStockReceived` invariant — a free checksum assertable in unit tests. Without the counter, committed stock silently vanishes from the numbers.

### 4. Per-SKU independent commit (not all-or-nothing)

Mirrors the release path: each line is committed independently. A line for which no reservation exists is a silent no-op. This handles partial delivery, duplicate messages, and race conditions identically to release.

## Risks / Trade-offs

- **Existing test churn.** The `PaymentDecisionHandler` return-type change from `Contracts.ReleaseStock?` to `(Contracts.CommitStock?, Contracts.ReleaseStock?)` breaks any test that asserts on the specific return type of the approve path (previously `null`). Mitigation: the approve-path tests now assert the returned tuple contains a `CommitStock` with the correct order ID and lines via `tracked.Sent.SingleMessage<Contracts.CommitStock>()`.
- **Cross-BC smoke test scope.** The existing approve-path cross-BC smoke currently ends at `OrderConfirmed`. It now needs to verify that `CommitStock` arrives at Inventory and the stock is committed. Mitigation: extend the existing smoke rather than adding a new one.
