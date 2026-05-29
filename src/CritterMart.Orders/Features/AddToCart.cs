using CritterMart.Orders.Cart;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer adds an item to their cart (Workshop 001 slice 3.1). customerId comes from
// the route; the product name + price are snapshotted on the command (no Catalog read).
public record AddToCart(string Sku, int Quantity, ProductSnapshot ProductSnapshot);

// The cartId handed back so the caller can read the cart at GET /carts/{cartId}.
public record AddToCartResponse(string CartId);

public static class AddToCartEndpoint
{
    [WolverinePost("/carts/{customerId}/items")]
    public static async Task<IResult> Post(string customerId, AddToCart command, IDocumentSession session)
    {
        // The Cart stream is keyed by cartId, but the command knows only the customer, so
        // resolve the customer's open cart first (design.md decision 2). The partial unique
        // index on CartView.CustomerId (scoped to open carts) backstops a concurrent create.
        var open = await session.Query<CartView>()
            .Where(v => v.CustomerId == customerId && v.IsOpen)
            .FirstOrDefaultAsync();

        string cartId;
        var itemAdded = new CartItemAdded(command.Sku, command.Quantity, command.ProductSnapshot);

        if (open is null)
        {
            // First add: start a new Cart stream — CartCreated then the first CartItemAdded.
            cartId = Guid.NewGuid().ToString();
            session.Events.StartStream<CartView>(cartId, new CartCreated(cartId, customerId), itemAdded);
        }
        else
        {
            // Subsequent add: append onto the same open cart.
            cartId = open.Id;
            var stream = await session.Events.FetchForWriting<CartView>(cartId);
            stream.AppendOne(itemAdded);
        }

        // AutoApplyTransactions commits; the inline CartView projection updates the lines.
        return Results.Created($"/carts/{cartId}", new AddToCartResponse(cartId));
    }
}

public static class CartEndpoint
{
    [WolverineGet("/carts/{cartId}")]
    public static async Task<IResult> Get(string cartId, IQuerySession session)
    {
        var view = await session.LoadAsync<CartView>(cartId);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
