## Why

The Orders bounded context holds two event-sourced aggregates — Cart and Order (Workshop 001 § 2). Slice 3.1 opened the **Cart**; this change opens the **Order** with its genesis operation, **`PlaceOrder`** (Workshop 001 slice 4.1). Checkout is the hinge of the Customer's purchasing journey: it turns the working selection (the cart) into a commitment (an order). Narrative 004's Moment 2 (the Customer placing their order) is this proposal's human-readable sibling.

`PlaceOrder` is also the project's **first multi-stream atomic write** — one command appends to two streams owned by two aggregates: `OrderPlaced` on a new Order stream, and `CartCheckedOut` on the existing Cart stream, committed together in one transaction. That is why this change carries **two capability deltas**: a new `order-lifecycle` capability for the Order side, and an addition to the existing `shopping-cart` capability for the cart-checkout side.

## What Changes

- Introduce the `PlaceOrder { customerId }` command and a `POST /orders` HTTP surface (Workshop 001 § 3 storyboard).
- Resolve the customer's **open cart** (the same indexed `CartView` query slice 3.1 uses); snapshot its lines onto a new **Order** stream keyed by a generated `orderId`, appending `OrderPlaced { orderId, customerId, items, total }` where `total` is the sum of quantity × snapshot price.
- In the **same transaction**, append `CartCheckedOut { orderId }` to the Cart stream; the inline `CartView` flips `IsOpen` to false, freeing the customer to start a fresh cart (the partial-unique index from slice 3.1 already anticipates this).
- An **inline** `OrderStatusView` snapshot projects the order's lines, total, and status; slice 4.1 reaches only `awaiting_confirmation`. `GET /orders/{orderId}` reads it.
- **Failure paths:** no open cart → `409` (this also covers the workshop's "cart already checked out" — a checked-out cart is not open, so a repeat `PlaceOrder` finds none); an open-but-empty cart → `409 CartEmpty` (a defensive guard, unreachable until remove-item 3.2 ships).
- **Out of scope (named deferrals):** the cross-BC `ReserveStock` send (4.2), stubbed payment authorization (4.3), confirm/cancel (4.4–4.7). Crucially, **`OrderPaymentTimeout` scheduling and the `OrdersAwaitingPayment*` Bruun projection are deferred to slice 4.7** — slice 4.1 schedules no timeout, exactly as slice 3.1 deferred `CartActivityTimeout` to 3.4. No RabbitMQ in this slice.

## Capabilities

### New Capabilities

- `order-lifecycle`: managing the Order aggregate's stream from placement to its terminal `OrderConfirmed`/`OrderCancelled`. Slice 4.1 introduces **placing an order** (Order stream genesis + the inline `OrderStatusView`); later slices fold stock reservation (4.2), payment authorization (4.3), confirmation (4.4), and cancellation (4.5–4.7) into the same capability. This is the **second of the Orders BC's two capabilities** (one per aggregate, alongside `shopping-cart`). The name is deliberately *not* `order-fulfillment` — CritterMart models no shipping/delivery (vision.md non-goal); the aggregate's terminal is `OrderConfirmed`.

### Modified Capabilities

- `shopping-cart`: gains the **checkout** behavior — a cart is terminated (`CartCheckedOut`, `IsOpen` → false) when its customer places an order. The event belongs to the Cart aggregate (Workshop 001 § 4 lists `CartCheckedOut` under the Cart), so it is a `shopping-cart` requirement even though `PlaceOrder` (an `order-lifecycle` command) triggers it.

## Impact

- **New aggregate in `CritterMart.Orders`:** the Order stream (`StreamIdentity.AsString`, keyed by a generated `orderId`, parallel to the Cart's `cartId`), its `OrderPlaced` event + `OrderLine`, and the inline `OrderStatusViewProjection` (ADR 008; no async daemon). No new service — Orders already exists (slice 3.1).
- **Cart aggregate gains a terminal event:** `CartCheckedOut` + a new `Apply` on `CartViewProjection`. The slice-3.1 partial-unique index (`Predicate "(data ->> 'IsOpen')::boolean = true"`) now does real work: a checked-out cart leaves the predicate, so a new open cart for the same customer no longer collides.
- **First multi-stream atomic write:** `StartStream<OrderStatusView>` + `FetchForWriting<CartView>` on one `IDocumentSession`, committed by `AutoApplyTransactions` in a single transaction. Raw `IDocumentSession` (not the `[Aggregate]` middleware) because the two stream ids are not both known up front — one is created, the other resolved by query.
- **HTTP surface:** `POST /orders { customerId }` → `201`, `Location: /orders/{orderId}`, body `{ orderId }`; `GET /orders/{orderId}` → `200` `OrderStatusView` / `404`. No synchronous service-to-service calls; no RabbitMQ.
- **Tests:** a new pure-function unit test for the `OrderStatusView` fold and one for the `CartCheckedOut` fold (CI unit job), plus four Alba + Testcontainers integration tests (happy path, no-open-cart, double-placement, HTTP read).
- **Downstream artifacts:** `design.md` + `tasks.md` are authored in this same consolidated PR (the slice runs as one PR per the consolidate-slice-prs convention, the Orders skeleton already existing).
