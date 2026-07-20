# Design — slice 6.6 per-customer coupon preview + tailored refusal copy

This note records the implementation-session decisions for slice 6.6. The two **scoping** forks (one slice, not two; enrich `GET /coupons/{code}/validate` rather than fork a sibling route) were settled with the owner in the design session and are recorded in [`proposal.md`](proposal.md) — they are not re-opened here. What follows is the mechanics the implementation follows, plus the two UI calls Workshop 003 §8 item 6 explicitly deferred to this session.

Slice 6.6 appends **nothing**. It is the only slice in Workshop 003 with no command and no event on its write path — the `customerId` amendment below is an *evolution of existing events*, not a new one.

## Decision 1 — the `customerId` event amendment must land first, and why it is unavoidable

Through slice 6.5 the `(coupon × customer)` pair existed **only** as a `CouponCustomerTag`. That is sufficient for the write-time boundary, which is opened by tag query (`FetchForWritingByTags`). It is **not** sufficient for a read model: a Marten `MultiStreamProjection` routes an event to a document by an **event member** —

```csharp
Identity<CouponRedeemed>(e => e.CouponId);   // the shipped CouponUsageViewProjection
```

— and a tag is a *write-side query mechanism*, not a projection grouping key. There is no `Identity<T>(e => tagOf(e))` seam. So the per-customer view a preview needs cannot be projected from the events as slice 6.5 left them, and step 1 is a hard prerequisite of step 2 rather than a convenience.

Both events gain the field as an **optional defaulted record parameter**, the identical non-breaking evolution `PerCustomer` (6.5) and `OneRedemptionPerCustomer` (6.5) already made:

```csharp
public record CouponRedeemed(
    string OrderId, string CouponId, string CouponCode, decimal Discount,
    bool PerCustomer = false, string CustomerId = "");

public record CouponRedemptionReleased(string OrderId, string CouponId, string CustomerId = "");
```

`string CustomerId = ""` rather than `string?`: the empty string is the natural "unattributed" value, it keeps the projection's identity expression total (no null-forgiving operator, no null-guard in the routing lambda), and old serialized JSON — which carries no such property — folds to it. `CustomerId` is appended **last** in each record's parameter list so every existing positional construction site keeps compiling unchanged; the append sites then pass it explicitly.

**Populating it is plumbing, not sourcing.** The customer id is already threaded to every append site for slice 6.5's composite tag: `Features/PlaceOrder.cs` holds `customerId` as a local throughout `RedeemWithDcbAsync`, and `Promotions/CouponRelease.cs` already takes a `customerId` parameter that its three callers (`Ordering/{StockReservationOutcomeHandlers,PaymentHandlers,PaymentTimeoutHandler}.cs`) already supply from `Order.CustomerId`. Neither the three handlers nor `AppendCouponRelease`'s signature change — only the `new CouponRedemptionReleased(...)` construction inside it.

**Set `CustomerId` unconditionally, not only for per-customer coupons.** `PerCustomer` gates the *tag* because the tag defines a boundary that should not exist for a global-cap-only coupon. The event member is a plain fact about who redeemed — always true, always cheap, and recording it uniformly keeps the event honest and avoids a second forward-only cliff if `oneRedemptionPerCustomer` is ever flipped on for an existing coupon. The *view* is the thing that must stay policy-scoped, and it is scoped at the query (Decision 3), not at the append.

## Decision 2 — the view is a second inline `MultiStreamProjection`, keyed on the tag's own value shape

`CustomerCouponUsageView` mirrors the shipped `CouponUsageView` structurally and differs only in its identity:

```csharp
public class CustomerCouponUsageView
{
    public string Id { get; set; } = string.Empty;   // "{couponId}|{customerId}"
    public int NetCount { get; set; }
}

public partial class CustomerCouponUsageViewProjection
    : MultiStreamProjection<CustomerCouponUsageView, string>
{
    public CustomerCouponUsageViewProjection()
    {
        Identity<CouponRedeemed>(e => CustomerCouponUsageView.KeyFor(e.CouponId, e.CustomerId));
        Identity<CouponRedemptionReleased>(e => CustomerCouponUsageView.KeyFor(e.CouponId, e.CustomerId));
    }

    public void Apply(CouponRedeemed e, CustomerCouponUsageView view) => view.NetCount++;
    public void Apply(CouponRedemptionReleased e, CustomerCouponUsageView view) => view.NetCount--;
}
```

