using CritterMart.Orders.Shopping;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// The Customer adds an item to their cart (Workshop 001 slice 3.1). Identity arrives via the
// X-Customer-Id header (ADR 009 seam — harmonized with the cart read GET /carts/mine); the product
// name + price are snapshotted on the command (no Catalog read).
public record AddToCart(string Sku, int Quantity, ProductSnapshot ProductSnapshot);

// The cartId handed back so the caller can read the cart at GET /carts/{cartId}.
public record AddToCartResponse(string CartId);

public static class AddToCartEndpoint
{
    // Boundary guard (validation-boundary): the cart never reads the Catalog, so the ProductSnapshot the
    // storefront composed (ProductSnapshot.cs) is the cart line's ONLY source of product truth. A command
    // that carries no usable snapshot has nothing to build a line from — that is malformed *input*, not a
    // domain-state conflict (contrast ChangeCartItemQuantity's CartItemNotPresent). Wolverine runs Validate
    // before Post and short-circuits on a populated ProblemDetails, so a malformed command never reaches the
    // handler and never appends an event — and an appended event can't be un-appended. Before this guard a
    // null snapshot survived into CartItemAdded and only surfaced as a 500 NRE deep in the shared
    // CartLines.Add fold (CartLine.cs:19). Mirrors PublishProduct's ValidateAsync ProblemDetails guard;
    // synchronous because a snapshot-shape check needs no I/O. The 400 + application/problem+json is
    // auto-reflected into the endpoint's OpenAPI metadata by Wolverine.
    public static ProblemDetails Validate(AddToCart command)
    {
        if (command.ProductSnapshot is null)
        {
            return new ProblemDetails
            {
                Title = "MissingProductSnapshot",
                Detail = "AddToCart requires a productSnapshot — the name and price the storefront composed from the Catalog.",
                Status = StatusCodes.Status400BadRequest
            };
        }

        if (string.IsNullOrWhiteSpace(command.ProductSnapshot.Name))
        {
            return new ProblemDetails
            {
                Title = "MissingProductName",
                Detail = "The productSnapshot must carry a non-empty product name.",
                Status = StatusCodes.Status400BadRequest
            };
        }

        if (command.ProductSnapshot.Price < 0)
        {
            return new ProblemDetails
            {
                Title = "NegativeProductPrice",
                Detail = $"The productSnapshot price must be non-negative; got {command.ProductSnapshot.Price}.",
                Status = StatusCodes.Status400BadRequest
            };
        }

        return WolverineContinue.NoProblems;
    }

    // Returns the HTTP response AND a cascaded output: a SCHEDULED CartActivityTimeout self-message
    // (slice 3.4), set only when this add CREATES the cart — the Bruun temporal automation's
    // starting gun on the cart side, mirroring how PlaceOrder schedules OrderPaymentTimeout. Under
    // fire-and-check (design.md Decision 1) one scheduled timeout per cart suffices: subsequent
    // adds and edits just append events whose timestamps ARE the activity record the fired timeout
    // checks — so they cascade null, which Wolverine skips.
    [WolverinePost("/carts/mine/items")]
    public static async Task<(IResult, DeliveryMessage<CartActivityTimeout>?)> Post(
        [FromHeader(Name = "X-Customer-Id")] string? customerId,
        AddToCart command, IDocumentSession session, CartActivityDeadline deadline)
    {
        // Identity rides in the X-Customer-Id header — the ADR 009 useCurrentCustomer seam, the same
        // transport the cart READ (GET /carts/mine) already uses. A missing/blank header is a malformed
        // request (no identity to resolve a cart) → 400, mirroring ViewMyCart. The header is the round-one
        // stand-in for the authenticated claim Polecat will provide; the route no longer carries identity.
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return (Results.BadRequest("X-Customer-Id header is required."), null);
        }

        // The Cart stream is keyed by cartId, but the command knows only the customer, so
        // resolve the customer's open cart first (design.md decision 2). The partial unique
        // index on Cart.CustomerId (scoped to open carts) backstops a concurrent create.
        var open = await session.Query<Cart>()
            .Where(c => c.CustomerId == customerId && c.IsOpen)
            .FirstOrDefaultAsync();

        string cartId;
        var itemAdded = new CartItemAdded(command.Sku, command.Quantity, command.ProductSnapshot);
        DeliveryMessage<CartActivityTimeout>? activityTimeout = null;

        if (open is null)
        {
            // First add: start a new Cart stream — CartCreated then the first CartItemAdded —
            // and schedule the cart's inactivity deadline (slice 3.4). The schedule is durable
            // (UseDurableLocalQueues), so the deadline survives a service restart.
            cartId = Guid.NewGuid().ToString();
            session.Events.StartStream<Cart>(cartId, new CartCreated(cartId, customerId), itemAdded);
            activityTimeout = new CartActivityTimeout(cartId).DelayedFor(deadline.Duration);
        }
        else
        {
            // Subsequent add: append onto the same open cart. No new schedule — the pending
            // timeout re-aims itself off this event's timestamp when it fires (fire-and-check).
            cartId = open.Id;
            var stream = await session.Events.FetchForWriting<Cart>(cartId);
            stream.AppendOne(itemAdded);
        }

        // AutoApplyTransactions commits; the inline Cart aggregate + CartView read projection both update.
        return (Results.Created($"/carts/{cartId}", new AddToCartResponse(cartId)), activityTimeout);
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

    // The cart-side Bruun todo-list (slice 3.4): every open cart the abandonment automation is
    // watching, soonest deadline first — the mirror of GET /orders/awaiting-payment. Rows appear
    // at cart creation and vanish on either terminal event (the CartsAwaitingActivity projection's
    // conditional deletes). A literal route segment, so it wins over /carts/{cartId} by ASP.NET
    // Core route precedence.
    [WolverineGet("/carts/awaiting-activity")]
    public static async Task<IResult> GetAwaitingActivity(IQuerySession session)
    {
        var rows = await session.Query<CartAwaitingActivity>()
            .OrderBy(x => x.Deadline)
            .ToListAsync();
        return Results.Ok(rows);
    }
}
