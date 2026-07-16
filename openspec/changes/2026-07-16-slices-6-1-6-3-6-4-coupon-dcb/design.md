# Design: slices-6-1-6-3-6-4-coupon-dcb (slices 6.1 / 6.3 / 6.4)

CritterMart's first **Dynamic Consistency Boundary** — a global per-coupon redemption cap enforced inside the
Orders store, spanning many order streams and aligning with no aggregate. See the proposal for the Why/What,
the `coupon-promotion` spec delta for the coupon SHALLs, and the `order-lifecycle` delta for the Order-stream
integration. This document records the implementation decisions, and in particular the one genuinely
non-obvious call — how a tagged redemption event composes with a brand-new order stream in one transaction
(Workshop 003 §8 item 1).

## Verified API surface (Marten 9.15.1, resolved transitively via WolverineFx 6.19.0)

Confirmed by a spike test against Marten 9.15.1 + real Postgres (see decision 2) — **not** the Polecat-flavored
symbols the installed `marten-advanced-dynamic-consistency-boundary` / `-cross-stream-operations` skills show
(`docs/skills/DEBT.md` row 3; those name `[BoundaryModel]`, `IEventBoundary<T>`, `EventTagQuery.For(…)` — do
not use them here). Namespaces: `JasperFx.Events.Tags` (`EventTagQuery`), `JasperFx.Events.Aggregation`
(`[BoundaryAggregate]`), `JasperFx.Events` (`Event.WithTag`, `IEventStoreOperations.{BuildEvent,FetchForWritingByTags}`),
`Marten.Exceptions` (`DcbConcurrencyException`):

- **`opts.Events.RegisterTagType<CouponId>("coupon").ForAggregate<CouponUsage>()` is the entire DCB config** —
  registering the strong-typed tag against its boundary aggregate is what triggers the tags schema on
  `orders.mt_events` (`ApplyAllDatabaseChangesOnStartup` creates it). There is **no** separate `EnableDcb()`
  call — the spike confirmed the registration alone suffices. `CouponId` is a `record CouponId(string Value)`
  (a wrapper record around a primitive; string is supported).
- `CouponUsage` is a `[BoundaryAggregate]` (identity-less) class with mutable `NetCount` + void `Apply(...)`.
- Per-event tag: `var evt = session.Events.BuildEvent(data); evt.WithTag(new CouponId(couponId));` then
  `session.Events.Append(orderId, evt)` to land it on the order stream.
- `new EventTagQuery().Or<CouponId>(new CouponId(couponId))` → `session.Events.FetchForWritingByTags<CouponUsage>(query)`,
  returning a boundary with `.Aggregate` (nullable) and `.LastSeenSequence`.
- `SaveChangesAsync()` throws `DcbConcurrencyException` when a matching tagged event lands inside the boundary
  between the read and the commit.
- ADR 024 verified 9.11.0; the resolved assembly has since moved to 9.15.1. DCB only gets more first-class in
  later 9.x, so this is a re-confirmation, not a re-decision. No Wolverine/Marten version bump.

## Decisions

1. **`CouponUsage` is the boundary aggregate; it is never persisted.** A plain immutable fold — net count =
   `CouponRedeemed (+1)` − `CouponRedemptionReleased (−1)` — materialized on demand by
   `FetchForWritingByTags<CouponUsage>` inside the write transaction and thrown away. It is registered as a
   **live** projection target only (no `Snapshot`/`Inline` document), distinct from the advisory persisted
   `CouponUsageView`. The two carry deliberately-similar names with the boundary-state-vs-view fence Workshop
   003 §§4/7 drew; keeping them separate types prevents a downstream reader from treating the queryable view
   as the cap authority.

2. **Append mechanics (Workshop 003 §8 item 1) — SPIKE-CONFIRMED, mechanic (a) LOCKED.** A throwaway
   integration spike (`DcbCouponSpike`, deleted after confirmation) ran against real Postgres and proved:
   `session.Events.StartStream<Order>(orderId, orderPlaced)` for the order genesis; then
   `var evt = session.Events.BuildEvent(new CouponRedeemed(...)); evt.WithTag(new CouponId(couponId));
   session.Events.Append(orderId, evt);` to land the **tagged** redemption on the **same** order stream; with
   `session.Events.FetchForWritingByTags<CouponUsage>(query)` read beforehand supplying the concurrency
   assertion the shared `SaveChangesAsync` enforces. The spike confirmed all three properties: the tagged
   event lands on the real order stream (not a tag-derived synthetic one — that is what `boundary.AppendOne`
   would do, so it is **not** used); a forced concurrent append makes the loser's `SaveChangesAsync` throw
   `DcbConcurrencyException`; and the net count holds at the cap. Mechanic (b) (`boundary.AppendOne`) is
   rejected — it auto-routes by tag, off the order stream, contradicting ADR 024's "real order streams" intent.

