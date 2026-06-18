using CritterMart.Identity.Customers;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace CritterMart.Identity.Features;

public record CustomerResponse(string Id, string Email, string DisplayName, DateTimeOffset RegisteredAt);

public static class GetCustomerEndpoint
{
    // The READ side: a straight primary-key lookup against the row — no projection, no read model,
    // because the row IS the read model. FindAsync hits the identity-schema `customers` table.
    [WolverineGet("/customers/{id}")]
    public static async Task<IResult> Get(string id, IdentityDbContext db)
    {
        var customer = await db.Customers.FindAsync(id);
        return customer is null
            ? Results.NotFound()
            : Results.Ok(new CustomerResponse(
                customer.Id, customer.Email, customer.DisplayName, customer.RegisteredAt));
    }
}
