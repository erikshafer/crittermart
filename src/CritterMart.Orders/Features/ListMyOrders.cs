using System.Security.Claims;
using CritterMart.Orders.Auth;
using CritterMart.Orders.Customers;
using CritterMart.Orders.Ordering;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Orders.Features;

// "My Orders" — the customer-keyed order list (workshop § 5.1 Gap #3, closed; OpenSpec change list-my-orders).
// W4 tracks ONE order by id (GET /orders/{orderId}); this is the list across all of a customer's orders. It is
// NOT a new projection: every placed order already has an inline OrderStatusView document carrying customerId,
// status, total, and placedAt (slice 025), so the list is a filtered query over those existing documents — the
// composition of two in-repo precedents, GET /products (a list over Product documents, BrowseProducts.cs) and
// GET /carts/mine (a customer-keyed read over CartView, ViewMyCart.cs). The full OrderStatusView shape is
// returned per order, so the list row and the W4 track screen bind one contract (no separate summary DTO — the
// reason BrowseProducts projects a DTO is an id→sku rename this read does not need).
public static class ListMyOrdersEndpoint
{
    // Identity is the authenticated JWT `sub` claim, guaranteed by [Authorize] (ADR 023 hard cutover;
    // this realizes the ADR 009 seam's promise — the claim replaced the header with call sites unchanged).
    // This mirrors GET /carts/mine exactly (ViewMyCart.cs) and consciously supersedes the workshop Gap #3
    // sketch GET /orders?customerId= (a modeling-time query-string form): keeping identity in the token,
    // not the URL, means a customer can't read another's orders by editing a query param. A literal route
    // segment, so /orders/mine wins over /orders/{orderId} by ASP.NET Core route precedence — the same
    // precedence that already lets /orders/awaiting-payment win.
    [Authorize]
    [WolverineGet("/orders/mine")]
    public static async Task<IResult> Get(ClaimsPrincipal user, IQuerySession session)
    {
        var customerId = user.CustomerId();

        // The customer-keyed query over the existing OrderStatusView documents (served by the non-unique
        // OrderStatusView.CustomerId index, Program.cs). Ordered newest-first by placedAt (the genesis
        // OrderPlaced append timestamp, present on every order from slice 025) — the natural reading order for
        // an order-history screen. Every lifecycle state is included (active and terminal alike); a cancelled
        // order carries its cancelReason. A customer with no orders resolves to an empty list (200 []), a
        // domain-empty state, NOT a 404 — unlike GET /carts/mine, where 404 means "no open cart."
        var orders = await session.Query<OrderStatusView>()
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.PlacedAt)
            .ToListAsync();

        // Enrich with customer display name (slice 5.3): one LocalCustomerView load for the whole list
        // (all orders share the same customerId from the token — one scan, not N). Degrades to null
        // when the local model is absent (PL eventually consistent). No call to Identity.
        var customer = await session.LoadAsync<LocalCustomerView>(customerId);
        var enriched = orders.Select(o => EnrichedOrderView.From(o, customer?.DisplayName)).ToList();

        return Results.Ok(enriched);
    }
}
