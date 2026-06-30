using Wolverine;

namespace CritterMart.Inventory.Stock;

// CritterMart's first convention Wolverine.Saga (Workshop 001 slices 2.5–2.7). The saga IS the backorder
// state — a Marten-stored document keyed by SKU, deleted on MarkCompleted(), NEVER event-sourced (Design A,
// chosen with Erik). Contrast the Order's Process-Manager-via-Handlers (state on the stream, ADR 007): here
// the transient coordination state lives in saga storage, which is exactly what a saga is for. The third
// member of the "ways to wait for a deadline" trio: Bruun todo-projection vs. PMvH-on-the-stream vs. this.
//
// Lifecycle (slices 2.5 → 2.6 → 2.7):
//   open     — a BackorderDetected with no open saga starts one (StartOrHandle, new branch): records
//              Outstanding, fires the supplier-notification stub (RequestRestock), and schedules the
//              deadline (ReplenishTimeout).
//   re-open  — a BackorderDetected with a saga already open raises Outstanding to the GREATER value
//              (StartOrHandle, existing branch): idempotent under at-least-once redelivery; no second saga,
//              no second timeout, no fresh RequestRestock.
//   resolve  — a RestockArrived that covers Outstanding completes the saga; a partial receipt reduces
//              Outstanding and stays open (no fresh RequestRestock).
//   escalate — a ReplenishTimeout that fires while still open publishes an operator alert and completes.
//
// BackorderDetected is handled by a single StartOrHandle method, NOT a separate Start + Handle pair: the
// same message both opens a saga (when none exists) and updates an open one. Wolverine's StartOrHandle
// convention dispatches both cases to this one instance method — on a fresh, blank saga when none is found,
// on the loaded saga when one is. (A separate static Start + instance Handle for the same message type is
// ambiguous and resolves to the continuation path, leaving a new saga's Id unset.)
//
// NotFound — the load-bearing detail. Wolverine THROWS when a non-Start saga message arrives for a saga it
// cannot find. The spec's "silent no-op" for a RestockArrived with no open saga, and for a ReplenishTimeout
// delivered after the saga already resolved (the runtime offers no scheduled-message cancellation — the
// same property slices 3.4/4.7 rely on), requires the explicit static NotFound methods below. Empty is
// enough — their presence alone is what keeps Wolverine from blowing up. There is no global flag for this.
public class Replenishment : Saga
{
    public string? Id { get; set; }      // = Sku
    public int Outstanding { get; set; }

    // open OR re-open — Wolverine calls this on a blank saga when none exists for the SKU, or on the loaded
    // saga when one does. A blank instance has Outstanding == 0 (its default); an open saga always holds
    // Outstanding >= 1 (every shortfall is >= 1, and a partial restock leaves >= 1 — completion deletes the
    // saga), so Outstanding == 0 reliably means "new" regardless of how Wolverine assigns the Id. On open we
    // cascade the supplier-notification stub and the deadline (returning the TimeoutMessage auto-schedules
    // it); on re-open we only raise Outstanding to the greater value (max, not sum — idempotent under
    // at-least-once redelivery) and cascade nothing.
    public OutgoingMessages StartOrHandle(BackorderDetected e, ReplenishDeadline deadline)
    {
        var isNew = Outstanding == 0;
        Id = e.Sku;
        Outstanding = Math.Max(Outstanding, e.Shortfall);

        return isNew
            ? new OutgoingMessages
            {
                new RequestRestock(e.Sku, e.Shortfall),
                new ReplenishTimeout(e.Sku, deadline.Duration),
            }
            : [];
    }

    // resolve — covered → complete (state deleted); partial → reduce and stay open, no fresh RequestRestock.
    public void Handle(RestockArrived e)
    {
        if (e.Quantity >= Outstanding)
        {
            MarkCompleted();
        }
        else
        {
            Outstanding -= e.Quantity;
        }
    }

    // escalate — still open at the deadline: publish the operator alert (cascaded) and complete.
    public ReplenishmentEscalated Handle(ReplenishTimeout t)
    {
        MarkCompleted();
        return new ReplenishmentEscalated(Id!, Outstanding);
    }

    // Silent no-ops (see the class remarks) — without these Wolverine throws on an unmatched saga message.
    public static void NotFound(RestockArrived e) { }
    public static void NotFound(ReplenishTimeout t) { }
}
