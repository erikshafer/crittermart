using Wolverine.Persistence.Sagas;

namespace CritterMart.Inventory.Stock;

// Published by ReceiveStockEndpoint on every stock receipt (Workshop 001 slice 2.6, § 8 resolution #15 —
// a dedicated message, NOT Marten→Wolverine forwarding of the raw StockReceived stream event, which would
// annotate a domain event and pull in the async daemon). Routed to the open Replenishment saga for the SKU;
// a silent no-op when none is open (the saga's NotFound). [SagaIdentity] correlates it on `Sku`.
public record RestockArrived([property: SagaIdentity] string Sku, int Quantity);
