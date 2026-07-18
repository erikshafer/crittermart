# Design — slice 6.5 one-redemption-per-customer (the composite-tag second DCB)

This note records the modeling fork the owner settled and the mechanics the implementation follows. Unlike slice 6.3, there is **no open Marten mechanic to spike** — the composite tag is byte-for-byte the shipped single-tag shape, verified against the resolved assembly (below). The novelty is entirely in the domain model.

## Decision 1 — per-customer is an opt-in policy on the definition (settled with Erik)

Three coherent models were weighed at session start (`AskUserQuestion` with previews); the owner chose the first:

- **Chosen — a `oneRedemptionPerCustomer` bit on `CouponDefined`.** Per-customer is a coupon **policy**, event-sourced alongside the cap (configuration-as-events extended). Checkout opens the composite boundary **only when the flag is set**. FLASH20 stays a pure global-cap demo (flag off); a new `FIRSTORDER` (flag on) demonstrates the composite. Preserves the first DCB's clean story, keeps the two invariants separable, still teaches the composite tag.
- *Rejected — compose unconditionally* (every coupon is also once-per-customer): no new field, but it retroactively changes FLASH20's meaning and falsifies Narrative 011's "one person could redeem all three" line and a couple of existing tests.
- *Rejected — a distinct once-per-customer kind with no global cap*: `cap` is a required, cap-shaped field, so "no cap" is an awkward `int.MaxValue` — which is just the chosen option with the global cap disabled. Reachable as a special case of the chosen model, so it does not warrant its own shape.

**A boolean, not a `perCustomerLimit` integer.** The modeled invariant is exactly "at most once" (ADR 024 §38 / Workshop 003 §8 item 6). A bool is its crispest expression and makes the checkout check an **existence** test (`netCount ≥ 1`), the textbook composite-DCB shape (the skill's `AlreadySubscribed` course×student example). A `perCustomerLimit > 1` generalization is a later widening the definition payload can take without touching the boundary mechanic.

## Decision 2 — the composite is a single-scalar tag, not a two-tag `.And<>()` (verified)

The handoff flagged the composite `EventTagQuery` shape as the one place a stale (Polecat-framed) skill could mislead. Reflected on the resolved `JasperFx.Events` **v2.27.0.0** (the Marten 9.15.1 dependency in `bin`): `EventTagQuery` exposes `For<T>`, `Or<T>`, `Or<TEvent,T>`, and `AndEventsOfType<T1..T6>` — and **no method to AND two *different* tag values** (`AndEventsOfType` filters event *types*, not tags). `ITagTypeRegistration.ExtractValue` + a singular `SimpleType` confirm a tag stores **one scalar**.

So the `(coupon × customer)` boundary is a **single strong-typed tag whose string value encodes the pair**:

```csharp
public record CouponCustomerTag(string Value)
{
    public static CouponCustomerTag For(string couponId, string customerId) =>
        new($"{couponId}|{customerId}");
}
```

registered exactly like `CouponId` — `opts.Events.RegisterTagType<CouponCustomerTag>("couponcustomer").ForAggregate<CustomerCouponUsage>()` — and queried with the proven single-tag path `new EventTagQuery().Or<CouponCustomerTag>(CouponCustomerTag.For(couponId, customerId))`. The composite value (`"{couponId}|{customerId}"`) matches no event property, so Marten's tag inference cannot manufacture or omit it — the tag lands **only** by explicit `evt.WithTag(...)`, the same mechanic slice 6.3 uses. Zero new API; the per-customer self-race integration test is the empirical proof (retro 039's "a concurrency invariant needs a concurrency test").

## Decision 3 — two boundary reads compose in one transaction; the existing retry loop covers both

`RedeemWithDcbAsync` already loops (fresh session per attempt) because DCB optimistic concurrency is cap-blind. For a per-customer coupon each attempt now opens **both** boundaries on the same session:

