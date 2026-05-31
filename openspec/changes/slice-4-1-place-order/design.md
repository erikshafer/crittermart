## Context

Slice 4.1 opens the **Order** aggregate, the second of the Orders BC's two event-sourced aggregates (the Cart, slice 3.1, is the first). It runs as **one consolidated PR** (narrative bump + proposal + implementation), per the consolidate-slice-prs convention — the Orders service skeleton already exists, so the blueprint-architecture exception that made slice 3.1 a per-edge bootstrap no longer applies.

`PlaceOrder` is checkout: it turns the customer's open cart into an order. Mechanically it is the project's **first multi-stream atomic write** — `OrderPlaced` on a new Order stream and `CartCheckedOut` on the existing Cart stream, committed together. The cart side reuses slice 3.1's machinery wholesale: the same open-cart resolution query and the partial-unique `CartView` index, which was written in 3.1 *specifically anticipating* this slice flipping `IsOpen`.

## Goals / Non-Goals

**Goals:**
- `PlaceOrder { customerId }` create-and-checkout: resolve the customer's open cart → start a new Order stream (`OrderPlaced`) and check the cart out (`CartCheckedOut`) in one transaction.
- Inline `OrderStatusView` snapshot projecting the order's lines, total, and status (`awaiting_confirmation`) from a pure fold.
- Fold `CartCheckedOut` into `CartViewProjection` (flip `IsOpen`), exercising the slice-3.1 partial-unique index for the first time.
- A pure-function unit test for each new fold, plus Alba + Testcontainers integration tests for the happy path and the two rejection paths.

**Non-Goals:** stock reservation cross-BC (4.2); payment authorization (4.3); confirm/cancel (4.4–4.7). **No `OrderPaymentTimeout` scheduling and no `OrdersAwaitingPayment*` projection** — both deferred to slice 4.7, the slice that consumes them (mirrors 3.1 deferring `CartActivityTimeout` to 3.4; this slice schedules nothing). No RabbitMQ. No Catalog read (lines come snapshotted from the cart, which snapshotted them from the frontend).

## Decisions

### 1. Slice scope is the checkout transaction only — timeout/Bruun deferred to 4.7

Workshop § 6.1 slice 4.1's happy path lists `OrdersAwaitingPayment*` row-add and `OrderPaymentTimeout` scheduling alongside the checkout write. Those exist only to feed slice 4.7's Bruun timeout-cancel; nothing in 4.1–4.6 reads them. Shipping them in 4.1 would mean a scheduled message firing into a non-existent handler. Deferring them to 4.7 keeps 4.1 atomic and is the exact precedent slice 3.1 set (it deferred the whole `CartActivityTimeout` apparatus to 3.4). The workshop slice-table intent is preserved; only the timing of the timeout machinery moves.

### 2. The Order capability is `order-lifecycle`, not `order-fulfillment`

Orders is one-capability-per-aggregate (slice 3.1 established this): `shopping-cart` for the Cart, a new capability for the Order. "Fulfillment" carries e-commerce baggage — picking, packing, shipping, delivery — that CritterMart explicitly does not model (`vision.md:28` non-goal: "no shipping rate calculations"; the Order's terminal success is `OrderConfirmed`, after which nothing ships). Naming the capability `order-fulfillment` would invite a future reader, or a future shipping BC, to assume this capability owns logistics. `order-lifecycle` names exactly what the Order aggregate owns: the stream from `OrderPlaced` to a terminal `OrderConfirmed`/`OrderCancelled`. (`vision.md:39` does say "process manager for fulfilling a purchase", but loosely — "see the purchase through", not the logistics sense.)

### 3. One command, two capabilities — the spec delta splits

`PlaceOrder` writes two streams owned by two aggregates. The change therefore carries two spec deltas: `order-lifecycle` ADDED (place an order — the Order stream side) and `shopping-cart` ADDED (check out the cart — the Cart stream side). `CartCheckedOut` is a Cart-aggregate event (Workshop § 4 lists it under the Cart), so the checkout requirement is honestly a `shopping-cart` concern even though an `order-lifecycle` command triggers it. This faithfully teaches that a vertical slice can cross capability boundaries inside one bounded context.

### 4. "Already checked out" collapses into open-cart resolution

The handler resolves the customer's *open* cart with the same indexed `CartView` query `AddToCart` uses. A checked-out cart has `IsOpen=false`, so it is simply not found — a repeat `PlaceOrder` returns `409 NoOpenCart`, which is exactly the workshop's "cart already checked out → rejected, no new Order stream" behavior, achieved without a dedicated guard. The integration test proves it (place twice; second is `409`; exactly one order exists). `CartEmpty` remains a separate, defensive guard: an open cart with zero lines is unreachable in 4.1 (a cart is born with a line, and remove-item is 3.2), but the guard is in place the moment 3.2 makes a lineless-open cart possible.

### 5. Raw `IDocumentSession`, not the `[Aggregate]` middleware

Wolverine's `[Aggregate]`/`[WriteAggregate]` middleware shines when the stream id(s) arrive on the command. Here neither id is known up front: the Order id is generated, and the Cart id is resolved by a query on `customerId`. So the endpoint takes a raw `IDocumentSession`, resolves, `StartStream`s the order, `FetchForWriting`s the cart, and lets `AutoApplyTransactions` commit both. This matches the slice-3.1 `AddToCart` shape and keeps the multi-stream write explicit and readable — the teaching point of the slice.

### 6. HTTP surface — `POST /orders`, read by `orderId`

`POST /orders { customerId }` (matching the Workshop § 3 storyboard's `POST /orders`) → `201`, `Location: /orders/{orderId}`, body `{ orderId }`; `GET /orders/{orderId}` → `OrderStatusView` (`LoadAsync`, `404` if none). The order is the created resource, keyed by its stream id — the same write-then-read-by-stream-id shape as the cart.

## Risks / Trade-offs

- **Query-then-write is not atomic with the cart resolution** → same trade-off slice 3.1 accepted; acceptable for a single customer acting sequentially in round one. The cart append uses `FetchForWriting` (optimistic concurrency), and a customer cannot place two orders concurrently in the demo.
- **`OrderStatusView.Status` is a `string`, not an enum** → chosen to keep the snake_case workshop status names (`awaiting_confirmation`, …) readable in JSON without a serializer converter, and to keep the fold a trivially unit-testable pure function. The status set grows by constant as 4.2–4.7 land.
- **`OrderLine` duplicates `CartLine`'s shape** → intentional aggregate separation; the Order owns its own line type in the `Order` namespace rather than coupling to the Cart's. The mapping is one `Select`.
- **Total is computed at placement and frozen** → correct by design (the cart snapshot is authoritative through the order; no re-pricing). Recorded on `OrderPlaced` so the order's total is reconstructable from its own stream.

## Open Questions

- None blocking. Whether 4.5's stock-failure cancellation publishes a symmetric cross-BC `OrderCancelled` (Workshop § 8 open question 2) is a 4.5 concern, untouched here.
