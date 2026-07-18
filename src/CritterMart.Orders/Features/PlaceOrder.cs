using System.Security.Claims;
using CritterMart.Orders.Auth;
using CritterMart.Orders.Customers;
using CritterMart.Orders.Ordering;
using CritterMart.Orders.Promotions;
using CritterMart.Orders.Shopping;
using JasperFx.Events;
using JasperFx.Events.Tags;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Wolverine.Http;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Features;

// The Customer checks out their open cart, turning it into an order (Workshop 001 slice 4.1).
// customerId is the authenticated JWT `sub` claim — the same identity transport as GET /orders/mine
// and GET /carts/mine (ADR 023 hard cutover). The cart's snapshotted lines + computed total are
// frozen onto a new Order stream. This is the project's first multi-stream atomic write.
//
// Slice 6.3 (Workshop 003) adds an OPTIONAL coupon: PlaceOrder MAY carry a couponCode as an optional
// `?couponCode=` QUERY parameter. Absent → the path below is byte-for-byte slice 4.1 (POST /orders with no
// body, the existing contract — a body-bound field would 400 the bodyless checkout the W3 screen already
// uses). Present → the DCB redemption branch resolves the coupon, enforces the global cap via a boundary
// pre-check, and appends a tagged CouponRedeemed onto the same order stream in the same transaction —
// CritterMart's first Dynamic Consistency Boundary (ADR 024).
//
// Slice 6.5 (ADR 024 §38) composes a SECOND DCB when the coupon is oneRedemptionPerCustomer: the composite
// (coupon × customer) boundary, enforcing "at most once per customer" alongside the global cap in the same
// transaction. CritterMart's first composite-tag boundary — the boundary aligns with a PAIR, not a single id.

// The orderId handed back so the caller can read the order at GET /orders/{orderId}.
public record PlaceOrderResponse(string OrderId);

public static class PlaceOrderEndpoint
{
    // Returns the HTTP response AND two cascaded outputs: a ReserveStock message (slice 4.2) and a
    // SCHEDULED OrderPaymentTimeout self-message (slice 4.7). Wolverine.Http treats the IResult as
    // the response and publishes the other tuple members through the outbox when the Marten
    // transaction commits — so the order is durably placed before the cross-BC reservation request
    // goes out, and the payment deadline is set in the same step that placed the order (the Bruun
    // temporal automation's starting gun; Workshop slice 4.1 writes-to). On a rejection there is no
    // order, so both cascades are null (Wolverine skips null cascading messages).
    [Authorize]
    [WolverinePost("/orders")]
    public static async Task<(IResult, Contracts.ReserveStock?, DeliveryMessage<OrderPaymentTimeout>?)> Post(
        ClaimsPrincipal user,
        IDocumentSession session,
        IDocumentStore store,
        IMessageBus bus,
        [FromServices] PaymentDeadline deadline,
        string? couponCode = null)
    {
        // Identity is the authenticated JWT `sub` claim, guaranteed by [Authorize] (ADR 023 hard cutover):
        // a missing/bad/expired token is a 401 decided by JwtBearer before this handler runs — consistent
        // with GET /orders/mine and GET /carts/mine.
        var customerId = user.CustomerId();

        // Resolve the customer's open cart — the same indexed Cart query AddToCart uses.
        // A cart that was already checked out has IsOpen=false, so a repeat PlaceOrder finds no
        // open cart and is rejected here: the workshop's "cart already checked out" failure
        // path, handled for free by open-cart resolution (no separate guard needed).
        var cart = await session.Query<Cart>()
            .Where(c => c.CustomerId == customerId && c.IsOpen)
            .FirstOrDefaultAsync();

        if (cart is null)
        {
            return (Results.Problem(
                title: "NoOpenCart",
                detail: $"Customer '{customerId}' has no open cart to place.",
                statusCode: StatusCodes.Status409Conflict), null, null);
        }

        // Defensive guard for the workshop's CartEmpty path. Unreachable in 4.1 (a cart is
        // created with its first line and remove-item is 3.2), but it guards the invariant the
        // moment 3.2 makes a lineless-but-open cart reachable.
        if (cart.Lines.Count == 0)
        {
            return (Results.Problem(
                title: "CartEmpty",
                detail: $"Customer '{customerId}' has an empty cart.",
                statusCode: StatusCodes.Status409Conflict), null, null);
        }

        var items = cart.Lines
            .Select(l => new OrderLine(l.Sku, l.Quantity, l.Name, l.Price))
            .ToList();
        var subtotal = items.Sum(i => i.Quantity * i.Price);

        // ── Coupon path (slice 6.3, DCB) ────────────────────────────────────────────────────────────
        // A carried couponCode diverts to the DCB redemption path (its own transaction + retry, below); the
        // API is the boundary — the W2 advisory check (slice 6.2) is a convenience, not a guard.
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var coupon = await session.Query<CouponView>().FirstOrDefaultAsync(c => c.Code == couponCode);
            if (coupon is null)
            {
                return (Results.Problem(
                    title: "CouponInvalid",
                    detail: $"Coupon '{couponCode}' is not valid.",
                    statusCode: StatusCodes.Status409Conflict), null, null);
            }

            return await RedeemWithDcbAsync(store, bus, customerId, cart.Id, items, subtotal, coupon, deadline);
        }

