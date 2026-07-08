using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Http;
using Wolverine.Persistence.Sagas;

namespace CritterMart.Identity.Customers;

// Opens (or updates) an EmailChange saga (Workshop 002 slice 5.5). The customer-facing HTTP command that
// both STARTS a saga (none open for the customer) and CONTINUES one (already open — updates PendingEmail,
// does not reschedule the deadline). [SagaIdentity] points the saga at CustomerId, since it is not named
// EmailChangeId/SagaId/Id — load-bearing for message-bus dispatch (EmailChange.StartOrHandle), not for the
// HTTP route below, which dispatches by explicit IMessageBus.InvokeAsync instead of saga-identity routing.
public record RequestEmailChange([property: SagaIdentity] string CustomerId, string NewEmail);

public static class RequestEmailChangeEndpoint
{
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // Guards mirroring RegisterCustomer.ValidateAsync's railway idiom. Static and self-contained — no saga
    // instance exists yet on the open path, and the re-request path only needs the incoming command.
    public static async Task<ProblemDetails> ValidateAsync(RequestEmailChange command, CustomerDbContext db)
    {
        var customerExists = await db.Customers.AnyAsync(c => c.Id == command.CustomerId);
        if (!customerExists)
        {
            return new ProblemDetails
            {
                Title = "CustomerNotFound",
                Detail = $"No customer with id '{command.CustomerId}' is registered.",
                Status = StatusCodes.Status404NotFound
            };
        }

        var newEmail = Normalize(command.NewEmail);
        var emailTaken = await db.Customers.AnyAsync(c => c.Email == newEmail && c.Id != command.CustomerId);

        return emailTaken
            ? new ProblemDetails
            {
                Title = "EmailAlreadyRegistered",
                Detail = $"A customer with email '{newEmail}' is already registered.",
                Status = StatusCodes.Status409Conflict
            }
            : WolverineContinue.NoProblems;
    }

    // Dispatches into EmailChange.StartOrHandle via IMessageBus.InvokeAsync — synchronous, in-process
    // (confirmed against ctx7 guide/messaging/message-bus.md). A SEPARATE class from the saga itself:
    // putting [WolverinePost] directly on a saga instance method alongside another such method for a
    // different message type (the original design) made Wolverine's HTTP chain builder throw
    // UnResolvableVariableException, conflating the two chains' dependencies. This composition — plain
    // static endpoint, guard, then InvokeAsync — is verified working by the integration tests.
    [WolverinePost("/customers/{customerId}/email-change")]
    public static async Task Post(RequestEmailChange command, IMessageBus bus)
    {
        await bus.InvokeAsync(command);
    }
}
