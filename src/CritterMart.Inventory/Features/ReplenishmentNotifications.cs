using CritterMart.Inventory.Stock;

namespace CritterMart.Inventory.Features;

// The two Replenishment-saga "sinks" (Workshop 001 slices 2.5 + 2.7). Both are Inventory-local message
// handlers the saga cascades to, and both are deliberately thin log-only stubs — the saga carries the
// behavior; these exist to make its outbound signals observable on the CritterWatch console and in logs.

// RequestRestock — the supplier-notification stub (§ 8 resolution #19). A real integration would call a
// supplier API; here we log, and the Operator's existing ReceiveStock path is what fulfils the restock.
public static class RequestRestockHandler
{
    public static void Handle(RequestRestock message, ILogger<RequestRestock> logger)
        => logger.LogInformation(
            "Supplier notified: restock {Sku} x{Quantity}", message.Sku, message.Quantity);
}

// ReplenishmentEscalated — the operator-alert sink (§ 8 resolution #18, escalate-and-complete). Logged at
// Warning so an unreplenished SKU stands out; the message itself is what surfaces as bus traffic on the console.
public static class ReplenishmentEscalatedHandler
{
    public static void Handle(ReplenishmentEscalated message, ILogger<ReplenishmentEscalated> logger)
        => logger.LogWarning(
            "Operator alert: SKU {Sku} went unreplenished, {Outstanding} still short",
            message.Sku, message.Outstanding);
}
