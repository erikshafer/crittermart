namespace CritterMart.Identity.Customers;

// The Customer registry record — a plain EF Core entity, NOT an event-sourced aggregate. This is the
// whole point of the spike: Identity persists CURRENT STATE in a row, while Catalog / Inventory /
// Orders persist EVENTS. There is no stream, no projection, no fold — just a mutable row that EF
// Core's change tracker writes.
//
// Id is a string (not a Guid) to line up with the storefront's X-Customer-Id seam (ADR 009): a
// future lookup could resolve that header against this table without a type change. For round one it
// is a server-minted opaque id and nothing reads it back through the seam yet.
public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; set; }
}
