## Why

Reserved stock that is never released currently stays reserved indefinitely on confirmed orders — the Stock stream has no terminal event for the success path. This means the stream cannot tell its own story (was this reservation in-flight or permanently consumed?), the `StockLevelView` conflates in-flight holds with sold stock, and the invariant `Available + Reserved + Committed = ΣStockReceived` is unassertable. Slice 2.4 closes this gap by mirroring the existing release path (slice 2.3): every reservation now reaches a terminal event — `StockReleased` on cancellation or `StockCommitted` on confirmation.

## What Changes

- **Orders (4.4 amendment).** `PaymentDecisionHandler`'s approve branch — today `return null;` after appending `OrderConfirmed` — now **cascades a `CommitStock { orderId, lines }` message** carrying the order's lines. The handler's return type changes from `Contracts.ReleaseStock?` to `(Contracts.CommitStock?, Contracts.ReleaseStock?)` (nullable tuple — Wolverine sees both types at code-gen time and provisions outbound routing for each; a null tuple member simply doesn't cascade; see design.md Decision 2 for why `object?` was rejected). No new events on the Order stream — the commit is an Inventory concern, not an Orders concern.
- **Inventory (2.4).** A new `CommitStockHandler` consumes `Contracts.CommitStock` (auto-listened by conventional routing, mirroring `ReleaseStockHandler`). For each line it loads the SKU's `StockLevelView` and, **only if that order holds a reservation on the SKU** (`Reservations.Contains(orderId)`), appends a `StockCommitted { sku, orderId, quantity }` event. A line for which no reservation exists (duplicate delivery, already committed) is a per-SKU silent no-op. `StockLevelViewProjection` gains `Apply(StockCommitted)`: reserved `-= quantity`, committed `+= quantity`, and the order id is removed from `Reservations`.
- **Published language (ADR 014).** The cross-BC contract is a new `CritterMart.Contracts.CommitStock { orderId, lines: [{ sku, quantity }] }` — symmetric with `ReleaseStock`, keeping Inventory's wire language about *stock* rather than *orders*. Same divergence from the workshop's model-level `OrderConfirmed` trigger as the 2.3/4.6 `ReleaseStock` divergence from `OrderCancelled`.
- **`StockLevelView` gains a `Committed` counter.** The `Available + Reserved + Committed = ΣStockReceived` invariant is assertable after every fold.

## Capabilities

### New Capabilities

(None — the Inventory and Orders BC shapes are unchanged.)

### Modified Capabilities

- `stock-management`: Inventory gains the commit path — on a `CommitStock` message it commits each line's reservation (reserved falls, committed rises, the order id is dropped from the SKU's reservations), idempotently skipping any SKU the order does not hold. (One ADDED requirement: commit reserved stock on confirmation.)
- `order-lifecycle`: the Order aggregate's confirm path now cascades `CommitStock` to Inventory carrying the order's lines. (One MODIFIED requirement: confirm now has an Inventory consequence.)

## Impact

- **Orders.** `PaymentDecisionHandler.Handle` return type changes from `Task<Contracts.ReleaseStock?>` to `Task<(Contracts.CommitStock?, Contracts.ReleaseStock?)>` (nullable tuple — Wolverine sees both types at code-gen time and provisions outbound routing for each; a null member doesn't cascade). Approve path returns `(commitStock, null)`, decline `(null, releaseStock)`, guard `(null, null)`. Existing tests that assert on the return type will need updating.
- **Inventory.** New event `Stock/StockCommitted.cs` (`StockCommitted(string Sku, string OrderId, int Quantity)`). New handler `Features/CommitStock.cs` (`CommitStockHandler`, mirrors `ReleaseStockHandler`). `StockLevelView` gains `public int Committed { get; set; }`. `StockLevelViewProjection` gains `Apply(StockCommitted)`.
- **Contracts.** New `CritterMart.Contracts/CommitStock.cs` (`CommitStock`, `CommitStockLine`) — symmetric with `ReleaseStock`/`ReleaseStockLine`.
- **Context map.** Orders→Inventory edge gains a third message type (`CommitStock`).
- **No new packages, no new project, no new broker topology.** `CommitStock` rides existing conventional routing.
- **Tests:** Inventory unit fold for `StockCommitted` (the invariant); Inventory integration for `CommitStockHandler` (happy path + idempotent no-ops); Orders tracked-session confirm path (now cascades `CommitStock`); amend cross-BC smoke (approve path now produces a `CommitStock` that arrives at Inventory).
- **Downstream artifacts:** `design.md` + `tasks.md` in this consolidated PR; Workshop 001 → v1.6; Narrative 004 → v1.8; context map update; prompt + retro `implementations/014`.
