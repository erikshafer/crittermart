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

// The discriminated advisory answer (endpoint-shape decision, design.md §1: settled with Erik over a
// RESTful-resource and a server-priced variant). Status maps one-to-one onto the three W2 UI states:
//   valid     → the code applies; DiscountPercent is set and the storefront prices the dollar amount
//               against the cart total it already holds (the % → $ math stays client-side; the server
//               never sees cart money here).
//   exhausted → a definition resolves but its advisory net count has reached the cap ("no longer available").
//   invalid   → no definition resolves for the code ("this code isn't valid").
// DiscountPercent is null for invalid/exhausted. A 200 in every case — checking a code is not an error.
public record CouponValidation(string Code, string Status, int? DiscountPercent);

public static class CouponValidationStatus
{
    public const string Valid = "valid";
    public const string Invalid = "invalid";
    public const string Exhausted = "exhausted";
}

public static class ValidateCouponEndpoint
{
    [WolverineGet("/coupons/{code}/validate")]
    public static async Task<CouponValidation> Get(string code, IQuerySession session)
    {
        // Resolve the definition the same way checkout does (PlaceOrder.cs) — by the human-facing code.
        var coupon = await session.Query<CouponView>().FirstOrDefaultAsync(c => c.Code == code);
        if (coupon is null)
        {
            // Unknown code — nothing to price, nothing to hold. "This code isn't valid."
            return new CouponValidation(code, CouponValidationStatus.Invalid, DiscountPercent: null);
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
