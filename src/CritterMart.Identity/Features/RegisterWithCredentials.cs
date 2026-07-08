using CritterMart.Contracts;
using CritterMart.Identity.Customers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace CritterMart.Identity.Features;

// Register a customer WITH credentials (Workshop 002 slice 5.8, ADR 023) — the credentialed evolution of
// slice 5.1's RegisterCustomer. Creates an ASP.NET Core Identity user (password hashed) AND the registry
// Customer row, keyed by the SAME string id, and publishes CustomerRegistered — all in ONE transaction.
//
// The spike's passwordless POST /customers (RegisterCustomer) is KEPT for admin/seeder-provisioned customers
// (Workshop 002 § 8 Q14 → layer, not replace); a self-registering shopper comes through /register here.
public record RegisterWithCredentials(string Email, string DisplayName, string Password);

public record RegisterWithCredentialsResponse(string Id);

public static class RegisterWithCredentialsEndpoint
{
    // Same normalization + duplicate-email railway guard as RegisterCustomer.ValidateAsync (reused, not
    // reinvented) — a duplicate email is an expected, modeled 409, returned before the handler so no user,
    // no row, no event. Racy on its own; ux_customers_email is the DB backstop, exactly as for RegisterCustomer.
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static async Task<ProblemDetails> ValidateAsync(RegisterWithCredentials command, CustomerDbContext db)
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

    // Returns the HTTP response AND a cascaded CustomerRegistered — the same tuple idiom as RegisterCustomer.
    // ALL-OR-NOTHING (spec 5.8): the Identity user, the Customer row, and the outbox message commit in ONE
    // Wolverine transaction. We do NOT call UserManager.CreateAsync — its EF UserStore has AutoSaveChanges=true
    // and would SaveChanges (commit the user) mid-handler, BEFORE Wolverine's AutoApplyTransactions commits the
    // row + enrolls the event, splitting the write in two. Instead we use UserManager's building blocks
    // (PasswordValidators for the policy check, PasswordHasher to hash, the normalizer for lookup keys) and
    // db.Users.Add the user alongside db.Customers.Add — so the transactional middleware commits all three
    // together and CustomerRegistered publishes only after that single commit succeeds. Password-policy failure
    // returns 400 (Identity's own messages) BEFORE any Add, so a weak password creates nothing.
    [WolverinePost("/register")]
    public static async Task<(IResult, CustomerRegistered?)> Post(
        RegisterWithCredentials command,
        CustomerDbContext db,
        UserManager<IdentityUser> users)
    {
        var email = Normalize(command.Email);
        var id = Guid.NewGuid().ToString();

        var user = new IdentityUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = users.KeyNormalizer.NormalizeName(email),
            Email = email,
            NormalizedEmail = users.KeyNormalizer.NormalizeEmail(email),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        // ASP.NET Core Identity's configured password policy (slice 5.8's weak-password 400). Run the
        // validators explicitly, since we bypass CreateAsync (see remarks). Any failure → 400 with the
        // Identity error descriptions, no writes.
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, command.Password);
            if (!result.Succeeded)
            {
                return (Results.Problem(
                    title: "PasswordRejected",
                    detail: string.Join(" ", result.Errors.Select(e => e.Description)),
                    statusCode: StatusCodes.Status400BadRequest), null);
            }
        }

        user.PasswordHash = users.PasswordHasher.HashPassword(user, command.Password);

        var customer = new Customer
        {
            Id = id,
            Email = email,
            DisplayName = command.DisplayName,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        db.Customers.Add(customer);

        return (
            Results.Created($"/customers/{id}", new RegisterWithCredentialsResponse(id)),
            new CustomerRegistered(id, email, command.DisplayName));
    }
}
