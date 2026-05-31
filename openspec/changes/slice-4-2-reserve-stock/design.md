## Context

Slice 4.2 is the cross-bounded-context centerpiece: Orders asks Inventory to reserve a placed order's stock over RabbitMQ, and the outcome returns as a Klefter local commit on the Order stream. It is the OTel distributed-trace demo (ADR 005), the project's first message handlers, first live broker traffic, and first shared published-language contract. It runs as **one consolidated PR** and **bundles slice 4.5** (cancel on stock failure) so the failure path is end-to-end.

Two modeling questions the Workshop under-specifies were resolved with the user this session (Decisions 2 and 4 below): how a *multi-line* order reserves (the Workshop's `ReserveStock` is single-SKU, but slice 4.1 creates multi-line orders), and where the cross-BC contract types live.

## Goals / Non-Goals

**Goals:**
- `OrderPlaced` → cascade one whole-order `ReserveStock` to Inventory over RabbitMQ.
- Inventory `Handle(ReserveStock)`: reserve every line all-or-nothing across the SKU `Stock` streams in one transaction; cascade `StockReserved`/`StockReservationFailed` back.
- Orders inbound handlers append the Klefter `StockReserved` (status `stock_reserved`) or `StockReservationFailed` + `OrderCancelled(stock_unavailable)` (status `cancelled`, slice 4.5).
- At-least-once idempotency on both sides via stream-state guards.
- Tracked-session tests both sides + one RabbitMQ-Testcontainer smoke.

**Non-Goals:** stock release on cancellation (2.3); payment (4.3); confirm (4.4); cancel on payment decline/timeout (4.6/4.7); `OrderPaymentTimeout` + `OrdersAwaitingPayment*` (4.7). No async daemon / Marten event subscriptions (ADR 008). The 4.5 cancel publishes no cross-BC `OrderCancelled` (nothing reserved).

## Decisions

### 1. Scope bundles slices 4.2 + 4.5 in one PR

A user-sanctioned deviation from one-slice-one-PR (resolved at this session's scope fork). Pure 4.2 would leave a `stock_reservation_failed` order sitting in limbo until 4.5; bundling the aggregate decision `StockReservationFailed → OrderCancelled(stock_unavailable)` makes the failure path demoable whole. The bundling is clean because of Decision 2 — a refusal reserves nothing, so the cancel has nothing to release and pulls in no 2.3 dependency and no cross-BC traffic.

### 2. Whole-order atomic reservation — one message, all-or-nothing across SKU streams

The Workshop's `ReserveStock { orderId, sku, quantity }` is single-SKU and every § 6.1 GWT uses a one-line order, but slice 4.1 already creates multi-line orders (Narrative Moment 2 has two lines), and the Order-stream Klefter `StockReserved` is order-level (`{ orderId }`, no SKU). Resolved with the user: Orders sends **one** `ReserveStock { orderId, lines: [{ sku, quantity }, …] }` carrying the whole order; Inventory reserves **all** lines in a single transaction (every SKU's `Stock` stream gets a `StockReserved`, or none does) and replies **once**. Chosen over per-line fan-out (which drags in scatter-gather fan-in and partial-failure compensation that would need slice 2.3) and over single-line-only (which contradicts 4.1's multi-line orders). Consequences: the *request* message shape refines the Workshop's single-SKU illustration to carry lines (documented divergence); Inventory gains a cross-stream atomic write (multiple `FetchForWriting<StockLevelView>` on one session, committed by `AutoApplyTransactions` — the same single-transaction mechanism slice 4.1 used across two streams); the OTel trace is one clean round-trip per order; and a refusal leaves nothing reserved, so 4.5's cancel is self-contained.

### 3. Trigger is cascading messages from handlers — not bespoke PMvH machinery, not subscriptions

The send and every hop use Wolverine's **cascading messages** feature: the `PlaceOrder` endpoint returns `(IResult, ReserveStock)`; Inventory's `Handle(ReserveStock)` returns the outcome message; Orders' inbound handlers return the events to append. Cascading messages is a first-class, heavily-tested Wolverine feature; PMvH (ADR 007) remains the Order aggregate's *lifecycle frame* (its stream is its own process state), not per-hop machinery. **Marten event subscriptions were ruled out** — they require Marten's async daemon, which ADR 008 / structural-constraints forbid for round one. (User preference recorded for future slices: prefer cascading messages over PMvH unless PMvH genuinely wins.)

### 4. Cross-BC contracts live in a new shared `CritterMart.Contracts` project (published language)

Resolved with the user. The cross-BC message records (`ReserveStock` + `ReserveStockLine`, `StockReserved`, `StockReservationFailed`) live in a new `CritterMart.Contracts` project that both Orders and Inventory `ProjectReference`. This is the explicit **published language** of the Orders↔Inventory Customer-Supplier relationship (context map), and a single source of truth for the wire shapes. It does **not** breach "services do not reference each other's projects" — `Contracts` is not a service, and both services already reference the shared `ServiceDefaults` project, so a shared non-service project is an established pattern. Chosen over per-service duplicated records with aligned `[MessageIdentity]` (more decoupled but duplicated definitions to keep in lockstep) and over folding contracts into `ServiceDefaults` (which would conflate domain contracts with cross-cutting infra). **Trade-off:** a shared-kernel coupling on the contract types. **Follow-up flagged in the retro:** an ADR recording the published-language-via-shared-project decision, paired with a structural-constraints.md note — deliberately not authored here to keep this PR within the frozen prompt's named deliverables.

### 5. Three `StockReserved`s, each owned by its context

The same conceptual fact is persisted three times, each owned by its context: `Inventory.Stock.StockReserved(Sku, OrderId, Quantity)` (per-SKU Stock-stream event, unchanged from 2.2), `Contracts.StockReserved(OrderId)` (the order-level wire message), and `Orders.Order.StockReserved(OrderId)` (the Order-stream Klefter event, Workshop § 4). The shared `Contracts` project owns **only** the wire message; neither stream's events leak into it. The inbound handler maps the message to the stream event. Same name across three namespaces is intentional (the Workshop calls them the same fact) and kept un-aliased.

### 6. RabbitMQ conventional routing

Both services call `opts.UseRabbitMq(...).AutoProvision()`; Wolverine derives the exchanges/queues/bindings from the message types. Lowest ceremony; the cross-BC hop still surfaces in the OTel trace (the visual the talk needs). Explicit topology declined for round one. The AppHost wires `.WithReference(rabbitmq)` + `.WaitFor(rabbitmq)` onto `orders` and `inventory`, and the stale "first cross-BC message flows in slice 2.2" comment on the RabbitMQ resource is corrected to 4.2.

### 7. The interim HTTP reserve endpoint is retired; the message handler is the sole entry

Slice 2.2 shipped `POST /stock/{sku}/reservations` explicitly as an interim trigger (its code comment says so) using the single-SKU shape. With the cross-BC message now the real and only trigger, and the shape changed to whole-order, the HTTP endpoint is retired and its reserve logic moves into `Handle(ReserveStock)`. The durable `stock-management` spec already phrases reservation transport-agnostically ("is received"), so retiring the route does not contradict it. The 2.2 HTTP integration tests are replaced by message-handler tracked-session tests.

### 8. Idempotency via stream-state guards, not Wolverine inbox dedup

Consistent with ADR 007's stance for the Order stream: both sides decide idempotency by reading their own stream state. Inventory: a `Stock` stream that already records a `StockReserved` for the order is not reserved again. Orders: an Order stream that is terminal or already records the gate event ignores the duplicate. This keeps the guards visible in the domain code (a teaching point) rather than hidden in transport configuration.

## Risks / Trade-offs

- **Shared-kernel coupling on `CritterMart.Contracts`** (Decision 4) — accepted; the alternative (duplication) trades coupling for lockstep-maintenance burden, and a single source of truth is clearer for a teaching reference.
- **Cross-stream atomic reserve on Inventory** (Decision 2) — multiple `FetchForWriting` on one session; relies on `AutoApplyTransactions` committing all appends in one transaction (same mechanism as slice 4.1's two-stream write). A genuinely concurrent reservation of the same SKU by two orders is out of the round-one demo's path; optimistic concurrency on each stream covers the sequential case.
- **`ReserveStock` shape diverges from the Workshop's single-SKU illustration** — documented in Decision 2; the Workshop § 6.1 wording is single-line by example, not by intent, and is amended/noted as part of this slice's faithfulness record.
- **Idempotency guards add a stream read per inbound message** — acceptable; the reads are inline and cheap, and the guard is the correctness contract under at-least-once delivery.

## Open Questions

- **Symmetric cross-BC `OrderCancelled` on a stock-failure cancel** (Workshop § 8 open question 2): decided **no** for this slice — the all-or-nothing reservation means a refusal reserved nothing, so there is nothing for Inventory to release. Revisit only if a future model allows partial reservation.
- **ADR for the published-language contracts project** (Decision 4): flagged for a follow-up `tidy:` PR (ADR + paired structural-constraints.md note), kept out of this feat PR per the frozen prompt's scope.
