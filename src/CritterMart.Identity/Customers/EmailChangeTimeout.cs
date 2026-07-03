using Wolverine;
using Wolverine.Persistence.Sagas;

namespace CritterMart.Identity.Customers;

// The confirm-or-expire deadline (Workshop 002 slice 5.7). A Wolverine TimeoutMessage auto-scheduled by
// being returned from EmailChange.StartOrHandle — mirrors Inventory's ReplenishTimeout. The delay is
// config-driven (Identity:EmailChangeTimeout, via EmailChangeDeadline). [SagaIdentity] correlates the fired
// timeout on CustomerId.
public record EmailChangeTimeout([property: SagaIdentity] string CustomerId, TimeSpan Delay) : TimeoutMessage(Delay);