Three points carry weight:

- **`partial` is load-bearing** (Marten 9 convention): conventional `Apply` methods are dispatched by the compile-time JasperFx source generator, which extends the partial class. Without it the host refuses to boot with `InvalidProjectionException` — `docs/skills/marten-projection-conventions/SKILL.md`, DEBT row 1.
- **`"{couponId}|{customerId}"`, built by one named helper.** The composite key mirrors `CouponCustomerTag`'s value shape deliberately — the boundary and the view describe the same pair, so they should spell it the same way — but the view does **not** take a dependency on the tag type (a document identity keyed off a DCB tag record would imply a coupling that does not exist). `CustomerCouponUsageView.KeyFor(couponId, customerId)` is the view's own one canonical construction site, used by both `Identity` routes and by the query in Decision 3, so the encoding never drifts between writer and reader.
- **Inline, not async** — the same call `CouponUsageView` made (Workshop 003 §8 item 2). No async daemon runs this round, so an async advisory view would sit perpetually empty and could not serve the affordance it exists for. Registered in `Program.cs` alongside `CouponUsageViewProjection`.

**Unattributed events route to a `"{couponId}|"` bucket.** Pre-6.6 events fold with `CustomerId = ""`, producing a per-coupon document that belongs to no customer. This is harmless: the query never constructs that key (an authenticated caller always has a non-empty `sub`), so the bucket is written and never read. Filtering those events out of the projection was considered and rejected — it buys nothing, and a routing predicate is a second place for the forward-only rule to be stated and to drift out of step with the query.

**Naming.** `CustomerCouponUsageView` (persisted projection) sits one character from `CustomerCouponUsage` (the id-less `[BoundaryAggregate]`, never persisted, never queried by the UI). That is the same deliberate one-character pairing `CouponUsage` / `CouponUsageView` already established: same arithmetic, different existence, and only the boundary is ever the authority. The two types stay separate; neither is expressed in terms of the other.

## Decision 3 — the fourth status is gated twice, and the precedence ladder is spec, not taste

`ValidateCouponEndpoint.Get` becomes optionally authenticated: it takes `ClaimsPrincipal user` alongside the existing `code` + `IQuerySession`, and it is **not** `[Authorize]`-annotated. It must never answer `401` — an anonymous caller receives today's answer byte-for-byte.

`user.CustomerId()` (`Auth/CustomerIdentity.cs`) is the wrong tool here: it is documented as "guaranteed present behind `[Authorize]`" and **throws** on a missing `sub`, which is exactly right for a `[Authorize]`'d endpoint and exactly wrong for an optionally-authenticated one where an absent claim is the normal anonymous case. This endpoint reads the claim directly and treats absent-or-empty as anonymous. That divergence is deliberate and is commented at the call site so the next reader does not "fix" it into the throwing helper.

The `already_redeemed` branch is gated on **both** conditions before the view is touched:

1. the resolved definition carries `OneRedemptionPerCustomer = true`, **and**
2. the request carries a non-empty `sub`.

Either gate failing skips the view load entirely — a global-cap-only coupon and an anonymous caller each cost exactly the queries slice 6.2 made. The ladder is then evaluated in **exactly** this order:

`invalid` → `already_redeemed` → `exhausted` → `valid`

which mirrors `RedeemWithDcbAsync`'s check order (per-customer existence before global cap) precisely. This is a spec'd requirement, not a preference: the two refusals send a customer to different remedies — *"try another code"* versus *"try again later"* — so a preview that blamed the crowd for a personal refusal would teach the wrong one.

