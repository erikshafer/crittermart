using System.Security.Claims;
using CritterMart.Orders.Promotions;
using Marten;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer previews a coupon at cart review (Workshop 003 slice 6.2, the advisory query). A READ-ONLY
// lookup: it resolves the code against the definition (CouponView) and the advisory net usage
// (CouponUsageView) and answers whether the code is redeemable, invalid, or advisorily exhausted — writing
// NOTHING. The checkout DCB append (slice 6.3) stays the sole authority; this query is a convenience that
// softens the surprise of a race, never a guard (Workshop 003 §3, the advisory-vs-authoritative split).
//
// IQuerySession (not IDocumentSession) is the structural guarantee of that intent: a query session has no
// unit of work to commit, so this handler CANNOT append an event even by accident.

// Slice 6.6 makes this query OPTIONALLY AUTHENTICATED and adds a fourth status. It is deliberately NOT
// [Authorize]'d and MUST NEVER answer 401 — an anonymous caller gets slice 6.2's answer byte-for-byte (the
// storefront's public contract does not move). A signed-in caller's answer gains one status: `already_redeemed`.

// The discriminated advisory answer (endpoint-shape decision, design.md §1: settled with Erik over a
// RESTful-resource and a server-priced variant). Status maps one-to-one onto the W2 UI states:
//   valid            → the code applies; DiscountPercent is set and the storefront prices the dollar amount
//                      against the cart total it already holds (the % → $ math stays client-side; the server
//                      never sees cart money here).
//   already_redeemed → (6.6) this SIGNED-IN customer has already used this per-customer coupon
//                      ("you've already used this coupon"). Unreachable anonymously and unreachable for a
//                      coupon whose definition does not carry OneRedemptionPerCustomer.
//   exhausted        → a definition resolves but its advisory net count has reached the cap
//                      ("no longer available").
//   invalid          → no definition resolves for the code ("this code isn't valid").
// DiscountPercent is null for everything but valid. A 200 in every case — checking a code is not an error.
public record CouponValidation(string Code, string Status, int? DiscountPercent);

public static class CouponValidationStatus
{
    public const string Valid = "valid";
    public const string Invalid = "invalid";
    public const string Exhausted = "exhausted";
    public const string AlreadyRedeemed = "already_redeemed";
}

public static class ValidateCouponEndpoint
{
    // NO [Authorize]: this endpoint is OPTIONALLY authenticated (slice 6.6). ClaimsPrincipal is always
    // injectable; it simply carries no `sub` for an anonymous caller.
    [WolverineGet("/coupons/{code}/validate")]
    public static async Task<CouponValidation> Get(string code, ClaimsPrincipal user, IQuerySession session)
    {
        // Read `sub` DIRECTLY rather than through user.CustomerId() (Auth/CustomerIdentity.cs). That helper
        // THROWS on an absent claim — correct behind [Authorize], where a missing `sub` means a misconfigured
        // issuer, and wrong here, where anonymous is the normal case this endpoint must serve. The divergence
        // is deliberate (design.md decision 3); do not "fix" it into the throwing helper.
        var customerId = user.FindFirst("sub")?.Value;

        // Resolve the definition the same way checkout does (PlaceOrder.cs) — by the human-facing code.
        var coupon = await session.Query<CouponView>().FirstOrDefaultAsync(c => c.Code == code);
        if (coupon is null)
        {
            // Unknown code — nothing to price, nothing to hold. "This code isn't valid."
            return new CouponValidation(code, CouponValidationStatus.Invalid, DiscountPercent: null);
        }

        // Slice 6.6 — the PERSONAL reason, checked BEFORE the crowd reason. Gated TWICE: the definition must
        // carry the per-customer policy AND the caller must be signed in. Either gate failing skips the view
        // load entirely, so a global-cap-only coupon and an anonymous caller cost exactly slice 6.2's queries
        // and take slice 6.2's path. Anonymous → we hold no identity, so we make no personal claim (never 401).
        if (coupon.OneRedemptionPerCustomer && !string.IsNullOrEmpty(customerId))
        {
            var perCustomer = await session.LoadAsync<CustomerCouponUsageView>(
                CustomerCouponUsageView.KeyFor(coupon.Id, customerId));

            if ((perCustomer?.NetCount ?? 0) >= 1)
            {
                // Ordered ahead of the cap check to MIRROR CHECKOUT exactly (PlaceOrder.RedeemWithDcbAsync
                // runs the per-customer existence check before the global-cap count check). The preview and
                // the authority must agree about WHY, not merely whether: "you already used this — try another
                // code" and "the crowd used them all — try again later" are different remedies, and blaming
                // the crowd for a personal refusal teaches the wrong one.
                //
                // Still ADVISORY. A release may return this slot before checkout, and the boundary re-decides
                // regardless — this never stops the customer carrying the code. Also FORWARD-ONLY: a redemption
                // predating the CustomerId event member is invisible here, so this may UNDER-warn (degrading to
                // exactly today's behavior) and can never wrongly accuse.
                return new CouponValidation(
                    code, CouponValidationStatus.AlreadyRedeemed, DiscountPercent: null);
            }
        }

        // The ADVISORY net count: an inline projection (immediately consistent, but still a projection that
        // could lag under async — it is inline here, so it is current). Absent document → no redemptions yet.
        // This is NOT the DCB boundary state; the authoritative count is only ever computed at checkout.
        var usage = await session.LoadAsync<CouponUsageView>(coupon.Id);
        var netCount = usage?.NetCount ?? 0;

        if (netCount >= coupon.Cap)
        {
            // Advisorily exhausted — a slot may free by cancellation before the customer checks out, so this
            // does not stop them carrying the code; checkout re-decides against the boundary. "No longer available."
            return new CouponValidation(code, CouponValidationStatus.Exhausted, DiscountPercent: null);
        }

        // Redeemable, advisorily. The storefront shows "-$X → new total" priced from DiscountPercent.
        return new CouponValidation(code, CouponValidationStatus.Valid, coupon.DiscountPercent);
    }
}
