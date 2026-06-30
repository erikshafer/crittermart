using Wolverine.Persistence.Sagas;

namespace CritterMart.Inventory.Stock;

// The per-SKU shortfall surfaced on the ReserveStock refusal path (Workshop 001 slice 2.5). The concrete
// saga-start message — the workshop modeled the trigger as "on ReserveStock shortfall," the same way
// slices 2.3/2.4 introduced concrete ReleaseStock/CommitStock names beyond the model. Inventory-local;
// never crosses the BC boundary. [SagaIdentity] points the Replenishment saga at the SKU (its Id), since
// `Sku` is not named ReplenishmentId/SagaId/Id and so would not be picked up by convention.
public record BackorderDetected([property: SagaIdentity] string Sku, int Shortfall);