3. **The redemption path is the canonical Marten "reload and retry" loop with a fresh session per attempt
   (Workshop 003 §8 item 1).** DCB optimistic concurrency is **cap-blind**: `FetchForWritingByTags` arms an
   assertion on the coupon's whole tag-set, so *any* concurrent redemption invalidates a commit — even when
   both racers are safely under the cap. A bare pre-check therefore **under-admits** under a burst (verified by
   the concurrency test: 6 racers at cap 3 let only **1** through, not 3 — the cap never *exceeds*, but
   throughput collapses). Marten's own DCB guide answers this with reload-and-retry, and that is what the
   workshop's "the losing handler retries" language describes. Implemented in `RedeemWithDcbAsync`:
   - Each attempt opens a **fresh `LightweightSession`** (a session whose `SaveChangesAsync` threw is dirty and
     cannot be reused), re-reads `FetchForWritingByTags<CouponUsage>`, re-checks the cap (`NetCount >= cap` →
     `409 CouponExhausted`, no stream), appends the priced `OrderPlaced` + tagged `CouponRedeemed` +
     `CartCheckedOut`, and commits.
   - On `DcbConcurrencyException` it **`continue`s** — a loser still under the cap succeeds on the next read;
     only a genuinely-full cap yields `CouponExhausted`. Bounded at 25 attempts (converges in ~cap rounds; each
     round commits at least one).
   - Because this path controls its own commit it does **not** ride `AutoApplyTransactions` (which owns the
     injected session's post-handler commit and would re-throw the caught exception). Cascades therefore
     publish through `IMessageBus` **after** the commit — a post-commit send, acceptable because the order is
     durably placed and `ReserveStock` / the payment timeout are at-least-once safety nets. **The no-coupon
     path is untouched**: it keeps the injected session + tuple-return + transactional outbox (slice 4.1
     byte-for-byte).

   No ASP.NET exception mapping is needed — the loop catches every `DcbConcurrencyException` and returns a
   `409` explicitly, so none escapes. Exactly `cap` redemptions ever succeed, under sequential *or* concurrent
   load.

4. **`PlaceOrder` stays one endpoint; the coupon path is an internal branch.** No second command, no separate
   redemption endpoint — the coupon rides the existing `POST /orders` with an optional `couponCode` on the
   request body (the workshop's "one write point"). Absent `couponCode` → the current slice-4.1 code path,
   untouched, `discount = 0`, no boundary opened (verified by a no-coupon regression test asserting byte-for-byte
   parity). Present → the DCB branch. The cascaded `ReserveStock` + scheduled `OrderPaymentTimeout` outputs are
   unchanged; reservation and payment authorize the **discounted** `total`, with no knowledge of coupons.

5. **Pricing lands on `OrderPlaced` (Workshop 003 §3 "where the discount lands").** `OrderPlaced` gains
   `Subtotal`, `Discount`, `Total` (`Total = Subtotal − Discount`); `discount = round(subtotal ×
   discountPercent / 100, 2)` computed once at checkout from the resolved `CouponView`. `Order` and
   `OrderStatusView` fold the three fields; a no-coupon order carries `Discount = 0`, `Total = Subtotal`,
   `CouponCode = null`. This is the additive wire-shape widening the `placedAt`/`cancelReason` enrichment
   (slice 025) already precedents — existing consumers reading `{ id, customerId, status, lines, total }` are
   unaffected. `total` remains the amount reservation/payment/`CommitStock` act on.

6. **Slice 6.4 is a fold + a conditional append at three existing sites, not a new handler.** The `Order`
   aggregate gains `CouponId?` (folded from `CouponRedeemed`, cleared by `CouponRedemptionReleased`). Each of
   the three cancellation sites — `StockReservationFailedHandler` (4.5), `PaymentDecisionHandler` decline
   branch (4.6), `PaymentTimeoutHandler` (4.7) — already `FetchForWriting<Order>` and append `OrderCancelled`;
   each additionally appends a `WithTag(couponId)`-tagged `CouponRedemptionReleased` **iff**
   `stream.Aggregate.CouponId is not null`, in the same transaction. At-most-one-release is inherited free from
   the terminal-once `OrderCancelled` guard each handler already enforces — no new idempotency machinery.

7. **Code uniqueness is a partial-unique index on `CouponView.Code` (Workshop 003 §8 item 3).** The open-cart
   partial-unique-index precedent (`Program.cs` Cart index). A duplicate `DefineCoupon` surfaces as the index
   violation → `409`. A code-uniqueness DCB would be over-engineering for seed-issued definitions; it becomes a
   natural second DCB only if coupon authoring ever turns user-facing and concurrent (deferred).

8. **`POST /coupons` is the seed-realized definition path.** A Wolverine.Http endpoint (`Features/DefineCoupon.cs`)
   the seeder drives with real HTTP — the same decoupled pattern the seeder uses for products/stock/customers
   (no project reference on any service). Validation (`cap ≥ 1`, `discountPercent ∈ (0,100]`) rejects with
   `400` via `Results.Problem`; a duplicate code rejects `409`. The endpoint is anonymous (a Seller-lane admin
   path this round, like the passwordless `POST /customers`); a real Promotions service would own auth later.

9. **DCB schema opt-in is Orders-only.** `EnableDcb()` sits in Orders' `AddMarten` alone;
   `ApplyAllDatabaseChangesOnStartup()` adds the `tags` column + GIN index to `orders.mt_events` on boot. No
   other service's store is touched (Catalog, Inventory, Identity), honoring ADR 002's per-schema isolation and
   ADR 024's "opts into the DCB schema" scope. The addition is additive and low-blast-radius — existing Order
   streams simply carry a null `tags` array.

10. **No cross-BC contract, no new broker topology.** Redemption and release are intra-Orders (ADR 024); the
    coupon events and `CouponId` tag never leave the service. No `CritterMart.Contracts` type, no new exchange
    or queue. The demo's observability comes from the existing `marten.event.append` counter now tagging the
    three coupon event types.
