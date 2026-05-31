## Context

Slice 3.1 opens the **Orders** bounded context — CritterMart's process-rich, event-sourced core — with its first aggregate, the **Cart**. It is also the bootstrap PR for the third service (`CritterMart.Orders`, the second event-sourced one), so it follows the "blueprint architecture" exception (skeleton + first slice in one PR, as slice 2.1 did for Inventory). It inherits the event-sourcing + inline-projection approach (ADR 008), `StreamIdentity.AsString` (ADR 002), and Wolverine.Http (ADR 006) from the Inventory precedent.

Unlike Inventory's `Stock` stream — keyed by the SKU that arrives on every command — the `Cart` is keyed by a generated `cartId` the caller does **not** know on the first add. `AddToCart` carries only `customerId`. That gap (resolve the customer's open cart before appending) is the central design problem this slice solves, and the proposal explicitly deferred the mechanism to this artifact.

## Goals / Non-Goals

**Goals:**
- Stand up `CritterMart.Orders` (Marten event store, `orders` schema, `:5103`, wired into Aspire + the solution).
- `AddToCart` create-or-append: first add → new `Cart` stream (`CartCreated` + `CartItemAdded`); subsequent add → `CartItemAdded` on the same cart.
- Resolve the customer's open cart from `customerId` without making `customerId` the stream key.
- Inline `CartView` snapshot projecting the cart's lines from a pure fold.
- The project's **first pure-function unit tests** (the line fold), which activate the green-but-empty CI unit job (PR #19), plus Alba + Testcontainers integration tests for the two GWT scenarios.

**Non-Goals:** remove item (3.2), change quantity (3.3), checkout (4.1); no stock check/reservation at add time (4.2); no `CartActivityTimeout`/`CartAbandoned` Bruun temporal automation (deferred to 3.4 — this slice schedules nothing); no RabbitMQ (Orders is `WithReference(postgres)` only this slice); no Catalog read (name + price arrive snapshotted on the command).

## Decisions

### 1. Stream key is `cartId`, not `customerId` (confirmed against ai-skills + CritterStackSamples)

A new stream per cart lifecycle, keyed by a generated `cartId`. A cart goes terminal at checkout (4.1) or abandonment (3.4), so the customer's next purchase is a fresh stream — consistent with how the Order aggregate keys by `orderId`, and faithful to event-sourcing's "streams go terminal, they are not deleted."

**Alternatives considered.** Keying the `Cart` stream by `customerId` would make resolution the trivial `FetchForWriting<CartView>(customerId)` happy path (matching the upstream `marten-aggregate-handler-workflow` sweet spot and the customer-keyed basket in `JasperFx/CritterStackSamples` `EcommerceMicroservices/Basket`). Rejected because: (a) the sample's basket is a **document** that is **deleted** on checkout — deletion is what makes "one open basket per customer" free, and an event stream cannot be deleted cleanly; (b) a customer-keyed stream is perpetual, forcing checkout/abandon to be modeled as *state* on a never-ending stream and a new cart to *reuse* the same stream id; (c) it would diverge from Order's `orderId`. The cost of keeping `cartId` is one resolution query before the append (Decision 2), which the skills sanction.

### 2. Resolve the open cart by querying `CartView` on `customerId` (skills-sanctioned)

The `marten-advanced-indexes-and-query-optimization` skill is explicit that querying a document by a non-key field is done with a computed `Index`, and a uniqueness rule with a partial `UniqueIndex`. So:

- `CartView` carries `CustomerId`; the projection is registered with `.Index(x => x.CustomerId)` and a partial `UniqueIndex` (`IsUnique = true`, predicate scoping it to *open* carts) to enforce **one open cart per customer** at the database.
- The handler queries `CartView` for the customer's open cart. None found → `MartenOps.StartStream<CartView>(newCartId, CartCreated, CartItemAdded)`. Found → `FetchForWriting<CartView>(open.Id)` + `AppendOne(CartItemAdded)`.

**Open-cart semantics this slice.** With no checkout/abandon yet, every cart that exists is open, so the partial-index predicate is trivially "all carts" today; it is written as a predicate now so the invariant survives unchanged when 4.1/3.4 introduce terminal states. The resolution query is `FirstOrDefaultAsync` against the indexed `CustomerId`.

**Alternative considered.** A hand-maintained `customerId → cartId` lookup document. Rejected: it does by hand what a computed index gives for free, adds a second document to keep in sync, and would need manual flipping on checkout/abandon. The indexed-projection approach keeps a single read model that does double duty (resolution + the readable cart).

### 3. The line fold is a pure function (first unit tests)

The `CartView` projection's `Apply(CartItemAdded)` is a pure fold over the stream — no DB, no mocks. It is unit-tested directly (`new` a view, apply events, assert lines), and these untagged tests run in the CI `Category!=Integration` job that has selected zero tests since PR #19. Each `CartItemAdded` appends a line carrying SKU, quantity, and the snapshotted name + price (no line-merging by SKU in this slice — quantity-merge is a 3.3 concern).

### 4. HTTP surface — write by customer, read by cartId

- `POST /carts/{customerId}/items` (body: `sku`, `quantity`, `productSnapshot { name, price }`) → `201 Created`, `Location: /carts/{cartId}`, body `{ cartId }`. Mirrors Inventory's sub-resource POST shape (`/stock/{sku}/receipts`); the customer is the path parent because the caller knows only `customerId` on the first add, and the response hands back the resolved/created `cartId`.
- `GET /carts/{cartId}` → `200` `CartView` (`LoadAsync`, `404` if none). The cart is the resource, keyed by its stream id.

**Alternative considered.** Reading by `customerId` (`GET /carts/{customerId}` reusing the resolution) — natural "show my cart" semantics, but it makes the read depend on the open-cart resolution and is less resource-faithful once a customer can have multiple historical (terminal) carts. Deferred; read-by-`cartId` is the cleaner resource model.

## Risks / Trade-offs

- **Resolution query runs off the id-on-command happy path** → accepted and intentional (the realistic event-sourcing pattern this slice teaches); the `CustomerId` index keeps it a single indexed lookup.
- **Partial unique index protects an invariant that is trivial today** (no terminal state yet) → written now so 4.1/3.4 inherit the guard without a migration; events replay into the same projection.
- **Query-then-write is not transactionally atomic with the append** → acceptable for a single customer acting sequentially in round one; the partial unique index is the backstop against a concurrent double-create.
- **`POST` returns `201` with a body, diverging from Inventory's `204` void POST** → intentional: the caller needs the generated `cartId` back to read the cart.

## Open Questions

- None blocking. Whether to additionally expose read-by-customer is a 3.2+/frontend concern, not needed for the two 3.1 scenarios.
