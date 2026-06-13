---
name: wolverine-cross-bc-cascading
description: "CritterMart's cross-BC messaging convention: cascade typed messages over Wolverine conventional RabbitMQ routing, and for conditional cascades return a typed nullable tuple — never object?. The object? trap silently breaks conventional routing because outbound exchanges/queues are provisioned from types discovered at code-gen time. Use when writing or reviewing any handler that cascades a message to another service."
cluster: core
tags: [wolverine, rabbitmq, messaging, cross-bc, cascading, conventional-routing]
---

# CritterMart Cross-BC Cascading Convention

How CritterMart's services send work across bounded-context boundaries, and the one trap that silently breaks it. Every cross-BC hop in the codebase follows this shape; the file references are the authority — when this skill and the code disagree, the code wins and this skill gets a DEBT row.

**Defer-to-upstream discipline:** the upstream JasperFx ai-skills library (`wolverine-handlers-fundamentals`, `wolverine-messaging-message-routing`, `wolverine-integrations-rabbitmq`) documents the cascading-return and conventional-routing *APIs*. This skill documents only what CritterMart layers on top: that cross-BC sends are *cascaded* (not `IMessageBus.PublishAsync` calls), that they ride *conventional* routing with no explicit topology, and the typed-return rule that keeps conventional routing able to see them. Do not consult this skill for Wolverine API mechanics; do not consult upstream for the CritterMart convention.

## When to apply this skill

- Writing a handler that needs to send a command/event to another service (Orders → Inventory is the only cross-BC pair in round one).
- Choosing the **return type** of any handler that conditionally sends a cross-BC message.
- Reviewing a PR that adds or changes a cross-BC message flow.
- Diagnosing "the handler ran, the local stream updated, but the other service never received the message."

## Convention 1 — cross-BC sends are cascaded return values, not bus calls

CritterMart prefers **Wolverine cascading messages** — return the message from the handler and let the framework send it — over injecting `IMessageBus` and calling `PublishAsync`, and over the Process-Manager-via-Handlers building block, on a per-hop basis. Cascading is a well-tested first-class feature; it keeps the handler a pure function of its inputs (testable with `tracked.Sent.SingleMessage<T>()`), and it keeps the "what leaves this hop" decision in the return type where review can see it. PMvH is reserved for the hops where it genuinely wins. (This is the project's standing `cascading-over-PMvH` preference.)

The cross-BC contracts themselves are published-language records in `CritterMart.Contracts` ([ADR 014](../../decisions/014-published-language-contracts-project.md)) — `ReserveStock`, `ReleaseStock`, `CommitStock` — not the sending service's internal events.

## Convention 2 — conventional routing, no explicit topology

Orders configures the broker with conventional routing:

```csharp
// src/CritterMart.Orders/Program.cs
opts.UseRabbitMqUsingNamedConnection("rabbitmq")
    .AutoProvision()
    .UseConventionalRouting();
```

Conventional routing **derives exchanges and queues from message types** — a cascaded message with no local handler is routed to the broker; replies are auto-listened by convention ([ADR 003](../../decisions/003-wolverine-rabbitmq-transport.md)). There is no hand-maintained topology. The cost of that convenience is Convention 3.

## Convention 3 — typed nullable tuple for conditional cascades, never `object?` (read this first)

When a handler cascades **one of several** messages depending on a decision, return a **typed nullable tuple** with one member per possible message — not `object?`.

```csharp
// src/CritterMart.Orders/Order/PaymentHandlers.cs — the payment gate (slices 4.4 / 4.6 / 2.4)
public static async Task<(Contracts.CommitStock?, Contracts.ReleaseStock?)> Handle(
    PaymentDecision message, IDocumentSession session)
{
    var stream = await session.Events.FetchForWriting<OrderStatusView>(message.OrderId);
    if (stream.Aggregate?.Status != OrderStatus.StockReserved)
        return (null, null);                       // guard / duplicate → nothing cascades

    if (message.Approved)
        return (new Contracts.CommitStock(...), null);   // approve → commit, to Inventory

    return (null, new Contracts.ReleaseStock(...));      // decline → release, to Inventory
}
```

**Why not `object?`.** The Wolverine docs show `object?` as a valid conditional-cascade return, and it works fine for *in-process* routing. But CritterMart routes cross-BC over RabbitMQ with **conventional routing**, which provisions outbound exchanges/queues from the message **types it discovers at code-gen / startup time**. With `object?`, Wolverine cannot infer that `CommitStock` (or `ReleaseStock`) needs an outbound broker route — so the message **silently fails to route**: the handler runs, the local stream commits, and the other service never hears about it. The typed tuple gives Wolverine compile-time visibility of *both* message types, so it provisions a route for each; a `null` member simply doesn't cascade.

This is the failure mode behind "the handler ran but the other service got nothing." It cost a real session (slice 2.4) to diagnose; it is buried no longer.

## In-repo precedents

| Hop | Handler | Cascades | Slice |
| --- | --- | --- | --- |
| Stock gate request | `StockReservationOutcomeHandlers` / place-order flow | `ReserveStock` (single, unconditional) | 4.2 |
| Payment decline | `PaymentDecisionHandler` (decline branch) | `ReleaseStock` | 4.6 |
| Payment timeout | `PaymentTimeoutHandler` | `ReleaseStock` (unconditional — Inventory's per-SKU guard decides) | 4.7 |
| Payment approve | `PaymentDecisionHandler` (approve branch) | `CommitStock` | 2.4 |

The approve and decline branches share one handler, which is exactly why the **tuple** (not `object?`) is load-bearing there: both `CommitStock` and `ReleaseStock` must be visible to conventional routing from the same return type.

## Quick reference: common mistakes to catch

- **`Task<object?>` (or `Task<IMessage?>`) as a cross-BC handler return type.** Breaks conventional routing silently — replace with a typed tuple or a single typed nullable. The symptom is a green handler test and a message that never crosses the broker.
- **Injecting `IMessageBus` to `PublishAsync` a cross-BC command** where a cascading return would do. Prefer the return value; it keeps the handler pure and the routing type-visible.
- **Sending an internal event across the boundary** instead of a `CritterMart.Contracts` published-language record ([ADR 014](../../decisions/014-published-language-contracts-project.md)).
- **Expecting conventional routing to "just know" about a type only ever produced behind an `object?`/`dynamic` seam.** It provisions from statically discovered types; hide the type and you hide the route.

## See also

- Upstream: `wolverine-handlers-fundamentals` (valid return types, cascading), `wolverine-messaging-message-routing` (routing rules), `wolverine-integrations-rabbitmq` (transport + conventional routing mechanics).
- [ADR 003](../../decisions/003-wolverine-rabbitmq-transport.md) — Wolverine + RabbitMQ transport, why conventional routing.
- [ADR 014](../../decisions/014-published-language-contracts-project.md) — published-language contracts in `CritterMart.Contracts`.
- Archived `openspec/changes/archive/2026-06-13-slice-2-4-commit-stock/design.md` (Decision 2) — the original diagnosis of the `object?` trap.
