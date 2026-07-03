using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Http;
using Wolverine.Persistence.Sagas;

namespace CritterMart.Identity.Customers;

// Confirms a pending email change within the window (Workshop 002 slice 5.6). Continues an already-open
// EmailChange saga; a saga that cannot be found (window expired, already confirmed) is a silent no-op via
// EmailChange.NotFound. [SagaIdentity] correlates it on CustomerId for message-bus dispatch.
public record ConfirmEmailChange([property: SagaIdentity] string CustomerId);

public static class ConfirmEmailChangeEndpoint
{
    // The conflict guard (slice 5.6's stays-open rule) needs the saga's OWN PendingEmail, which an
    // instance-scoped Validate on the saga class can't safely provide here — see EmailChange.cs's remarks
    // on why the HTTP surface lives in a separate class. Loads the EmailChange row directly via EF Core
    // instead. A missing row (window already expired, or already confirmed) is NOT treated as an error here
    // — it falls through to EmailChange.NotFound's silent no-op, which is the more faithful reading of the
    // spec's "silent no-op" than surfacing a 404 (verified empirically: IMessageBus.InvokeAsync does not
    // translate a saga's NotFound into an HTTP 404 — Post below simply completes and returns 200).
    public static async Task<ProblemDetails> ValidateAsync(ConfirmEmailChange command, IdentityDbContext db)
    {
        var saga = await db.EmailChanges.FindAsync(command.CustomerId);
        if (saga is null)
        {
            return WolverineContinue.NoProblems;
        }

        var conflict = await db.Customers.AnyAsync(c => c.Email == saga.PendingEmail && c.Id != command.CustomerId);

        return conflict
            ? new ProblemDetails
            {
                Title = "EmailChangeConflict",
                Detail = $"The email '{saga.PendingEmail}' has since been registered to another customer.",
                Status = StatusCodes.Status409Conflict
            }
            : WolverineContinue.NoProblems;
    }

    [WolverinePost("/customers/{customerId}/confirm-email-change")]
    public static async Task Post(ConfirmEmailChange command, IMessageBus bus)
    {
        await bus.InvokeAsync(command);
    }
}
