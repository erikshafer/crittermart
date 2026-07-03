namespace CritterMart.Identity.Customers;

// Config-singleton for the email-change confirmation deadline (Identity:EmailChangeTimeout), injected into
// EmailChange.StartOrHandle. Mirrors Inventory's ReplenishDeadline config-singleton pattern
// (src/CritterMart.Inventory/Stock/ReplenishDeadline.cs). The short default keeps the confirm-and-drop paths
// demoable at speaking pace; the modeled production duration is 24 hours (Workshop 002 slices 5.5–5.7).
public record EmailChangeDeadline(TimeSpan Duration)
{
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(2);
}
