using CritterMart.Contracts;
using CritterMart.Identity.Customers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace CritterMart.Identity.Features;

// Register a new customer into the EF Core registry. A plain CRUD insert guarded by one uniqueness
// rule (email) — the contrast with the event-sourced services' FetchForWriting + append is the
// teaching point, and the guard is Catalog's PublishProduct.ValidateAsync idiom expressed over a
// DbContext instead of an IDocumentSession.
//
// The optional Id field (slice 5.4 / seeder): when the caller supplies an explicit id the server
// uses it verbatim; when absent (the default) the server mints a UUID. This lets the seeder
// register "customer-demo" with a deterministic id that matches the SPA's X-Customer-Id stub
// without requiring every caller to supply an id.
public record RegisterCustomer(string Email, string DisplayName, string? Id = null);

public record RegisterCustomerResponse(string Id);

public static class RegisterCustomerEndpoint
{
    // Normalize the email ONCE — trim + lowercase — and reuse the result for the uniqueness check, the
    // stored row, and the published event alike. A registry whose guard `Ada@` could slip past `ada@`
    // would not be a uniqueness guard at all; normalizing is what makes it honest. The DB unique index
    // on the email column (IdentityDbContext) stores this same normalized value.
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // Railway-style guard mirroring PublishProduct.ValidateAsync: a duplicate email is an expected,
    // modeled outcome — return ProblemDetails (flow control), never throw. Because it short-circuits
    // before the handler, the duplicate path inserts no row and cascades no event, so the failure is
    // idempotent. This application-level check is racy on its own (two concurrent registrations could
    // both pass it before either commits); the unique index on email is the true backstop that rejects
    // the second insert. Catalog needs no such index — a product's SKU IS its Marten document id, so the
    // primary key enforces uniqueness for free; Identity keys on email, NOT the id, so the index earns it.
    public static async Task<ProblemDetails> ValidateAsync(RegisterCustomer command, IdentityDbContext db)
    {
        var email = Normalize(command.Email);
        var alreadyRegistered = await db.Customers.AnyAsync(c => c.Email == email);

        return alreadyRegistered
            ? new ProblemDetails
            {
                Title = "CustomerAlreadyRegistered",
                Detail = $"A customer with email '{email}' is already registered.",
                Status = StatusCodes.Status409Conflict
            }
            : WolverineContinue.NoProblems;
    }

    // Returns the HTTP response AND a cascaded CustomerRegistered — the same tuple-return idiom Orders'
    // PlaceOrder uses to cascade ReserveStock. AutoApplyTransactions commits the Customers.Add insert;
    // in the SAME transaction Wolverine enrolls CustomerRegistered in the EF Core outbox and publishes
    // it only after the commit succeeds. Note what is NOT here: no session.SaveChangesAsync() — the
    // transactional middleware owns the commit, exactly as the Marten endpoints never commit by hand.
    //
    // CustomerRegistered now lives in CritterMart.Contracts (the shared Published-Language type) — this
    // is when it graduated from Identity-internal (slice 5.4), because Orders now has a handler for it.
    [WolverinePost("/customers")]
    public static (IResult, CustomerRegistered) Post(RegisterCustomer command, IdentityDbContext db)
    {
        var customer = new Customer
        {
            Id = command.Id ?? Guid.NewGuid().ToString(),
            Email = Normalize(command.Email),
            DisplayName = command.DisplayName,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        return (
            Results.Created($"/customers/{customer.Id}", new RegisterCustomerResponse(customer.Id)),
            new CustomerRegistered(customer.Id, customer.Email, customer.DisplayName));
    }
}
