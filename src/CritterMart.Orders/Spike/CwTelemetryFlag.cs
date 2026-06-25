namespace CritterMart.Orders.Spike;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// The one runtime toggle (config key Cw:Telemetry, env Cw__Telemetry) gating the spike's ACTIVE
// behaviour: the async daemon (Program.cs) and the OrderPlacedSignal broadcast (PlaceOrder). Flag
// OFF reproduces the round-one baseline CritterWatch picture (inline-only, no async progress, no
// cross-BC topology beyond stock reservation); flag ON lights the dark surfaces. Registered as a
// singleton so the PlaceOrder endpoint can decide whether to broadcast. Mirrors the
// PaymentDeadline / PaymentDeclinePolicy config-singleton pattern already used in this service.
public record CwTelemetryFlag(bool Enabled);
