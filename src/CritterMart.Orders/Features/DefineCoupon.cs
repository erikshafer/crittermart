using CritterMart.Orders.Promotions;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Seller lane defines a coupon (Workshop 003 slice 6.1, configuration-as-events). Seed-issued this
// round: the seeder drives this endpoint with real HTTP, the same decoupled way it publishes products
// and receives stock (no project reference on any service). An anonymous admin path — the passwordless
// POST /customers precedent — a real Promotions service would own auth later (ADR 024).
//
// Slice 6.5: OneRedemptionPerCustomer opts the coupon into the composite (coupon × customer) DCB — a given
// customer may redeem it at most once. Defaulted false so existing callers/tests define a global-cap-only coupon.
public record DefineCoupon(string Code, int DiscountPercent, int Cap, bool OneRedemptionPerCustomer = false);

// The coupon id handed back so the location points at the (future 6.2) coupon read.
public record DefineCouponResponse(string CouponId, string Code);

public static class DefineCouponEndpoint
{
    // Railway-style guard: reject a nonsensical definition (400) and a duplicate code (409) before any
    // event is appended. Both are expected, modeled outcomes — ProblemDetails, never a throw. Because it
    // short-circuits, a rejected command starts no stream. The partial-unique index on CouponView.Code
    // (Program.cs) is the concurrent-duplicate backstop behind this pre-check (the open-cart precedent).
    public static async Task<ProblemDetails> ValidateAsync(DefineCoupon command, IDocumentSession session)
    {
        if (command.Cap < 1)
        {
            return new ProblemDetails
            {
                Title = "InvalidCoupon",
                Detail = $"Cap must be at least 1 (was {command.Cap}).",
                Status = StatusCodes.Status400BadRequest
            };
        }

        if (command.DiscountPercent is <= 0 or > 100)
        {
            return new ProblemDetails
            {
                Title = "InvalidCoupon",
                Detail = $"DiscountPercent must be within (0, 100] (was {command.DiscountPercent}).",
                Status = StatusCodes.Status400BadRequest
            };
        }

        var codeTaken = await session.Query<CouponView>()
            .AnyAsync(c => c.Code == command.Code);

        return codeTaken
            ? new ProblemDetails
            {
                Title = "CouponCodeAlreadyExists",
                Detail = $"A coupon with code '{command.Code}' is already defined.",
                Status = StatusCodes.Status409Conflict
            }
            : WolverineContinue.NoProblems;
    }

    [WolverinePost("/coupons")]
    public static CreationResponse Post(DefineCoupon command, IDocumentSession session)
    {
        // The couponId is the stream key (generated); the human-facing Code is the checkout lookup key.
        var couponId = Guid.NewGuid().ToString();

        // Configuration-as-events: the definition — including cap N and the per-customer policy — is appended
        // as a domain event. AutoApplyTransactions commits it with the inline CouponView projection in one transaction.
        session.Events.StartStream(
            couponId,
            new CouponDefined(
                couponId, command.Code, command.DiscountPercent, command.Cap, command.OneRedemptionPerCustomer));

        return new CreationResponse($"/coupons/{command.Code}");
    }
}
