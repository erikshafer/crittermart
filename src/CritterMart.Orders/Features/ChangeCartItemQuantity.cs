using CritterMart.Orders.Auth;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer changes the quantity of an item in their open cart (Workshop 001 slice 3.3).
// Identity is the authenticated JWT `sub` claim (ADR 023, slice 5.10), dev-only X-Customer-Id header
// fallback; the {sku} stays on the route and the new absolute quantity rides the body. The {sku}-on-the-route
// + body shape still mirrors Catalog's change-price (POST /products/{sku}/price) — a command-shaped
// POST to the thing being changed.
public record ChangeCartItemQuantity(int NewQuantity);

public static class ChangeCartItemQuantityEndpoint
{
    [WolverinePost("/carts/mine/items/{sku}/quantity")]
    public static async Task<IResult> Post(
        HttpContext http,
        [FromHeader(Name = "X-Customer-Id")] string? customerIdHeader, string sku,
        ChangeCartItemQuantity command, IDocumentSession session)
    {
        // A bad/expired token → 401; no identity at all → 400, mirroring ViewMyCart and the cart's other
        // commands. CustomerIdentity.TryResolve prefers the token's `sub`, dev-only header fallback.
        if (!CustomerIdentity.TryResolve(http, customerIdHeader, out var customerId, out var failure))
        {
            return failure ?? Results.BadRequest("X-Customer-Id header is required.");
        }

        // Malformed input, not a state conflict: zero or negative is never a valid quantity.
        // Removing an item is its own command (Workshop § 6.1 slice 3.3 failure path).
        if (command.NewQuantity <= 0)
        {
            return Results.Problem(
                title: "NonPositiveQuantity",
                detail: $"Quantity must be positive; got {command.NewQuantity}. Use remove-item for zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Resolve the customer's open cart — the same indexed Cart query AddToCart uses.
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

        // Guard against the projected view: changing a quantity presumes the line exists.
        // (Mirrors slice 3.2's CartItemNotPresent — design.md faithfulness note 2.)
        if (open.Lines.All(l => l.Sku != sku))
        {
            return Results.Problem(
                title: "CartItemNotPresent",
                detail: $"Cart for customer '{customerId}' has no line for SKU '{sku}'.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Append the change; the inline Cart + CartView projections rewrite the line's quantity at
        // commit. The snapshotted name/price are untouched — only "how many" changes.
        var stream = await session.Events.FetchForWriting<Cart>(open.Id);
        stream.AppendOne(new CartItemQuantityChanged(sku, command.NewQuantity));

        return Results.NoContent();
    }
}