        // ── No coupon → slice 4.1 exactly (byte-for-byte unchanged) ─────────────────────────────────
        // The multi-stream atomic write (slice 4.1's teaching beat): a new Order stream AND the cart's
        // terminal CartCheckedOut, committed together by AutoApplyTransactions in ONE transaction. The 4-arg
        // OrderPlaced convenience prices the order undiscounted (Subtotal == Total, Discount == 0).
        var orderId = Guid.NewGuid().ToString();
        session.Events.StartStream<Order>(
            orderId, new OrderPlaced(orderId, customerId, items, subtotal));

        var cartStream = await session.Events.FetchForWriting<Cart>(cart.Id);
        cartStream.AppendOne(new CartCheckedOut(orderId));

        var reserveStock = new Contracts.ReserveStock(
            orderId,
            items.Select(i => new Contracts.ReserveStockLine(i.Sku, i.Quantity)).ToList());
        var paymentTimeout = new OrderPaymentTimeout(orderId).DelayedFor(deadline.Duration);

        return (Results.Created($"/orders/{orderId}", new PlaceOrderResponse(orderId)), reserveStock, paymentTimeout);
    }

    // The DCB redemption path (slice 6.3): the canonical Marten "reload and retry" loop. DCB optimistic
    // concurrency is CAP-BLIND — FetchForWritingByTags arms an assertion on the tag-set, so ANY concurrent
    // redemption invalidates a commit even when both are safely under the cap. So a bare pre-check would
    // under-admit under a burst (the cap never EXCEEDS, but far fewer than `cap` get through). The retry
    // re-reads the boundary and re-decides each attempt: a loser still under the cap succeeds on retry; only
    // a genuinely-full cap yields CouponExhausted. Exactly `cap` redemptions ever succeed (Workshop 003 §6.3).
    //
    // Slice 6.5: for a per-customer coupon this opens TWO boundaries per attempt (the composite per-customer
    // boundary + the global cap) and appends a doubly-tagged CouponRedeemed, so a DcbConcurrencyException from
    // EITHER boundary drives the same retry — including a customer's own double-submit, which settles to exactly
    // one (the loser re-reads the composite boundary at net count 1 → CouponAlreadyRedeemedByCustomer).
    //
    // Each attempt uses a FRESH session because a session whose SaveChanges threw is dirty; and this path
    // does NOT ride AutoApplyTransactions (which owns the injected session's commit post-handler and would
    // re-throw the caught exception). Cascades therefore publish through the bus AFTER the commit rather than
    // via the tuple/outbox — a post-commit send (acceptable: the order is durably placed; ReserveStock and
    // the payment timeout are at-least-once safety nets). The no-coupon path keeps the transactional outbox.
    private static async Task<(IResult, Contracts.ReserveStock?, DeliveryMessage<OrderPaymentTimeout>?)>
        RedeemWithDcbAsync(
            IDocumentStore store, IMessageBus bus, string customerId, string cartId,
            List<OrderLine> items, decimal subtotal, CouponView coupon, PaymentDeadline deadline)
    {
        var discount = Math.Round(subtotal * coupon.DiscountPercent / 100m, 2);
        var total = subtotal - discount;
        var query = new EventTagQuery().Or<CouponId>(new CouponId(coupon.Id));

        // Slice 6.5: a per-customer coupon composes a SECOND DCB — the composite (coupon × customer) boundary.
        // Built once (the pair is constant across retries): a single-scalar CouponCustomerTag, queried through
        // the same single-tag path as the global cap. Null for a global-cap-only coupon → no second boundary opened.
        var perCustomerQuery = coupon.OneRedemptionPerCustomer
            ? new EventTagQuery().Or<CouponCustomerTag>(CouponCustomerTag.For(coupon.Id, customerId))
            : null;

        // Bounded so sustained contention cannot spin forever; generous enough to converge for realistic
        // concurrency (each round commits at least one, so winners settle in ~cap rounds).
        const int maxAttempts = 25;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await using var s = store.LightweightSession();

            // Slice 6.5: the per-customer EXISTENCE check runs BEFORE the global-cap count check, so a customer
            // who already redeemed hears the honest reason (AlreadyRedeemedByCustomer) rather than a coincidental
            // CouponExhausted. Reading the composite boundary here ALSO arms its tag-scoped concurrency assertion
            // (the doubly-tagged append below participates in it) — a transactional backstop. In practice a single
            // customer's concurrent checkouts are already serialized by the one-open-cart invariant (they collide
            // on the cart stream first), so this boundary's real guarantee is the cross-order EXISTENCE check: a
            // LATER order refused because an EARLIER one already redeemed. Distinct (coupon × customer) pairs are
            // independent boundaries, so different customers never false-conflict here.
            if (perCustomerQuery is not null)
            {
                var perCustomer = await s.Events.FetchForWritingByTags<CustomerCouponUsage>(perCustomerQuery);
                if ((perCustomer.Aggregate?.NetCount ?? 0) >= 1)
                {
                    return (Results.Problem(
                        title: "CouponAlreadyRedeemedByCustomer",
                        detail: $"Coupon '{coupon.Code}' may be redeemed only once per customer, and you have already redeemed it.",
                        statusCode: StatusCodes.Status409Conflict), null, null);
                }
            }

            var boundary = await s.Events.FetchForWritingByTags<CouponUsage>(query);
            if ((boundary.Aggregate?.NetCount ?? 0) >= coupon.Cap)
            {
                return (Results.Problem(
                    title: "CouponExhausted",
                    detail: $"Coupon '{coupon.Code}' has reached its redemption limit.",
                    statusCode: StatusCodes.Status409Conflict), null, null);
            }

            var orderId = Guid.NewGuid().ToString();

            // The tagged CouponRedeemed rides the SAME new order stream as the priced OrderPlaced, in one
            // transaction (ADR 024's "real order streams"; mechanic confirmed by the slice-6.3 DCB spike),
            // plus the cart's terminal CartCheckedOut — the multi-stream atomic write, now DCB-guarded.
            s.Events.StartStream<Order>(
                orderId, new OrderPlaced(orderId, customerId, items, subtotal, discount, total));

            var redeemed = s.Events.BuildEvent(
                new CouponRedeemed(orderId, coupon.Id, coupon.Code, discount, coupon.OneRedemptionPerCustomer));
            redeemed.WithTag(new CouponId(coupon.Id));
            // Slice 6.5: a per-customer redemption ALSO carries the composite tag, so both boundaries' assertions
            // are armed by this one committed event and the release can decrement the per-customer boundary too.
            if (coupon.OneRedemptionPerCustomer)
            {
                redeemed.WithTag(CouponCustomerTag.For(coupon.Id, customerId));
            }
            s.Events.Append(orderId, redeemed);

            var cartStream = await s.Events.FetchForWriting<Cart>(cartId);
            cartStream.AppendOne(new CartCheckedOut(orderId));

            try
            {
                await s.SaveChangesAsync();
            }
            catch (DcbConcurrencyException)
            {
                continue; // a concurrent redemption raced into the boundary — reload and retry
            }

            // Committed. Cascade the same reservation + payment-deadline slice 4.1 does, post-commit via the bus.
            await bus.PublishAsync(new Contracts.ReserveStock(
                orderId, items.Select(i => new Contracts.ReserveStockLine(i.Sku, i.Quantity)).ToList()));
            await bus.PublishAsync(
                new OrderPaymentTimeout(orderId),
                new DeliveryOptions { ScheduleDelay = deadline.Duration });

            return (Results.Created($"/orders/{orderId}", new PlaceOrderResponse(orderId)), null, null);
        }

        // Retries exhausted under sustained contention — safe to treat as exhausted (no order committed).
        return (Results.Problem(
            title: "CouponExhausted",
            detail: $"Coupon '{coupon.Code}' is under heavy demand — please try again.",
            statusCode: StatusCodes.Status409Conflict), null, null);
    }
}

