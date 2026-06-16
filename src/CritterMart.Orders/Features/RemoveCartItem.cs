using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer removes an item from their open cart (Workshop 001 slice 3.2). Both identifiers
// ride the route — there is no request body, which makes this the project's first DELETE
// endpoint. As with AddToCart, the customer edits *their* cart; resolving which Cart stream
// that is, is the server's business (design.md decision 3).
public static class RemoveCartItemEndpoint
{
    [WolverineDelete("/carts/{customerId}/items/{sku}")]
    public static async Task<IResult> Delete(string customerId, string sku, IDocumentSession session)
    {
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
