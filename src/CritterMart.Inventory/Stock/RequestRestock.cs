namespace CritterMart.Inventory.Stock;

// The supplier-notification stub (Workshop 001 slice 2.5, § 8 resolution #19). Cascaded by the
// Replenishment saga when it opens; RequestRestockHandler logs it. Fulfilment is the Operator's existing
// ReceiveStock path — a configurable auto-restock demo lever is out of scope (a later demo-affordance slice).
public record RequestRestock(string Sku, int Quantity);