1. `FetchForWritingByTags<CouponUsage>` over `CouponId` — the global cap (unchanged). `netCount ≥ cap` → `409 CouponExhausted`.
2. `FetchForWritingByTags<CustomerCouponUsage>` over `CouponCustomerTag` — the per-customer existence. `netCount ≥ 1` → `409 CouponAlreadyRedeemedByCustomer`.

Both reads arm their own tag-scoped concurrency assertions; the appended `CouponRedeemed` carries **both** tags, so a race invalidating **either** boundary throws `DcbConcurrencyException` and the loop retries against fresh reads. This is option 1's "two DCB reads, one transaction" made concrete.

**What actually races, and what the per-customer boundary really proves.** The global boundary is the burst-contention point: different customers redeeming the same coupon share its `CouponId` tag and retry against it (exactly as the first DCB does). The per-customer boundary, by contrast, is keyed by a `(coupon, customer)` **pair** — different customers hold *independent* composite boundaries and never false-conflict; only the *same* pair could race. And a single customer's concurrent checkouts are already serialized by the **one-open-cart invariant** (a customer has at most one open cart, a partial-unique index), so two concurrent `POST /orders` for one customer collide on the *cart stream* (a plain Marten `ConcurrencyException`, not a `DcbConcurrencyException`) before the composite boundary is even the deciding factor. So the per-customer invariant is enforced in practice by the **cross-order existence check** — a *later* order refused because an *earlier* one already redeemed — and the composite DCB is the transactional primitive that makes that existence check sound across arbitrarily many of the customer's order streams (the reason it is a DCB and not a projection query). The DCB assertion is the backstop that keeps it correct if the one-open-cart serialization is ever relaxed (e.g. multi-device carts). The reachable, meaningful concurrency test is therefore *different customers concurrent → all succeed* (composite isolation), not a same-customer self-race (which the cart guard makes nondeterministic to exercise via the endpoint).

**Order of the checks.** The per-customer existence check runs **before** the global-cap check so a customer who already redeemed is told the honest reason (`AlreadyRedeemedByCustomer`) rather than a misleading `CouponExhausted` when the coupon also happens to be near its global cap. Both boundaries are still read on the success path (both assertions must arm).

## Decision 4 — the release rides both tags; the flag lives on the `Order` aggregate

The three cancellation sites (4.5/4.6/4.7) already call `session.AppendCouponRelease(orderId, order.CouponId)` to append a `CouponId`-tagged `CouponRedemptionReleased` iff the order redeemed a coupon. For a per-customer coupon the release must **also** carry the composite tag, so the per-customer boundary decrements and the customer's slot returns. The helper needs the customer id (already on `Order` from `OrderPlaced`) and whether the coupon was per-customer — the one fact the aggregate did not yet track. So:

- `CouponRedeemed` gains a `PerCustomer` bool; `Order.Apply(CouponRedeemed)` folds it to a new `Order.CouponPerCustomer` field (cleared by the release fold, like `CouponId`).
- `AppendCouponRelease(session, orderId, couponId?, customerId?, perCustomer)` tags `CouponId` always and `CouponCustomerTag.For(couponId, customerId)` when `perCustomer` — keeping the composite-tag construction inside Promotions (the helper stays primitive-typed; `Order` carries no Promotions type). The three call sites pass `order.CouponId, order.CustomerId, order.CouponPerCustomer`.

At-most-one-release per redemption is inherited unchanged from the terminal-once `OrderCancelled` guard, so the per-customer net count can never under-count below true usage.

## Decision 5 — advisory preview stays global-only (no per-customer preview this slice)

`GET /coupons/{code}/validate` is anonymous and holds no customer identity, and the advisory-vs-authoritative split (Workshop 003 §3) makes checkout the sole authority. Previewing "you already used this" would need the caller's `sub` on the query and a per-customer advisory view — a UX follow-on, not part of the write-side boundary this slice lands. The new `409 CouponAlreadyRedeemedByCustomer` surfaces at checkout exactly as `CouponExhausted` does; the storefront's existing ProblemDetails handling covers it (tailored copy is a follow-on).
