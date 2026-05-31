# Retrospective: Implementations 007 — Slice 4.1 Place Order (Order aggregate)

**Prompt**: `docs/prompts/implementations/007-slice-4-1-place-order.md`
**Outcome**: shipped — slice 4.1 (`PlaceOrder`) implemented, OpenSpec change `slice-4-1-place-order` authored + `--strict` valid, Narrative 004 → v1.1 (Moment 2). One consolidated PR (`feat: slice 4.1 place order (Order aggregate)`).
**Tests**: Orders suite green — **5 unit** (pure folds: 3 cart + cart-checkout + order-placed) + **7 integration** (3 existing AddToCart + 4 new PlaceOrder), against Testcontainers Postgres.

## What shipped

- **Order aggregate** (`src/CritterMart.Orders/Order/`): `OrderPlaced` + `OrderLine`; `OrderStatusView` (Id/CustomerId/Status/Lines/Total) + `OrderStatus.AwaitingConfirmation` + inline `OrderStatusViewProjection` (pure fold). Registered inline in `Program.cs` (ADR 008).
- **Cart checkout** (`shopping-cart` delta): `CartCheckedOut` event + `Apply(CartCheckedOut)` on `CartViewProjection` (flip `IsOpen`, retain lines). First real exercise of slice 3.1's partial-unique index.
- **`PlaceOrder` feature**: `POST /orders { customerId }` resolves the open cart → `StartStream<OrderStatusView>(OrderPlaced)` + `FetchForWriting<CartView>(...).AppendOne(CartCheckedOut)` in one transaction; `409` on no-open-cart / empty-cart. `GET /orders/{orderId}`.
- **OpenSpec change** with two capability deltas (`order-lifecycle` ADDED, `shopping-cart` ADDED), `design.md`, `tasks.md`; `--strict` valid.
- **Narrative 004 → v1.1** with Moment 2.

## What worked

- **The slice-3.1 index paid off exactly as designed.** The partial-unique `CartView` index (`Predicate "(data ->> 'IsOpen')::boolean = true"`) was written in 3.1 anticipating this slice. 4.1 added one `Apply(CartCheckedOut)` line and the "one open cart per customer, recreatable after checkout" invariant fell out with no migration. Designing the guard a slice early was the right call.
- **The multi-stream write was undramatic.** `StartStream` + `FetchForWriting` on one `IDocumentSession`, committed by `AutoApplyTransactions` — no special API, no distributed-transaction ceremony. The happy-path test asserting *both* the new `OrderStatusView` and the flipped `CartView.IsOpen` after a single POST is the atomicity proof.
- **The "already checked out" failure cost zero extra code.** Resolving by *open* cart means a checked-out cart is simply not found; the workshop's "rejected, no new Order stream" behavior is a property of the resolution, not a separate guard. Captured as design decision 4.
- **The naming fork was worth stopping for.** The user flagged "fulfillment" baggage; verifying against `vision.md:28` (no shipping) and the Order vocabulary (terminal = `OrderConfirmed`) confirmed `order-fulfillment` would over-promise. Landed on `order-lifecycle`. The narrative now explicitly says the order heads toward *confirmed*, not delivered.

## What was harder / notable

- **Splitting the spec delta across two capabilities** is the first time a CritterMart slice did so. It is the honest model (`CartCheckedOut` is a Cart-aggregate event), but it is a precedent: future cross-aggregate slices inside Orders (e.g., 4.5/4.6 cancelling an order *and* releasing stock cross-BC) will face the same "which capability owns this delta" question. Decision 3 records the rule: the event's owning aggregate owns the requirement, regardless of which command triggers it.
- **`OrderStatusView.Status` as a `string`** (not an enum) — pragmatic to keep snake_case workshop names in JSON without a converter and the fold trivially unit-testable. Flagged as a trade-off (design decision); revisit if the status set or transition rules grow complex in 4.2–4.7.

## Methodology refinements

- **The "defer the timeout machinery to the slice that consumes it" pattern is now used twice** (3.1 → 3.4 for `CartActivityTimeout`; 4.1 → 4.7 for `OrderPaymentTimeout`). This is a stable, repeatable scope-boundary heuristic for Bruun temporal-automation slices: the scheduling lives with the handler that fires on it, not with the slice that "logically" first mentions it. Candidate for encoding as a convention once a third instance appears.
- **No workshop amendment was needed this slice** — unlike 3.1, where the §6.1 wording diverged from the shipped `cartId` keying. The 4.1 GWT matched the implementation, and the slice table already lists 4.7 as the timeout slice, so the deferral is faithful to the model as written.

## Outstanding / next-session inputs

- **`openspec archive slice-4-1-place-order`** after merge → folds `order-lifecycle` into a durable main spec and appends the checkout requirement to `openspec/specs/shopping-cart/spec.md` (whose `## Purpose` is still the `TBD` archive placeholder — worth filling in the same `tidy:` step).
- **README / index refresh** (stale BC-status rows, capability count) — a separate `tidy: docs` concern, deliberately kept out of this feat PR (no opportunistic edits).
- **Design-return cadence**: this is the **1st implementation PR** against Orders since the #28 design-return. Two more Orders implementation slices are in budget before the next mandatory interleave.
- **Next slice**: **4.2 — reserve stock cross-BC**, the over-RabbitMQ centerpiece (`ReserveStock` Orders→Inventory, `StockReserved`/`StockReservationFailed` back, Klefter local commits, at-least-once duplicate guard). This is where RabbitMQ + CritterWatch (ADR 013) light up. Orders now has the Order stream 4.2 appends its Klefter events onto.

## Spec-delta — landed?

**Yes.** `order-lifecycle` (ADDED: place an order) and `shopping-cart` (ADDED: check out the cart) both authored and `--strict` valid; satisfied by code (12 green tests). Narrative 004 records Moment 2 in its Document History (v1.1). Workshop § 6.1 slice 4.1 satisfied as written, timeout machinery deferred to 4.7 per the slice table.
