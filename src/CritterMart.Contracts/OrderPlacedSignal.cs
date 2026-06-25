namespace CritterMart.Contracts;

// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CW-TELEMETRY SPIKE (research/cw-telemetry-spike) — NOT round-one baseline.
//  See docs/research/cw-telemetry-fodder.md.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// A BROADCAST integration event published by Orders when an order is placed (gated on Cw:Telemetry).
// Unlike the request/reply ReserveStock flow (one sender, one handler), this notification fans OUT to
// MULTIPLE subscribers — Inventory AND Catalog — over RabbitMQ. The point is purely topological: it
// thickens CritterWatch's Topology edges, adds Listeners queues, and produces Durability inbox/outbox
// rows, and it gives Catalog (which has no cross-BC flows of its own on `main`) its first inbound edge.
// Carries only the notification shape — no command semantics, no reply expected.
public record OrderPlacedSignal(string OrderId, string CustomerId, decimal Total);
