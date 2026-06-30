namespace CritterMart.Inventory.Stock;

// The operator-facing alert raised when a Replenishment saga reaches its deadline still unreplenished
// (Workshop 001 slice 2.7, § 8 resolution #18 — escalate-and-complete). Cascaded by the saga so the alert
// flows on the bus and surfaces in CritterWatch (the saga's motivation); ReplenishmentEscalatedHandler logs
// it. Inventory-local. The 5th saga message beyond the four the prompt enumerated — see design.md decision 5.
public record ReplenishmentEscalated(string Sku, int Outstanding);
