namespace CritterMart.Inventory.Stock;

// Config-singleton for the replenishment deadline (Inventory:ReplenishTimeout), injected into the
// Replenishment saga's Start. Mirrors Orders' PaymentDeadline / CartActivityDeadline / PaymentAuthDelay
// config-singleton pattern (src/CritterMart.Orders/Program.cs). The short default keeps the escalate path
// demoable at speaking pace; a production value would be hours. This is the DEADLINE lever only — not the
// out-of-scope auto-restock supplier-delay lever (§ 8 resolution #19).
public record ReplenishDeadline(TimeSpan Duration)
{
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(2);
}
