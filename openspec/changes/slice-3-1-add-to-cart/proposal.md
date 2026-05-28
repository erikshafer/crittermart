## Why

The Orders bounded context is CritterMart's process-rich, **event-sourced** core, and it holds two aggregates — Cart and Order (Workshop 001 § 2). This change opens that context with its first aggregate: the **Cart**. Workshop 001 slice 3.1 (`AddToCart`) is the genesis cart operation — the customer's first action toward a purchase — and Narrative 004 (the Customer's purchasing journey, Moment 1) is this proposal's human-readable sibling. The Cart is the customer's working selection, shaped item by item until checkout (slice 4.1) or lapse (slice 3.4).

## What Changes

- Introduce the `AddToCart` command: the Customer adds an item (SKU + quantity), carrying a **product snapshot** (name + price) the frontend composed from the Catalog at render time.
- On the customer's **first** add (no open cart), create a new `Cart` stream keyed by a generated **`cartId`**, appending `CartCreated { cartId, customerId }` then `CartItemAdded { sku, quantity, snapshot }`.
- On a **subsequent** add while the customer has an open cart, append a further `CartItemAdded` to that **same** cart.
- An **inline** `CartView` snapshot projects the cart's line items (SKU, quantity, snapshotted name + price) from the stream (per ADR 008).
- Expose the cart via a read-only HTTP surface.
- The price in the cart is the price **snapshotted at add time** and is authoritative through checkout; no re-pricing, no Catalog↔Orders integration (context map: presentation-layer composition, not a BC integration).
- **Out of scope (named deferrals):** remove item (3.2), change quantity (3.3), checkout (4.1); **no stock check or reservation** at add time (4.2); and the **cart-inactivity timeout** (`CartActivityTimeout` / `CartAbandoned`, the Bruun temporal-automation pattern) is **deferred to slice 3.4** — slice 3.1 schedules no timeout.

## Capabilities

### New Capabilities

- `shopping-cart`: managing the Customer's pre-checkout cart as an event-sourced `Cart` stream with an inline `CartView` read model. Slice 3.1 introduces **add item** (cart creation + line append); later slices add remove (3.2), change-quantity (3.3), and abandonment (3.4). This is the **first of the Orders BC's two capabilities** — the Order aggregate's capability is introduced separately by the slice 4.x proposals. (Orders is the first BC with two aggregates; the one-capability-per-BC rhythm of `product-catalog` and `stock-management` becomes one-capability-per-aggregate here.)

### Modified Capabilities

<!-- None. -->

## Impact

- **New service:** `CritterMart.Orders` (the third service, the second event-sourced one), Marten **event store** on the shared PostgreSQL under an `orders` schema (ADR 002), `StreamIdentity.AsString` with `Cart` streams keyed by a generated `cartId`.
- **Cart stream identity — `cartId`, not `customerId`.** A new stream per cart lifecycle: a cart goes terminal at checkout (4.1) or abandonment (3.4), so the customer's next purchase is a fresh stream. This keeps the Cart consistent with how the Order aggregate keys by `orderId` and avoids modelling a terminal stream that cannot be recreated. The handler must therefore **resolve the customer's *open* cart** (or start a new one) before appending; the resolution mechanism (a `customerId`-queryable `CartView` or a dedicated index) is a `design.md` concern, not specified here.
- **Faithfulness note — workshop divergence.** Workshop 001 § 6.1's slice 3.1 scenarios read literally as "a new Cart stream is created **for `customer-X`**," implying a `customerId`-keyed stream, and also schedule a `CartActivityTimeout`. This proposal refines both: the stream is keyed by `cartId` (per the decision above), and timeout scheduling is deferred with the rest of the Bruun pattern to slice 3.4. The workshop's intent (one open cart per customer, created on first add) is preserved; only the key and the timeout move. The workshop's literal wording is amended in a follow-up (recorded in this change's retrospective).
- **HTTP surface:** a `POST` to add an item (`AddToCart`; resolves or creates the customer's open cart) and a `GET` to read the `CartView`; concrete routes are firmed in `design.md`. No synchronous service-to-service calls; no RabbitMQ in this slice.
- **First inline projection in Orders:** `CartView` (`SnapshotLifecycle`/`ProjectionLifecycle.Inline` per ADR 008) — no async daemon.
- **CI / tests:** the implementation edge brings the project's **first pure-function unit tests** (cart line-folding logic), which activate the so-far-green-but-empty CI unit job (PR #19), alongside the Alba + Testcontainers integration tests.
- **Aspire:** the Orders service needs a `Properties/launchSettings.json` with a distinct `applicationUrl` on `:5103` (the `:5000`-collision lesson from the infra bundle).
- **Downstream artifacts:** `design.md` + `tasks.md` are authored in the **implementation edge** (a separate PR, per the per-edge cadence for slice 3.1), not in this proposal.