**The preview never becomes load-bearing.** `IQuerySession` (not `IDocumentSession`) remains the structural guarantee that this handler cannot append, and nothing in `PlaceOrder` learns of the view's existence. No "optimization" skips a boundary read because the preview already answered `already_redeemed`.

## Decision 4 — the refusal copy, and what stays frozen

The `409` `detail` in `PlaceOrder.cs` becomes **"You've already used this coupon — remove it to continue, or try another."** — a fixed sentence, parallel in shape to the sibling `CouponExhausted` copy, that names the personal reason and hands the decision back to the shopper.

It drops the interpolated coupon code. The old sentence interpolated `coupon.Code` and read like a constraint violation; the new one addresses the shopper, and the storefront renders it beside the code the shopper just typed, so restating it is noise. The `409` status code and the `CouponAlreadyRedeemedByCustomer` **title token** are unchanged — no machine-readable contract moves, and every existing test asserting on the title keeps passing untouched.

## Decision 5 — the frontend needs less than the proposal assumed

The proposal's Impact lists "send the bearer token on the validate call when signed in" as frontend work. **It is already done.** `fetchCouponValidation` routes through `fetchParsed(url, schema, ctx)`, and `client.ts`'s `authHeaders(ctx)` attaches `Authorization: Bearer …` whenever `ctx.token` is non-null (Convention 4, ADR 023); `useValidateCoupon` obtains that context from `useApiContext()`. A signed-in shopper's validate call already carries the token today and reaches an endpoint that ignores it. So the whole frontend delta is:

- `couponSchema.ts` — add `"already_redeemed"` to the `CouponStatusSchema` enum. The enum is closed by design, so **without this the new status fails zod parsing and surfaces as a fetch error, not as UI copy** — this is the one frontend change that is load-bearing rather than cosmetic.
- `CartPage.tsx` — a third branch in `CouponField`'s `errorMessage` ladder rendering *"You've already used this coupon."* Structurally identical to the `exhausted` branch; the existing `role="alert"` / `aria-describedby` wiring covers it, and the "only a `valid` answer holds" rule already handles not applying the coupon.

## Decision 6 — the two deferred UI calls: both declined, and for the same reason

Workshop 003 §8 item 6 named these as implementation-session calls with no model consequence. Both are **declined this slice** — not on taste, but because each would require a contract change the spec forbids:

1. **Badge the `oneRedemptionPerCustomer` policy to anonymous shoppers** ("one per customer" on the coupon field) — declined. The validate response does not carry the policy flag, and it cannot gain one: the spec pins the anonymous response *identical to the behavior that shipped in slice 6.2*, and a badge visible to anonymous shoppers means exactly that response carrying a new field. Adding it would also leak a definition detail to unauthenticated callers for the first time — a small but real widening of a public contract, which deserves its own decision rather than riding a UX polish slice.
2. **Nudge a signed-out shopper to sign in for a sharper answer** — declined, and dependent on (1): a nudge that fires on *every* coupon is noise, and firing it only for per-customer coupons requires the same forbidden flag. It also inverts the slice's premise — 6.6 exists to move bad news *earlier*, not to add a new interstitial ask before a shopper can see a discount.

Both remain available as a later slice once there is a reason to widen the anonymous contract deliberately. Recording the decision here rather than in the retro satisfies the handoff's "silence is not an option" call.

## Decision 7 — forward-only is implemented as written, not worked around

`CustomerCouponUsageView` cannot see redemptions appended before `customerId` existed. The resulting error is **one-sided by construction**: the preview may **under-warn** (a customer whose only redemption predates the field sees `valid` and is refused at checkout exactly as today) and can never **over-warn** (it will not wrongly accuse someone entitled to the discount). That is tolerable precisely because the read is advisory — the same license `CouponUsageView` already holds to lag.

There is a GWT scenario (Workshop 003 §6.6) and an OpenSpec scenario for exactly this, and it is implemented as a **test** rather than patched with a backfill. A backfill would mean rewriting or supplementing history to serve a read model, which is a materially different conversation (event-stream mutation vs. projection rebuild) and would be its own slice with its own ADR. Nothing here forecloses it.
