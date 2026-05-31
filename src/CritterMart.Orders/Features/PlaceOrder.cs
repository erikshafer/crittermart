using CritterMart.Orders.Cart;
using CritterMart.Orders.Order;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer checks out their open cart, turning it into an order (Workshop 001 slice 4.1).
// customerId rides on the command; the cart's snapshotted lines + computed total are frozen
// onto a new Order stream. This is the project's first multi-stream atomic write.
public record PlaceOrder(string CustomerId);

// The orderId handed back so the caller can read the order at GET /orders/{orderId}.
public record PlaceOrderResponse(string OrderId);

public static class PlaceOrderEndpoint
{
    [WolverinePost("/orders")]
    public static async Task<IResult> Post(PlaceOrder command, IDocumentSession session)
    {
        // Resolve the customer's open cart — the same indexed CartView query AddToCart uses.
        // A cart that was already checked out has IsOpen=false, so a repeat PlaceOrder finds no
        // open cart and is rejected here: the workshop's "cart already checked out" failure
        // path, handled for free by open-cart resolution (no separate guard needed).
        var cart = await session.Query<CartView>()
            .Where(v => v.CustomerId == command.CustomerId && v.IsOpen)
            .FirstOrDefaultAsync();

        if (cart is null)
        {
            return Results.Problem(
                title: "NoOpenCart",
                detail: $"Customer '{command.CustomerId}' has no open cart to place.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Defensive guard for the workshop's CartEmpty path. Unreachable in 4.1 (a cart is
        // created with its first line and remove-item is 3.2), but it guards the invariant the
        // moment 3.2 makes a lineless-but-open cart reachable.
        if (cart.Lines.Count == 0)
        {
            return Results.Problem(
                title: "CartEmpty",
                detail: $"Customer '{command.CustomerId}' has an empty cart.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var orderId = Guid.NewGuid().ToString();
        var items = cart.Lines
            .Select(l => new OrderLine(l.Sku, l.Quantity, l.Name, l.Price))
            .ToList();
        var total = items.Sum(i => i.Quantity * i.Price);

        // The multi-stream atomic write (slice 4.1's teaching beat): a new Order stream AND the
        // cart's terminal CartCheckedOut, committed together by AutoApplyTransactions in ONE
        // transaction. The inline OrderStatusView + CartView projections both update.
        session.Events.StartStream<OrderStatusView>(
            orderId, new OrderPlaced(orderId, command.CustomerId, items, total));

        var cartStream = await session.Events.FetchForWriting<CartView>(cart.Id);
        cartStream.AppendOne(new CartCheckedOut(orderId));

        return Results.Created($"/orders/{orderId}", new PlaceOrderResponse(orderId));
    }
}

public static class OrderEndpoint
{
    [WolverineGet("/orders/{orderId}")]
    public static async Task<IResult> Get(string orderId, IQuerySession session)
    {
        var view = await session.LoadAsync<OrderStatusView>(orderId);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
