using CritterMart.Orders.Order;
using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine;
using Wolverine.Http;
using Contracts = CritterMart.Contracts;

namespace CritterMart.Orders.Features;

// The Customer checks out their open cart, turning it into an order (Workshop 001 slice 4.1).
// customerId rides on the command; the cart's snapshotted lines + computed total are frozen
// onto a new Order stream. This is the project's first multi-stream atomic write.
public record PlaceOrder(string CustomerId);

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
    [WolverinePost("/orders")]
    public static async Task<(IResult, Contracts.ReserveStock?, DeliveryMessage<OrderPaymentTimeout>?)> Post(
        PlaceOrder command, IDocumentSession session, PaymentDeadline deadline)
    {
        // Resolve the customer's open cart — the same indexed Cart query AddToCart uses.
        // A cart that was already checked out has IsOpen=false, so a repeat PlaceOrder finds no
        // open cart and is rejected here: the workshop's "cart already checked out" failure
        // path, handled for free by open-cart resolution (no separate guard needed).
        var cart = await session.Query<Cart>()
            .Where(c => c.CustomerId == command.CustomerId && c.IsOpen)
            .FirstOrDefaultAsync();

        if (cart is null)
        {
            return (Results.Problem(
                title: "NoOpenCart",
                detail: $"Customer '{command.CustomerId}' has no open cart to place.",
                statusCode: StatusCodes.Status409Conflict), null, null);
        }

        // Defensive guard for the workshop's CartEmpty path. Unreachable in 4.1 (a cart is
        // created with its first line and remove-item is 3.2), but it guards the invariant the
        // moment 3.2 makes a lineless-but-open cart reachable.
        if (cart.Lines.Count == 0)
        {
            return (Results.Problem(
                title: "CartEmpty",
                detail: $"Customer '{command.CustomerId}' has an empty cart.",
                statusCode: StatusCodes.Status409Conflict), null, null);
        }

        var orderId = Guid.NewGuid().ToString();
        var items = cart.Lines
            .Select(l => new OrderLine(l.Sku, l.Quantity, l.Name, l.Price))
            .ToList();
        var total = items.Sum(i => i.Quantity * i.Price);

        // The multi-stream atomic write (slice 4.1's teaching beat): a new Order stream AND the
        // cart's terminal CartCheckedOut, committed together by AutoApplyTransactions in ONE
        // transaction. The inline OrderStatusView, Cart, and CartView projections all update.
        session.Events.StartStream<OrderStatusView>(
            orderId, new OrderPlaced(orderId, command.CustomerId, items, total));

        var cartStream = await session.Events.FetchForWriting<Cart>(cart.Id);
        cartStream.AppendOne(new CartCheckedOut(orderId));

        // Cascade the whole order's reservation request to Inventory over RabbitMQ (slice 4.2,
        // design.md decision 2): one message carrying every line, reserved all-or-nothing.
        var reserveStock = new Contracts.ReserveStock(
            orderId,
            items.Select(i => new Contracts.ReserveStockLine(i.Sku, i.Quantity)).ToList());

        // Schedule the payment deadline (slice 4.7): a self-message delivered back to this service
        // after the configured timeout. If the order has settled by then, the timeout handler's
        // terminal guard makes it a no-op; if not, the order is cancelled and its stock released.
        var paymentTimeout = new OrderPaymentTimeout(orderId).DelayedFor(deadline.Duration);

        return (Results.Created($"/orders/{orderId}", new PlaceOrderResponse(orderId)), reserveStock, paymentTimeout);
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

    // The Bruun todo-list (slice 4.7): every order still awaiting its terminal state, soonest
    // deadline first. Rows appear when an order is placed and vanish when it confirms or cancels
    // (the OrdersAwaitingPayment projection's conditional delete) — so this list is always the
    // live set of orders the payment-timeout automation is watching. A literal route segment, so
    // it wins over /orders/{orderId} by ASP.NET Core route precedence.
    [WolverineGet("/orders/awaiting-payment")]
    public static async Task<IResult> GetAwaitingPayment(IQuerySession session)
    {
        var rows = await session.Query<OrderAwaitingPayment>()
            .OrderBy(x => x.Deadline)
            .ToListAsync();
        return Results.Ok(rows);
    }
}
