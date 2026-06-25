using CritterMart.Contracts;
using Microsoft.Extensions.Logging;

namespace CritterMart.Catalog.Spike;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline.
//  See docs/research/cw-telemetry-fodder.md.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// The SECOND subscriber to the OrderPlacedSignal broadcast — the one that makes it a genuine fan-out.
// On `main` Catalog has no cross-BC message flows (RabbitMQ is wired solely as the CritterWatch
// telemetry channel, no conventional routing). The spike adds conventional routing to Catalog's
// Program.cs so this handler binds an inbound queue — giving Catalog its first Topology edge in the
// console. The handler does no catalog work; the value is the edge itself.
public static class CatalogOrderPlacedSignalHandler
{
    public static void Handle(OrderPlacedSignal message, ILogger<OrderPlacedSignal> logger) =>
        logger.LogInformation(
            "CW-spike: Catalog observed OrderPlacedSignal for order {OrderId} (total {Total})",
            message.OrderId, message.Total);
}
