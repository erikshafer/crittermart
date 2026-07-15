using System.Security.Claims;
using CritterMart.Orders.Auth;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// Slice 3.5 — "View my open cart" (Workshop 001 § 6, round-two view slice). The cart's WRITE side
// is customer-keyed: every command resolves the customer's one open cart and never carries a cartId
// (the cartId is the server's business). But the round-one cart READ is GET /carts/{cartId}, and the
// storefront only learns a cartId from an AddToCart response. On a COLD load the SPA holds only the
// stubbed customer id (ADR 009) and no cartId, so it cannot render the cart-review screen (wireframe
// W2). This exposes the SAME open-cart resolution AddToCart already runs (AddToCart.cs:31) as a read,
// closing the pre-frontend audit's blocking Gap #1. No new event, projection, or index.
public static class ViewMyCartEndpoint
{
    // Identity is the authenticated JWT `sub` claim, guaranteed by [Authorize] (ADR 023 hard cutover):
    // a missing/bad/expired token is a 401 decided by JwtBearer before the handler — kept distinct from
    // the 404 that means "this customer has no open cart". A literal route segment, so /carts/mine wins
    // over /carts/{cartId} by ASP.NET Core route precedence — the same precedence that already lets
    // /carts/awaiting-activity win.
    [Authorize]
    [WolverineGet("/carts/mine")]
    public static async Task<IResult> Get(ClaimsPrincipal user, IQuerySession session)
    {
        var customerId = user.CustomerId();

        // The partial-unique open-cart index (Program.cs:74) guarantees at most one open cart per
        // customer, so FirstOrDefault is "the one." A checked-out (4.1) or abandoned (3.4) cart has
        // IsOpen=false and never resolves here — the customer is free to start a fresh cart.
        var view = await session.Query<CartView>()
            .Where(v => v.CustomerId == customerId && v.IsOpen)
            .FirstOrDefaultAsync();

        // No open cart is a domain state, not an error: the storefront renders an empty cart and the
        // next AddToCart (3.1) starts a fresh stream.
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
