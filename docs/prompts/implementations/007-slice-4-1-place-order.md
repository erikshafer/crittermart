# Prompt: Implementations 007 — Slice 4.1 Place Order (Order aggregate, one PR)

**Kind**: per-slice implementation, consolidated one PR (narrative bump + OpenSpec proposal + implementation), per the consolidate-slice-prs convention — the Orders skeleton already exists (slice 3.1), so no blueprint-architecture exception
**Files touched**: this prompt; `openspec/changes/slice-4-1-place-order/{proposal.md, specs/order-lifecycle/spec.md, specs/shopping-cart/spec.md, design.md, tasks.md}` (new); `docs/narratives/004-customer-purchase.md` (→ v1.1, Moment 2); `src/CritterMart.Orders/Order/**` (new aggregate); `src/CritterMart.Orders/Cart/{CartCheckedOut.cs, CartView.cs}`; `src/CritterMart.Orders/Features/PlaceOrder.cs`; `src/CritterMart.Orders/Program.cs`; `tests/CritterMart.Orders.Tests/{OrderStatusViewProjectionTests.cs, CartViewProjectionTests.cs, PlaceOrderTests.cs}`; `docs/retrospectives/implementations/007-slice-4-1-place-order.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; collaborative on genuine forks (present options + recommendation, user decides — memory `feedback-collaborate-on-decisions`, `feedback-options-with-previews`)
**Commit subject**: `feat: slice 4.1 place order (Order aggregate)`

## Framing

Slice 4.1 opens the **Order** aggregate — the second of the Orders BC's two event-sourced aggregates (Cart was first, slice 3.1). `PlaceOrder` is checkout: it turns the Customer's open cart into an order. Mechanically it is the project's **first multi-stream atomic write** (`OrderPlaced` on a new Order stream + `CartCheckedOut` on the existing Cart stream, one transaction), and it finally exercises the partial-unique `CartView` index that slice 3.1 wrote in anticipation of this slice flipping `IsOpen`.

## Goal

`POST /orders { customerId }` resolves the customer's open cart, freezes its lines + computed total onto a new Order stream (`OrderPlaced`), and checks the cart out (`CartCheckedOut`, `IsOpen` → false) in one transaction. An inline `OrderStatusView` projects status `awaiting_confirmation`, lines, and total; `GET /orders/{orderId}` reads it. Rejections: no open cart → `409` (also covers "already checked out"); empty cart → `409` (defensive). Proven by pure-fold unit tests + Alba/Testcontainers integration tests; `openspec validate --strict` passes.

## Spec delta

A new OpenSpec change `slice-4-1-place-order` with **two** capability deltas: `order-lifecycle` ADDED (place an order — Order stream side) and `shopping-cart` ADDED (check out the cart — Cart stream side). Narrative 004 gains Moment 2 (→ v1.1, `slices [3.1, 4.1]`). Workshop § 6.1 slice 4.1 is satisfied by code, with the timeout/Bruun machinery explicitly deferred to 4.7 (no workshop amendment needed — the slice table already lists 4.7 as the timeout slice).

## Locked decisions (collaborative forks, resolved with the user this session)

1. **Scope = checkout transaction only.** `OrderPaymentTimeout` scheduling + `OrdersAwaitingPayment*` projection deferred to slice 4.7 (the slice that consumes them) — mirrors 3.1 → 3.4 for `CartActivityTimeout`.
2. **Capability name = `order-lifecycle`** (not `order-fulfillment` — "fulfillment" carries shipping baggage the model excludes; the Order's terminal is `OrderConfirmed`, vision.md:28 non-goal).
3. **One consolidated PR** (narrative + proposal + impl + prompt/retro).

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** §§ 2, 3 (Place Order storyboard), 4 (Order vocabulary), 5 (slice 4.1 row), 6.1 (slice 4.1 GWT).
2. **`docs/narratives/004-customer-purchase.md`** — Moment 1 (the cart side this continues from).
3. **`openspec/specs/shopping-cart/spec.md`** — the shipped cart capability the checkout delta extends.
4. **`src/CritterMart.Orders/Features/AddToCart.cs` + `Cart/CartView.cs`** — the open-cart resolution query and the partial-unique index this slice reuses; mirror the feature/projection/test shape.
5. **Stack reality**: `Directory.Packages.props` (Wolverine 6.1 / Marten 9.2 / JasperFx 2.2 / .NET 10); `StreamIdentity.AsString`; raw `IDocumentSession` (not `[Aggregate]`) because neither stream id is on the command.

## Out of scope

- **No stock reservation (4.2), payment (4.3), confirm/cancel (4.4–4.7).**
- **No `OrderPaymentTimeout` scheduling, no `OrdersAwaitingPayment*` projection** — deferred to 4.7; this slice schedules nothing.
- **No RabbitMQ** — Orders stays Postgres-only this slice. **No Catalog read** — lines come from the cart's snapshot.
- **No README update** — stale-row refresh is a separate `tidy: docs` concern (no opportunistic edits).
- **No `openspec archive`** of this change (archive after merge).
