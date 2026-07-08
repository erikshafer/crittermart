using CritterMart.Orders.Auth;
using CritterMart.Orders.Customers;
using CritterMart.Orders.Ordering;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    // Identity rides in the X-Customer-Id header — the round-one stand-in for an authenticated claim behind
    // the useCurrentCustomer seam (ADR 009; the Polecat promotion swaps the header for a claim with call sites
    // unchanged). This mirrors GET /carts/mine exactly (ViewMyCart.cs:23) and consciously supersedes the
    // workshop Gap #3 sketch GET /orders?customerId= (a modeling-time query-string form): keeping identity in
    // the header, not the URL, means a customer can't read another's orders by editing a query param. A literal
    // route segment, so /orders/mine wins over /orders/{orderId} by ASP.NET Core route precedence — the same
    // precedence that already lets /orders/awaiting-payment win.
    [WolverineGet("/orders/mine")]
    public static async Task<IResult> Get(
        HttpContext http,
        [FromHeader(Name = "X-Customer-Id")] string? customerIdHeader, IQuerySession session)
    {
        // A bad/expired token → 401; no identity at all → 400, kept distinct from the empty-list case below
        // (a customer with no orders). CustomerIdentity.TryResolve prefers the token's `sub`, dev-only header
        // fallback. Mirrors ViewMyCart.
        if (!CustomerIdentity.TryResolve(http, customerIdHeader, out var customerId, out var failure))
        {
            return failure ?? Results.BadRequest("X-Customer-Id header is required.");
        }

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
        // (all orders share the same customerId from the header — one scan, not N). Degrades to null
        // when the local model is absent (PL eventually consistent). No call to Identity.
        var customer = await session.LoadAsync<LocalCustomerView>(customerId);
        var enriched = orders.Select(o => EnrichedOrderView.From(o, customer?.DisplayName)).ToList();

        return Results.Ok(enriched);
    }
}