public static class OrderEndpoint
{
    // Enrich the order with the customer's display name resolved from the consumer-local
    // LocalCustomerView (slice 5.3). Two primary-key loads: the order view (existing) and the
    // customer view (new). CustomerName is null when the local model is absent — the eventually-
    // consistent degradation (PL event not yet delivered). No synchronous call to Identity.
    [WolverineGet("/orders/{orderId}")]
    public static async Task<IResult> Get(string orderId, IQuerySession session)
    {
        var view = await session.LoadAsync<OrderStatusView>(orderId);
        if (view is null)
            return Results.NotFound();

        var customer = await session.LoadAsync<LocalCustomerView>(view.CustomerId);
        return Results.Ok(EnrichedOrderView.From(view, customer?.DisplayName));
    }

    // The Bruun todo-list (slice 4.7): every order still awaiting its terminal state, soonest
    // deadline first. Rows appear when an order is placed and vanish when it confirms or cancels
    // (the OrdersAwaitingPayment projection's conditional delete) — so this list is always the
    // live set of orders the payment-timeout automation is watching. A literal route segment, so
    // it wins over /orders/{orderId} by ASP.NET Core route precedence.
    [WolverineGet("/orders/awaiting-payment")]
    public static async Task<IResult> GetAwaitingPayment(IQuerySession session, PaymentDeadline deadline)
    {
        // The view stores PlacedAt; the visible Deadline is PlacedAt + the configured timeout, applied
        // here on read (the projection is stateless — see OrdersAwaitingPayment remarks). Ordering by
        // PlacedAt equals ordering by Deadline since the timeout is constant across rows.
        var rows = await session.Query<OrderAwaitingPayment>()
            .OrderBy(x => x.PlacedAt)
            .ToListAsync();
        var result = rows.Select(r =>
            new OrderAwaitingPaymentRow(r.Id, r.CustomerId, r.Total, r.PlacedAt.Add(deadline.Duration)));
        return Results.Ok(result);
    }
}
