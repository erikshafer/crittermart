---
narrative: 004
title: The Customer Buys From the Catalog
actor: Customer
status: draft
version: v1.2
slices: [3.1, 4.1, 4.2, 4.5]
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
- **Slice 4.2 — Reserve stock cross-BC**, and **slice 4.5 — cancel on stock failure.** Authored in this version. Moment 3 below covers the first thing that happens to a placed order: Orders asks Inventory to set the goods aside, over the message broker, and the order either advances to *stock reserved* or — if the goods aren't there — is cancelled.

Forthcoming (not authored here): editing the cart — remove (3.2), change quantity (3.3); the rest of the order's **processing** — payment (4.3), confirmation (4.4), and the other cancellations (payment decline 4.6, timeout 4.7); and **cart abandonment** (3.4) if the cart is left inactive. Each extends this narrative when its slice is authored.

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

## What the Customer does *not* yet see

- **No re-pricing of the cart.** If the Seller changes `crit-001`'s price (slice 1.3) after it is in the cart, the cart still shows the snapshot `$24.99`. The snapshot wins until checkout — by design.
- **No stock check at *add* time.** Adding an item to the cart still neither verifies nor reserves stock — that only happens once the order is placed (Moment 3). So a Customer can fill a cart with more than exists; the shortfall surfaces at reservation, as a cancelled order, not at add-to-cart.
- **No payment or confirmation yet.** A reserved order stops at `stock_reserved`; it has cleared the stock gate but not the payment one. Authorizing the (stubbed) payment (4.3) and confirming once both gates close (4.4) come later, as do the *other* ways an order can be cancelled — payment decline (4.6) and payment timeout (4.7). Only cancellation on **stock** failure exists so far (slice 4.5, Moment 3). There is no `OrderPaymentTimeout` ticking yet — that scheduling arrives with slice 4.7.
- **No abandonment yet.** If the Customer walks away from a cart *before* checking out, nothing happens to it in these slices; the inactivity timeout and `CartAbandoned` arrive with slice 3.4.

## Forthcoming Moments

The Customer's purchasing journey will gain Moments as the Orders slices are authored:

- **Editing the cart** — removing an item (slice 3.2) and changing a quantity (slice 3.3).
- **The rest of order processing** (slices 4.3–4.7) — picking up where Moment 3 leaves a `stock_reserved` order: payment authorized (stubbed, 4.3), the order confirmed once both gates close (4.4), or cancelled on payment decline (4.6) or timeout (4.7). (Stock reservation and cancellation on stock failure are now authored — Moment 3.)
- **Abandonment** (slice 3.4) — a cart times out if left inactive *before* checkout and is marked `CartAbandoned`.

When those land, this narrative's `slices` frontmatter grows, the version bumps, and `## Document History` records the amendment.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                            |
| ------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-05-28 | Initial commit. Covers Workshop 001 slice 3.1 (Add item to cart) as Moment 1: the Customer adding their first item, the frontend snapshotting product fields into `AddToCart`, the new Cart stream (`CartCreated` + `CartItemAdded`), and `CartView`. Continues the Customer's journey from Narrative 002 (browse). Cart edits (3.2/3.3), place-order (4.1), fulfillment (4.2–4.7), and abandonment (3.4) noted as forthcoming. The cart-inactivity timeout (Bruun) is deferred to slice 3.4. |
| v1.1    | 2026-05-30 | Adds **Moment 2 — Placing the order** for Workshop 001 slice 4.1 (`PlaceOrder`): checkout as the project's first multi-stream atomic write (`OrderPlaced` on a new Order stream + `CartCheckedOut` on the cart, one transaction), the frozen line snapshot + computed total, the `awaiting_confirmation` status, and the closed cart freeing a fresh one. `slices` frontmatter → `[3.1, 4.1]`. Reframes "fulfillment" as "order processing" throughout and clarifies the order heads toward *confirmed*, not delivered (no logistics — vision.md non-goal). Notes that placement does not yet reserve stock (4.2) and that no `OrderPaymentTimeout` is scheduled (deferred to 4.7). Realized in `openspec/changes/slice-4-1-place-order/` (capabilities `order-lifecycle` + `shopping-cart`). |
| v1.2    | 2026-05-31 | Adds **Moment 3 — Reserving the stock** for Workshop 001 slices 4.2 (reserve stock cross-BC) **and** 4.5 (cancel on stock failure), bundled in one slice. The journey's first cross-bounded-context hop: Orders cascades `ReserveStock` to Inventory over RabbitMQ (ADR 003), Inventory reserves on the Stock stream (or refuses), and the answer returns as a **Klefter local commit** — `StockReserved` (→ status `stock_reserved`) or `StockReservationFailed` followed by `OrderCancelled { reason: stock_unavailable }` (→ status `cancelled`). Explains the two-copies-of-`StockReserved` Klefter pattern and the both-sides idempotency that makes at-least-once delivery safe; notes the stock-failure cancel crosses no boundary back (nothing was reserved). `slices` frontmatter → `[3.1, 4.1, 4.2, 4.5]`. Updates "does not yet see" (stock now reserved at place time; only stock-failure cancellation exists) and "Forthcoming" (4.3/4.4/4.6/4.7 remain). Realized in `openspec/changes/slice-4-2-reserve-stock/` (capabilities `order-lifecycle` + `stock-management`). |
