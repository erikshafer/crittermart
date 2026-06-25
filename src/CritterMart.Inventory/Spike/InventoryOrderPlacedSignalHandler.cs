using CritterMart.Contracts;
using Microsoft.Extensions.Logging;

namespace CritterMart.Inventory.Spike;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline.
//  See docs/research/cw-telemetry-fodder.md.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// One of two subscribers (Catalog is the other) to the OrderPlacedSignal broadcast. Its only purpose
// is to make Inventory bind a conventional listening queue for the signal — that queue is the fan-out
// edge CritterWatch's Topology renders. The handler does no domain work (Inventory's real reservation
// flow is the ReserveStock request/reply path). Inert unless Orders broadcasts (Cw:Telemetry on).
public static class InventoryOrderPlacedSignalHandler
{
    public static void Handle(OrderPlacedSignal message, ILogger<OrderPlacedSignal> logger) =>
        logger.LogInformation(
            "CW-spike: Inventory observed OrderPlacedSignal for order {OrderId} (total {Total})",
            message.OrderId, message.Total);
}
