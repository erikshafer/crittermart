using CritterMart.Orders.Auth;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer removes an item from their open cart (Workshop 001 slice 3.2). Identity is the authenticated
// JWT `sub` claim (ADR 023, slice 5.10), dev-only X-Customer-Id header fallback; the {sku} rides the route
// and there is no request body, which makes this the project's first DELETE endpoint. As with AddToCart, the
// customer edits *their* cart; resolving which Cart stream that is, is the server's business.
public static class RemoveCartItemEndpoint
{
    [WolverineDelete("/carts/mine/items/{sku}")]
    public static async Task<IResult> Delete(
        HttpContext http,
        [FromHeader(Name = "X-Customer-Id")] string? customerIdHeader, string sku, IDocumentSession session)
    {
        // A bad/expired token → 401; no identity at all → 400, mirroring ViewMyCart and the cart's other
        // commands. CustomerIdentity.TryResolve prefers the token's `sub`, dev-only header fallback.
        if (!CustomerIdentity.TryResolve(http, customerIdHeader, out var customerId, out var failure))
        {
            return failure ?? Results.BadRequest("X-Customer-Id header is required.");
        }

        // Resolve the customer's open cart — the same indexed Cart query AddToCart and
        // PlaceOrder use. No open cart → nothing to edit.
        var open = await session.Query<Cart>()
            .Where(c => c.CustomerId == customerId && c.IsOpen)
            .FirstOrDefaultAsync();

        if (open is null)
        {
            return Results.Problem(
                title: "NoOpenCart",
                detail: $"Customer '{customerId}' has no open cart to edit.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Guard against the projected view: the SKU must be a line in the cart (Workshop § 6.1
        // slice 3.2 failure path — CartItemNotPresent). Lines are SKU-keyed, so Any() is exact.
        if (open.Lines.All(l => l.Sku != sku))
        {
            return Results.Problem(
                title: "CartItemNotPresent",
                detail: $"Cart for customer '{customerId}' has no line for SKU '{sku}'.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Append the removal fact; the inline Cart + CartView projections drop the line at commit.
        // Removing the last line leaves the cart open and empty (design.md decision 5) — the
        // CartEmpty guard in PlaceOrder protects checkout from here on.
        var stream = await session.Events.FetchForWriting<Cart>(open.Id);
        stream.AppendOne(new CartItemRemoved(sku));

        return Results.NoContent();
    }
}
