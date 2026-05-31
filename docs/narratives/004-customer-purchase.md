---
narrative: 004
title: The Customer Buys From the Catalog
actor: Customer
status: draft
version: v1.4
slices: [3.1, 4.1, 4.2, 4.5, 4.3, 4.4, 4.6, 2.3]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 2 Orders BC / Cart + Order aggregates, § 3 Place Order storyboard, § 4 vocabulary, § 5 slices 3.1 + 4.1, § 6.1 GWT)
  - docs/narratives/002-customer-browse-catalog.md (the browsing step this journey continues from)
  - docs/narratives/001-seller-manage-catalog.md (the price-snapshot non-event, mirrored here from the Customer's side)
  - docs/context-map/README.md (product fields reach the Cart only via the frontend snapshot; no Catalog↔Orders integration)
---

# Narrative 004 — The Customer Buys From the Catalog

This is the same Customer as [Narrative 002](002-customer-browse-catalog.md), now past browsing and deciding to buy. Their journey here is the purchasing arc: filling a **cart**, placing an **order**, and watching it get fulfilled. Where the Catalog is a document store, the **Orders bounded context is event-sourced** — the Cart is an event-sourced aggregate (a stream of what the Customer added, removed, and changed), and the readable cart contents are projected from those events. The Customer's identity is stubbed for round one (a hardcoded customer ID flows through their commands, per ADR 009).

## Journey scope

The Customer's purchasing journey threads several Orders slices:

- **Slice 3.1 — Add item to cart.** Moment 1 below covers adding the first item.
- **Slice 4.1 — Place order from cart.** Moment 2 below covers checking out: the cart becomes an order.
- **Slice 4.2 — Reserve stock cross-BC**, and **slice 4.5 — cancel on stock failure.** Moment 3 below covers the first thing that happens to a placed order: Orders asks Inventory to set the goods aside, over the message broker, and the order either advances to *stock reserved* or — if the goods aren't there — is cancelled.
- **Slice 4.3 — Authorize payment (stubbed)**, and **slice 4.4 — confirm when both gates close.** Moment 4 below covers the second gate and the journey's payoff: with stock reserved, Orders authorizes payment against a stubbed provider and — on approval — *confirms* the order, its terminal success state.
- **Slice 4.6 — Cancel on payment decline**, and **Inventory slice 2.3 — release the reserved stock.** Authored in this version. Moment 5 below covers what happens when payment is *refused*: the order cancels itself and, because stock was already reserved, hands it back to Inventory across the broker — the journey's first cancellation that crosses a boundary *back*.

Forthcoming (not authored here): editing the cart — remove (3.2), change quantity (3.3); the *last* cancellation of order processing — payment timeout (4.7); and **cart abandonment** (3.4) if the cart is left inactive. Each extends this narrative when its slice is authored.

Browsing the catalog is the prior step ([Narrative 002](002-customer-browse-catalog.md)); catalog *management* is the Seller's journey ([Narrative 001](001-seller-manage-catalog.md)).

## Moment 1 — Adding the first item to the cart

**Context.** The Customer has been browsing the storefront ([Narrative 002](002-customer-browse-catalog.md)) and has settled on the *Cosmic Critter Plush* (SKU `crit-001`, listed at `$24.99`). They have no cart yet — this is their first action toward a purchase.

**Interaction.** On the product listing, the Customer adds the *Cosmic Critter Plush* to their cart (quantity 1). The frontend already read this product from the Catalog when rendering the listing, so it **snapshots** the relevant fields — name and price — into the command: `AddToCart { customerId, sku: "crit-001", quantity: 1, productSnapshot: { name: "Cosmic Critter Plush", price: 24.99 } }`.

**System response.** The Orders service finds no Cart stream for this customer, so it **creates one**: it appends `CartCreated { customerId }` and then `CartItemAdded { sku: "crit-001", quantity: 1, snapshot }` to the new stream. The `CartView` projects from those events and shows a single line — *Cosmic Critter Plush*, quantity 1, at the snapshot price `$24.99`. If the Customer then adds a second product — say the *Nebula Newt* (`crit-002`, `$18.00`), quantity 3 — a second `CartItemAdded` is appended to the **same** cart, and `CartView` shows two lines. The cart is the Customer's working selection: a running event stream they keep shaping until they check out (slice 4.1) or it lapses (slice 3.4).

The price in the cart is the price **snapshotted at add-to-cart time**. This is the Customer-side mirror of the non-event named in [Narrative 001](001-seller-manage-catalog.md): product information reaches the cart only through the frontend's snapshot (the context map calls this presentation-layer composition, not a bounded-context integration), and the cart's snapshot is authoritative through checkout.

## Moment 2 — Placing the order

**Context.** The Customer has shaped their cart through Moment 1 — say two lines: the *Cosmic Critter Plush* (`crit-001`, quantity 2 at the snapshot price `$24.99`) and the *Nebula Newt* (`crit-002`, quantity 3 at `$18.00`). They are on the cart-review screen and decide to buy.

**Interaction.** The Customer taps **Place Order**. The frontend sends `PlaceOrder { customerId }` — and nothing else: the order's contents are not re-sent from the browser. What the Customer is buying is whatever their **open cart** already holds, server-side.

**System response.** The Orders service resolves the Customer's open cart (the same open-cart lookup that Moment 1 relied on), reads its lines, and performs a **single atomic step that touches two streams at once**:

- on a brand-new **Order** stream (keyed by a generated `orderId`), it appends `OrderPlaced { orderId, customerId, items, total }` — the cart's lines are **frozen** onto the order, and the **total** is computed from the snapshot prices (here `2 × 24.99 + 3 × 18.00 = $103.98`);
- on the **Cart** stream, it appends `CartCheckedOut { orderId }` — the cart is now closed.

Because both appends commit in one transaction, the Customer can never end up with a placed order whose cart is still open, or a closed cart with no order. The `OrderStatusView` now reads `awaiting_confirmation`, and the closed cart is no longer the Customer's open cart — so their next visit to the storefront starts a fresh one. The cart they just checked out is not deleted; it remains as readable history, marked closed.

**Why the order keeps its own copy of the lines.** The order does not point back at the cart, and it does not read the Catalog. The prices and names it shows are the ones snapshotted into the cart at add time and then copied onto the order at checkout — authoritative for this order forever, even if the Seller re-prices the product or the Customer later starts a new cart. The order is reconstructable entirely from its own stream.

**What the Customer sees next is a status, not a shipment.** "Placing" an order in CritterMart means it has been *recorded and is awaiting confirmation* — the system will next try to reserve stock and authorize (stubbed) payment behind the scenes. It does **not** mean anything has been picked, packed, or shipped; CritterMart models no logistics. The terminal the order is heading toward is **confirmed** (or cancelled), not delivered.

## Moment 3 — Reserving the stock (across the boundary, behind the scenes)

**Context.** The order sits at `awaiting_confirmation` (Moment 2). From here the Customer takes no action — what follows is the system working the order toward confirmation, and the Customer sees only a status that changes. This is the first moment in the whole journey that **crosses a bounded-context boundary**: the **Orders** service has recorded an intent to buy, but it is the **Inventory** service that owns the goods. Before the order can be confirmed, the stock has to be set aside.

**Interaction.** None from the Customer. Placing the order is itself the trigger — the moment `OrderPlaced` lands, Orders asks Inventory to reserve the order's lines.

**System response.** Orders sends a `ReserveStock { orderId, sku, quantity }` **message** to Inventory **over the message broker** (RabbitMQ). This is the first time anything in CritterMart travels between services rather than within one — and it is exactly the cross-service hop the talk's distributed trace follows, from the Orders handler, across the broker, into Inventory, and back. Inventory receives the message and decides against its own per-SKU **Stock** stream:

- **If enough is available**, Inventory appends `StockReserved` to the Stock stream — available drops, reserved rises — and sends a `StockReserved` message back to Orders. Orders records that answer as a **`StockReserved` event on the Order's own stream**, and `OrderStatusView` moves to `stock_reserved`. The order has cleared its first gate.
- **If the goods aren't there** (the SKU is short, or has none), Inventory changes **nothing** on the Stock stream — a refusal is not a state change, so there is nothing to record there — and sends back a `StockReservationFailed { orderId, sku, reason }` message. Orders records a `StockReservationFailed` on the Order stream and then **cancels the order**, appending `OrderCancelled { reason: "stock_unavailable" }`; `OrderStatusView` reads `cancelled`. Because nothing was ever reserved, there is nothing to hand back — the cancellation stays entirely inside Orders and crosses no boundary back to Inventory.

**Why Orders keeps its own copy of Inventory's answer.** `StockReserved` exists as *two* facts: one on Inventory's Stock stream (a real change to what's on hand) and a separate one on the Order's stream (Orders recording the decision it received). The order does not reach back into Inventory to ask "am I still reserved?" — the answer it was given is written into its own history as a first-class event, and that local record is what the order reasons about from now on. The same is true of a refusal: `StockReservationFailed` lives only on the Order stream, because Inventory had no state change to record. The order's stream stays a complete, self-contained account of everything that happened to it.

**Why a re-sent message can't double-book.** Messages over the broker can arrive more than once (a network hiccup, a retry). Both services are built to shrug that off. If Inventory sees a second `ReserveStock` for an order it has already reserved, it doesn't reserve again — it recognises the existing reservation and lets the duplicate pass. If Orders sees a `StockReserved` for an order that is already past this gate (or already cancelled), it ignores it. So the Customer's order is reserved exactly once no matter how the messages bounce around — the correctness is a property of each side checking its own stream, not of the broker delivering perfectly.

## Moment 4 — Authorizing payment and confirming the order (behind the scenes)

**Context.** The order cleared its first gate in Moment 3 and sits at `stock_reserved`. One gate remains: payment. As in Moment 3, the Customer takes no action — the system works the order toward its terminal, and the Customer sees only a status that settles. The difference from Moment 3 is that this gate **does not cross a boundary**: paying is Orders' own concern, handled in-process against a stubbed provider (CritterMart stubs payment — there is no real gateway; vision.md's non-goal).

**Interaction.** None from the Customer. Reserving the stock is itself the trigger — the moment the `StockReserved` grant lands on the Order stream, Orders moves to authorize payment.

**System response.** Orders sends an `AuthorizePayment { orderId, amount }` request to the stubbed provider, where `amount` is the order's own total (the figure frozen at checkout — `$103.98` for our two-line order). Unlike Moment 3's broker hop, this request **never leaves the Orders service**: it is an in-process message, and the talk's distributed trace shows it staying within Orders, in contrast to the cross-service jump that preceded it. The provider answers with a decision, and Orders records that decision as a **`PaymentAuthorized` event on the Order's own stream** — a Klefter local commit, exactly like the stock grant before it, carrying the provider's auth code and the amount. The provider's reply is transient; the event on the stream is the durable truth.

- **If payment is authorized** (the round-one stub always approves), the order has now closed **both** gates — stock reserved *and* payment authorized. That is the whole condition the Order aggregate has been waiting for, so it **confirms itself**: it appends `OrderConfirmed` to the stream, and `OrderStatusView` settles on `confirmed`. This is the journey's payoff. "Confirmed" is as far as the order travels — CritterMart models no picking, packing, or shipping; the terminal of a successful purchase is a *confirmed order*, not a delivered parcel.
- **If payment is declined**, Orders records a `PaymentAuthFailed` on the stream — the refusal, captured as a first-class fact — and the order does **not** confirm. What happens next — the order cancelling itself and handing the reserved stock back to Inventory — is **Moment 5**.

**Why confirmation is the aggregate's own decision.** No outside actor tells the order it is confirmed. The Order stream *is* the process state (the aggregate acting as its own process manager, ADR 007): each gate writes a fact onto the stream, and when the stream shows both `StockReserved` and `PaymentAuthorized` with no terminal event, the only correct conclusion is `OrderConfirmed`. Because payment is always the second gate to close — it only starts after stock is reserved — the confirmation is written in the very same step as the payment authorization. The order needs nothing but its own history to know it is done.

**Why a re-delivered message can't double-charge or double-confirm.** As in Moment 3, the safety is a property of the order checking its own stream, not of perfect delivery. Orders authorizes payment only while the order sits at the payment gate (`stock_reserved`); once it has moved on — `confirmed`, or recorded a decision already — a repeated request changes nothing. So the order is authorized and confirmed exactly once, however the in-process messages are retried.

## Moment 5 — Cancelling a declined order and handing the stock back (behind the scenes)

**Context.** Payment was *declined* (Moment 4's second branch). The order's stream carries a `PaymentAuthFailed`, but the order is not yet finished: its visible status would still read `stock_reserved`, and the goods set aside for it in Inventory (Moment 3) are still held. Something has to make the order terminal *and* give the stock back. As with Moments 3 and 4, the Customer takes no action — the system settles the order, and the Customer sees a status reach its end state.

**Interaction.** None from the Customer. Recording the decline is itself the trigger: in the very same step that writes `PaymentAuthFailed`, the Order aggregate makes its decision.

**System response.** This is the mirror image of Moment 3's stock-failure cancellation — but with a crucial difference. There, the order was cancelled and *nothing* crossed back to Inventory, because a refused reservation had reserved nothing. Here, the reservation **succeeded** in Moment 3; the stock is really being held. So cancelling the order is two coordinated facts:

- **Inside Orders**, the aggregate appends `OrderCancelled { reason: "payment_declined" }` to its own stream, right after the `PaymentAuthFailed` and in the same commit. `OrderStatusView` settles on `cancelled` — the order's terminal state, the same terminal a stock-failure cancellation reaches, only by a different reason.
- **Across the boundary**, Orders sends a `ReleaseStock { orderId, lines }` **message** to Inventory over the broker — the journey's first message that carries a *cancellation* back to the service that owns the goods. It is the symmetric counterpart of Moment 3's `ReserveStock`: where that asked Inventory to set the lines aside, this asks Inventory to put them back. Inventory receives it and, for each line it still holds a reservation against, appends `StockReleased` to that SKU's Stock stream — available rises back, reserved falls, and the order is dropped from the reservations it tracks. The goods are on the shelf again for the next Customer.

**Why "release," not "an order was cancelled."** Orders could have told Inventory "this order was cancelled" and let Inventory work out what to do. It doesn't. It sends a request phrased in *Inventory's* own language — *release this stock* — carrying exactly the lines to give back. Inventory never has to know what an *order* is or why it was cancelled; it only knows about stock and reservations. That keeps the two services' vocabularies from bleeding into each other (the published-language contract of `CritterMart.Contracts`, ADR 014): the order's reason for cancelling is Orders' private business, while the wire only ever speaks of stock.

**Why a re-delivered release can't over-release.** As with reserving (Moment 3), the safety is each side checking its own stream. Inventory releases a SKU only if it still holds a reservation for that order; a repeated `ReleaseStock` finds the reservation already gone and does nothing. And because the order reaches the payment gate *only after* Inventory's grant came back, by the time Orders sends the release the stock is guaranteed to be held — so the release always finds something to give back. (The same release message and the same guard will serve the *other* way an order can fail at the payment gate — a timeout, slice 4.7 — without any change to Inventory.)

## What the Customer does *not* yet see

- **No re-pricing of the cart.** If the Seller changes `crit-001`'s price (slice 1.3) after it is in the cart, the cart still shows the snapshot `$24.99`. The snapshot wins until checkout — by design.
- **No stock check at *add* time.** Adding an item to the cart still neither verifies nor reserves stock — that only happens once the order is placed (Moment 3). So a Customer can fill a cart with more than exists; the shortfall surfaces at reservation, as a cancelled order, not at add-to-cart.
- **No payment timeout yet.** A declined payment now cancels the order and releases its stock (Moment 5). But the *other* way the payment gate can fail — the provider never answering at all — has no handling yet: there is no `OrderPaymentTimeout` ticking, so an order that hears nothing back has no deadline that cancels it. That scheduling — and the `OrdersAwaitingPayment*` projection that drives it — arrives with slice 4.7, and will reuse Moment 5's same `ReleaseStock` to hand the stock back. After Moment 5, **payment timeout is the only order-cancellation path left to build.**
- **No committing of the reserved stock.** When an order *confirms* (Moment 4), the stock stays *reserved* — it is never turned into a settled "committed/shipped" decrement, because CritterMart models no fulfilment. Releasing on cancellation (Moment 5) is the only thing that ever moves reserved stock back; there is no opposite "commit" step, by design.
- **No abandonment yet.** If the Customer walks away from a cart *before* checking out, nothing happens to it in these slices; the inactivity timeout and `CartAbandoned` arrive with slice 3.4.

## Forthcoming Moments

The Customer's purchasing journey will gain Moments as the Orders slices are authored:

- **Editing the cart** — removing an item (slice 3.2) and changing a quantity (slice 3.3).
- **The last cancellation of order processing** (slice 4.7) — payment *timeout*: an order that never hears back from the provider is cancelled on a deadline, reusing Moment 5's `ReleaseStock` to hand the stock back. (Stock reservation + stock-failure cancel are Moment 3; payment authorization + confirmation are Moment 4; payment-decline cancel + release are Moment 5.)
- **Abandonment** (slice 3.4) — a cart times out if left inactive *before* checkout and is marked `CartAbandoned`.

When those land, this narrative's `slices` frontmatter grows, the version bumps, and `## Document History` records the amendment.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                            |
| ------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-28 | Initial commit. Covers Workshop 001 slice 3.1 (Add item to cart) as Moment 1: the Customer adding their first item, the frontend snapshotting product fields into `AddToCart`, the new Cart stream (`CartCreated` + `CartItemAdded`), and `CartView`. Continues the Customer's journey from Narrative 002 (browse). Cart edits (3.2/3.3), place-order (4.1), fulfillment (4.2–4.7), and abandonment (3.4) noted as forthcoming. The cart-inactivity timeout (Bruun) is deferred to slice 3.4. |
| v1.1    | 2026-05-30 | Adds **Moment 2 — Placing the order** for Workshop 001 slice 4.1 (`PlaceOrder`): checkout as the project's first multi-stream atomic write (`OrderPlaced` on a new Order stream + `CartCheckedOut` on the cart, one transaction), the frozen line snapshot + computed total, the `awaiting_confirmation` status, and the closed cart freeing a fresh one. `slices` frontmatter → `[3.1, 4.1]`. Reframes "fulfillment" as "order processing" throughout and clarifies the order heads toward *confirmed*, not delivered (no logistics — vision.md non-goal). Notes that placement does not yet reserve stock (4.2) and that no `OrderPaymentTimeout` is scheduled (deferred to 4.7). Realized in `openspec/changes/slice-4-1-place-order/` (capabilities `order-lifecycle` + `shopping-cart`). |
| v1.2    | 2026-05-31 | Adds **Moment 3 — Reserving the stock** for Workshop 001 slices 4.2 (reserve stock cross-BC) **and** 4.5 (cancel on stock failure), bundled in one slice. The journey's first cross-bounded-context hop: Orders cascades `ReserveStock` to Inventory over RabbitMQ (ADR 003), Inventory reserves on the Stock stream (or refuses), and the answer returns as a **Klefter local commit** — `StockReserved` (→ status `stock_reserved`) or `StockReservationFailed` followed by `OrderCancelled { reason: stock_unavailable }` (→ status `cancelled`). Explains the two-copies-of-`StockReserved` Klefter pattern and the both-sides idempotency that makes at-least-once delivery safe; notes the stock-failure cancel crosses no boundary back (nothing was reserved). `slices` frontmatter → `[3.1, 4.1, 4.2, 4.5]`. Updates "does not yet see" (stock now reserved at place time; only stock-failure cancellation exists) and "Forthcoming" (4.3/4.4/4.6/4.7 remain). Realized in `openspec/changes/slice-4-2-reserve-stock/` (capabilities `order-lifecycle` + `stock-management`). |
| v1.3    | 2026-05-31 | Adds **Moment 4 — Authorizing payment and confirming the order** for Workshop 001 slices 4.3 (authorize payment, stubbed) **and** 4.4 (confirm when both gates close), bundled in one slice. The **second gate**, and the journey's payoff: with stock reserved, Orders cascades an **in-process** `AuthorizePayment { orderId, amount }` (amount = order total) to a stubbed `IPaymentProvider`, records the decision as a Klefter `PaymentAuthorized` (→ transient status `payment_authorized`) or `PaymentAuthFailed`, and — on approval, both gates closed — the aggregate confirms itself with `OrderConfirmed` (→ terminal status `confirmed`). Contrasts the in-process payment hop with Moment 3's cross-BC hop (the trace stays inside Orders); explains confirmation as the aggregate's own decision on stream state (PMvH, ADR 007) and the stream-state idempotency guard. Decline is recorded but left non-terminal (cancellation + stock release deferred to 4.6). `slices` frontmatter → `[3.1, 4.1, 4.2, 4.5, 4.3, 4.4]`. Updates "does not yet see" (declined-order cancel 4.6 + payment timeout 4.7 remain) and "Forthcoming" (4.6–4.7). Realized in `openspec/changes/slice-4-3-authorize-payment/` (capability `order-lifecycle`). |
| v1.4    | 2026-05-31 | Adds **Moment 5 — Cancelling a declined order and handing the stock back** for Workshop 001 slice 4.6 (cancel on payment decline) **and** Inventory slice 2.3 (release reserved stock), bundled in one cross-BC slice. The journey's **first cancellation that crosses a boundary *back***: in the same commit that records `PaymentAuthFailed`, the Order aggregate appends `OrderCancelled { reason: "payment_declined" }` (→ terminal status `cancelled`) **and** publishes a `ReleaseStock { orderId, lines }` message to Inventory over RabbitMQ; Inventory appends `StockReleased` per held SKU (available rises, reserved falls, order dropped from reservations), idempotent per-SKU on reservation presence. Frames it as the mirror of Moment 3's stock-failure cancel — but unlike that one, stock *was* reserved, so this path must release it. Explains the `ReleaseStock`-command-not-`OrderCancelled`-event choice as published-language / anti-corruption (ADR 014; a deliberate divergence from the Workshop's § 2.3/§ 4.6 wording, recorded in the change's `design.md`), and the both-sides idempotency. `slices` frontmatter → `[3.1, 4.1, 4.2, 4.5, 4.3, 4.4, 4.6, 2.3]`. Updates "does not yet see" (payment timeout 4.7 is now the only remaining cancellation; reserved stock is never committed) and "Forthcoming" (4.7 + cart edits + abandonment). Realized in `openspec/changes/slice-4-6-cancel-on-payment-decline/` (capabilities `order-lifecycle` + `stock-management`). |
