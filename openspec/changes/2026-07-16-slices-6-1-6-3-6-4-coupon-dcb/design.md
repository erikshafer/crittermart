# Design: slices-6-1-6-3-6-4-coupon-dcb (slices 6.1 / 6.3 / 6.4)

CritterMart's first **Dynamic Consistency Boundary** — a global per-coupon redemption cap enforced inside the
Orders store, spanning many order streams and aligning with no aggregate. See the proposal for the Why/What,
the `coupon-promotion` spec delta for the coupon SHALLs, and the `order-lifecycle` delta for the Order-stream
integration. This document records the implementation decisions, and in particular the one genuinely
non-obvious call — how a tagged redemption event composes with a brand-new order stream in one transaction
(Workshop 003 §8 item 1).

## Verified API surface (Marten 9.15.1, resolved transitively via WolverineFx 6.19.0)

Confirmed present in the pinned `Marten.dll` and against current Marten DCB docs — **not** the Polecat-flavored
symbols the installed `marten-advanced-dynamic-consistency-boundary` / `-cross-stream-operations` skills show
(`docs/skills/DEBT.md` row 3; those name `[BoundaryModel]`, `IEventBoundary<T>`, `EventTagQuery.For(…)` — do
not use them here):

- `opts.Events.EnableDcb()` — opt-in; triggers the `tags TEXT[]` column + GIN index on `orders.mt_events`.
- `opts.Events.TagEvent<T>(e => new[]{ … })` for declarative tagging, and/or per-event
  `var evt = session.Events.BuildEvent(data); evt.WithTag(couponId);` for strong-typed tags.
- `new EventTagQuery().Or<CouponId>(couponId)` → `session.Events.FetchForWritingByTags<CouponUsage>(query)`,
  returning a boundary with `.Aggregate` (nullable), `.LastSeenSequence`, and `.AppendOne(evt)`.
- `SaveChangesAsync()` throws `DcbConcurrencyException` (`ex.Query`, `ex.LastSeenSequence`) when a matching
  tagged event lands inside the boundary between the read and the commit.
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

2. **Append mechanics (Workshop 003 §8 item 1) — SPIKE-CONFIRMED at slice-6.3 start, then locked here.**
   Intent (ADR 024): the tagged `CouponRedeemed` lives on the **real new order stream**, in the same
   transaction as `OrderPlaced`; the DCB boundary provides only the cap assertion at `SaveChangesAsync`. Two
   candidate mechanics, to be disambiguated by a throwaway integration spike **before** writing the handler,
   because the Marten DCB docs show `boundary.AppendOne(evt)` without an explicit stream and do not show a
   `StartStream` composition:
   - **(a)** `session.Events.StartStream<Order>(orderId, orderPlaced)`, then build the `CouponRedeemed`,
     `WithTag(couponId)` it, and append it to the same order stream (`StartStream` overload / `Append`) — the
     boundary read from `FetchForWritingByTags` supplies the concurrency assertion the shared
     `SaveChangesAsync` enforces.
   - **(b)** `boundary.AppendOne(taggedRedeemed)` for the tagged event and `StartStream` for `OrderPlaced`,
     both flushed by one `SaveChangesAsync`, if the boundary requires the tagged append to route through it.
   The spike asserts three things against a real Postgres (Testcontainers): the two events land on the **same**
   order stream; the cap holds under a forced concurrent append (`DcbConcurrencyException` thrown); and a
   non-coupon `PlaceOrder` opens no boundary. Whichever mechanic the spike proves is recorded as the locked
   decision in the slice-6.3 code comment and the retro — no guessing in the shipped handler.

3. **The `DcbConcurrencyException` retry seats handler-local, not as a Wolverine retry policy (Workshop 003
   §8 item 1).** The loser of a race must **re-decide against fresh state**, not blindly replay the same
   append — a Wolverine `OnException().RetryOnce()` would re-run the whole endpoint including the cart
   resolution and re-open the boundary, which is heavier and re-reads the cart needlessly. Instead the
   redemption decision is a small local method wrapped in a bounded `for`/`try` (one retry): on
   `DcbConcurrencyException` it re-reads the boundary once and re-evaluates the cap; a still-full cap becomes
   the `CouponExhausted` rejection. One retry is sufficient because the second read is post-commit-consistent
   for the winning append; a persistent race would need cap-many losers, none of which can succeed past the
   cap. Bounded, not a `while(true)`.

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
