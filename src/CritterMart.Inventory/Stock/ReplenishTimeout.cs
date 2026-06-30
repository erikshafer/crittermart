using Wolverine;
using Wolverine.Persistence.Sagas;

namespace CritterMart.Inventory.Stock;

// The replenishment deadline (Workshop 001 slice 2.7). A Wolverine TimeoutMessage auto-scheduled by being
// RETURNED from the saga's Start — distinct from the manual DelayedFor self-scheduling the Bruun PMvH
// slices (3.4/4.7) use; that contrast is a teaching beat. The delay is config-driven
// (Inventory:ReplenishTimeout, via ReplenishDeadline). [SagaIdentity] correlates the fired timeout on `Sku`.
public record ReplenishTimeout([property: SagaIdentity] string Sku, TimeSpan Delay) : TimeoutMessage(Delay);
