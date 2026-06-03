# Proposal: Slice 3.4 — Cart Abandonment (inactivity timeout + async projection teaser)

## Why

A cart the Customer walks away from currently stays open forever — the Cart stream has only one terminal event (`CartCheckedOut`), so an inactive cart never resolves and its `CartView` row sits open indefinitely. Workshop 001 slice 3.4 models the missing ending: a configured inactivity window after which the cart is **abandoned** (the Bruun temporal automation, mirroring slice 4.7's payment timeout on the order side). This is the last Orders BC slice of round one, and it carries the round-one **async projection teaser** required by ADR 008.

## What Changes

- Creating a cart (`AddToCart`, first add) also schedules a `CartActivityTimeout` self-message for the configured inactivity window (`Orders:CartActivityTimeout`, default 2 hours).
- A new `CartAbandonmentHandler` processes the fired timeout with **fire-and-check** semantics (Workshop § 8 open question 1, resolved): a closed cart is a silent no-op; a cart with activity newer than the window reschedules the timeout from that activity; an inactive cart appends `CartAbandoned { reason: "inactivity_timeout", lines, totalValue }` — the Cart stream's second terminal event — and closes.
- Cart edits (`RemoveCartItem`, `ChangeCartItemQuantity`, subsequent `AddToCart`) schedule **nothing** — their event timestamps are the activity record the firing handler checks. (This dissolves the 3.2/3.3 slice-table "refresh `CartActivityTimeout`" clauses; they never become code.)
- A new inline `CartsAwaitingActivity` projection (one row per open cart with its visible deadline, conditional-deleted on either terminal event), readable at `GET /carts/awaiting-activity`.
- A new **async** `CartAbandonmentReport` projection — a daily rollup (`abandonedCartCount`, `totalValueAbandoned`, `abandonedSkus`) grouped by abandonment date. Registered with `ProjectionLifecycle.Async`, **no daemon** (ADR 008); populated by rebuild-on-demand.
- `CartView` gains `LastActivityAt` (folded from event timestamps) and an `Apply(CartAbandoned)` that closes the cart.

## Capabilities

### New Capabilities

*(none — all deltas extend the existing `shopping-cart` capability, per the one-capability-per-aggregate shape)*

### Modified Capabilities

- `shopping-cart`: 1 MODIFIED requirement (*Add an item to the cart* — creating a cart now also schedules the inactivity timeout) + 3 ADDED requirements (*Abandon the cart on inactivity*, *Track carts awaiting activity*, *Report on cart abandonment*).

## Impact

- **Code**: `src/CritterMart.Orders/Cart/` (new: `CartAbandoned`, `CartActivityTimeout` + `CartActivityDeadline`, `CartAbandonmentHandler`, `CartsAwaitingActivity`, `CartAbandonmentReport`; modified: `CartView`), `Features/AddToCart.cs` (scheduled cascade + new GET endpoint), `Program.cs` (config + singleton + two projection registrations).
- **Tests**: `tests/CritterMart.Orders.Tests/` — new pure-fold suites for both projections, a new integration suite for the abandonment paths and the rebuild, amendments to `CartViewProjectionTests` (IEvent wrappers) and `AddToCartTests` (scheduled-message assertion).
- **No cross-BC impact**: cart abandonment stays inside Orders (no stock was reserved by a cart, so nothing is released). No contract changes, no broker topology changes, no new packages.
- **Out of band (post-merge tidy)**: Workshop 001 § 8 open question 1 resolution + a factual correction (Wolverine has no scheduled-message cancellation API); workshop § 6.1 slice 3.4 amendment; `openspec archive`.
