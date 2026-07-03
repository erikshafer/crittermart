using Wolverine;

namespace CritterMart.Identity.Customers;

// CritterMart's SECOND convention Wolverine.Saga (Workshop 002 slices 5.5-5.7), EF-Core-backed — the direct
// counterpart to Inventory's Marten-backed Replenishment (src/CritterMart.Inventory/Stock/Replenishment.cs).
// A DbSet-mapped entity on IdentityDbContext, keyed by CustomerId, deleted on MarkCompleted(). Per ADR 022's
// guard, this does relational things the Wolverine way — plain state, StartOrHandle/Handle, TimeoutMessage,
// MarkCompleted() — and never re-implements event sourcing on SQL.
//
// This class is a PURE Wolverine message handler — no [WolverinePost] attributes, exactly like
// Replenishment. An earlier draft put [WolverinePost] directly on this class's own methods (design.md
// decision 1's original plan); that failed empirically — Wolverine's HTTP chain builder conflates multiple
// [WolverinePost]-annotated methods living in the same class, throwing UnResolvableVariableException while
// building one chain because it pulled in a dependency belonging to the other. The fix, verified by
// re-running the integration tests: the customer-facing HTTP surface lives in RequestEmailChangeEndpoint /
// ConfirmEmailChangeEndpoint (see those files), which validate then dispatch into this saga via
// IMessageBus.InvokeAsync — confirmed synchronous, in-process (ctx7 guide/messaging/message-bus.md).
//
// Lifecycle (slices 5.5 -> 5.6 / 5.7):
//   open      — a RequestEmailChange with no open saga starts one (StartOrHandle, new branch): records
//               PendingEmail and schedules the deadline (EmailChangeTimeout).
//   re-request — a RequestEmailChange with a saga already open (StartOrHandle, existing branch): updates
//               PendingEmail to the newest value WITHOUT rescheduling the timeout. Wolverine has no
//               scheduled-message cancellation, so the ORIGINAL deadline continues to govern the window —
//               rescheduling would leave the first timeout still armed, firing early against a "reset"
//               window (a defect caught during Workshop 002 v1.1 review; do not reintroduce it).
//   confirm   — a ConfirmEmailChange within the window applies PendingEmail to the Customer row and
//               completes the saga; a conflict (PendingEmail claimed by another customer since) is rejected
//               by the endpoint's guard BEFORE dispatch, so this Handle only ever runs on the success path.
//   timeout   — an EmailChangeTimeout while still open drops the pending change (no row write) and completes.
//
// NotFound — mandatory, not automatic (the same Saga #1 finding). Wolverine THROWS when a non-start saga
// message arrives for a saga it cannot find. The spec's silent no-op for a ConfirmEmailChange or
// EmailChangeTimeout after the saga already resolved requires the explicit static NotFound methods below.
// Verified empirically: NotFound does NOT surface as an HTTP 404 through IMessageBus.InvokeAsync — the
// endpoint's Post simply completes and returns 200, which is a MORE faithful "silent no-op" than an error
// status would be (the caller is told nothing went wrong, exactly as the spec intends).
public class EmailChange : Saga
{
    public string? Id { get; set; }              // = CustomerId
    public string PendingEmail { get; set; } = "";

    // open OR re-request — Wolverine calls this on a blank saga when none is open for the customer, or on
    // the loaded saga when one is. A blank instance has PendingEmail == "" (its default); an open saga
    // always holds a non-empty PendingEmail, so emptiness reliably means "new" regardless of how Wolverine
    // assigns Id. On open we schedule the deadline (returning the TimeoutMessage auto-schedules it); on
    // re-request we only update PendingEmail and cascade nothing — see the class remarks on why the deadline
    // is never rescheduled. Normalizes the email itself (single source of truth), same convention as
    // RegisterCustomer.
    public OutgoingMessages StartOrHandle(RequestEmailChange command, EmailChangeDeadline deadline)
    {
        var isNew = string.IsNullOrEmpty(PendingEmail);
        Id = command.CustomerId;
        PendingEmail = command.NewEmail.Trim().ToLowerInvariant();

        return isNew
            ? new OutgoingMessages { new EmailChangeTimeout(command.CustomerId, deadline.Duration) }
            : [];
    }

    // confirm — applies the pending email and completes. ConfirmEmailChangeEndpoint's guard already
    // rejected a conflicting email before this runs, so this is the success path only. The DB unique index
    // (ux_customers_email) remains the true backstop against that guard's own race, exactly as
    // RegisterCustomer's guard is racy on its own.
    public async Task Handle(ConfirmEmailChange command, IdentityDbContext db)
    {
        var customer = await db.Customers.FindAsync(Id);
        customer!.Email = PendingEmail;
        MarkCompleted();
    }

    // timeout — still open at the deadline: drop the pending change (no row write) and complete.
    public void Handle(EmailChangeTimeout timeout) => MarkCompleted();

    // Silent no-ops (see the class remarks) — without these Wolverine throws on an unmatched saga message.
    public static void NotFound(ConfirmEmailChange command) { }
    public static void NotFound(EmailChangeTimeout timeout) { }
}
