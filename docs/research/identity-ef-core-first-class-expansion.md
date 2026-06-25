---
version: 0.1
status: Active
date: 2026-06-25
references:
  - src/CritterMart.Identity/Program.cs
  - docs/decisions/009-identity-ef-core-data-store.md
  - openspec/specs/customer-registry/spec.md
  - docs/retrospectives/implementations/033-slices-5-1-5-2-customer-registry.md
  - docs/retrospectives/implementations/034-slices-5-3-5-4-customer-data.md
---

# Research: expanding Identity into a first-class Wolverine + EF Core showcase

> **What this is:** a parking-lot capture of an exploratory direction Erik floated on 2026-06-25 —
> grow the Identity bounded context into a fuller demonstration that *"Wolverine treats EF Core as a
> first-class citizen."* **What this is not:** a decision, a committed slice, or a build order. It has
> no authority over the pipeline; if it earns a binding choice it graduates to an ADR, and if it earns
> build work it becomes an event-model pass + narrative + OpenSpec proposal + prompt like any other BC
> work. Priority per Erik: **not high** — "something to think about."

## Bottom line

Identity is currently the deliberately-boring counter-example in CritterMart: the one service that is
**not** event-sourced, proving Wolverine's handler model is persistence-agnostic (ADR 009 — EF Core /
Npgsql instead of Marten). The idea is to lean *into* that contrast and make Identity a richer,
intentional showcase of the Critter Stack's EF Core story (`WolverineFx.EntityFrameworkCore`): the
transactional outbox over EF Core, `AutoApplyTransactions`, `[Entity]` auto-loading, domain events
raised from EF entities, and possibly an EF-Core-backed saga — so the talk can say "and here's the
*same* Wolverine programming model over a relational DbContext, with the same durability guarantees,"
not just "Identity happens to use EF Core."

## Where Identity stands today (round one)

- **EF Core on shared Postgres**, not Marten; same Wolverine handler wiring, a `DbContext` instead of an
  `IDocumentSession` (ADR 009 — a *data store*, not an auth provider; no Polecat).
- **Customer registry** (slices 5.1–5.4): `POST /customers` registers a customer; emits the
  `CustomerRegistered` **published-language** event over RabbitMQ; Orders consumes it (slice 5.4) into a
  local `LocalCustomerView` read model.
- So today Identity already does *one* cross-BC RabbitMQ flow and *one* write path — enough to prove the
  point, but thin as a showcase.

## What "first-class EF Core" could exercise (menu, not a plan)

The Critter Stack's EF Core value props that Identity does **not** yet demonstrate — each a candidate
slice, each with an upstream skill to defer to:

- **Transactional outbox over EF Core** — `WolverineFx.EntityFrameworkCore` outbox + `AutoApplyTransactions`
  so the `CustomerRegistered` publish and the DbContext write commit atomically (skill:
  `wolverine-handlers-efcore`, `critterstack-arch-new-project-wolverine-efcore`). *Is this already on?*
  Worth auditing what slice 5.x actually wired vs. relied on default.
- **`[Entity]` declarative persistence** — auto-load the customer entity before a handler runs and return
  `IStorageAction<T>` / `Storage.Update(...)` from pure functions, instead of manual `LoadAsync`/null
  checks (skill: `wolverine-handlers-declarative-persistence`). A clean before/after teaching beat against
  the Marten `[Aggregate]` equivalent.
- **Domain events raised from EF entities** — an EF entity that raises domain events Wolverine picks up and
  publishes, the relational mirror of Marten's event append.
- **More features → more published-language → more RabbitMQ:** customer profile updates, address /
  contact management, customer preferences, a deactivate/reactivate lifecycle — each emitting its own
  published-language event (`CustomerProfileUpdated`, `CustomerDeactivated`, …) that Orders (or a future
  BC) consumes. This thickens Topology / Listeners / Durability with *relational*-backed flows.
- **An EF-Core-backed saga** (if a genuine multi-step Identity workflow exists) — saga storage over the
  DbContext, contrasting Marten saga storage. Only if a real workflow motivates it; don't invent one.

## Open questions (to resolve before any of this becomes build work)

1. **Does it earn a workshop pass?** A larger Identity BC means new events/commands/read-models — that's
   an Event Modeling workshop increment + a context-map update (Identity↔Orders relationship), not just
   tacked-on endpoints. Where does it sit relative to the round-two storefront roadmap?
2. **Does the contrast survive?** Identity's whole teaching value is being the *non*-event-sourced foil.
   Adding domain events / sagas must stay clearly "EF Core doing relational things the Wolverine way," not
   drift into re-implementing event sourcing on SQL — or it loses the point it exists to make.
3. **Audit first.** Before proposing features, confirm what slice 5.x *already* wired (outbox on/off,
   transactions, the publish path) so the showcase builds on reality, not assumption.
4. **Priority / sequencing.** Explicitly low-priority. Most likely lands as a *design-return* (a workshop
   increment or narrative) when the cadence calls for one, rather than jumping the storefront queue.

## Relationship to the cadence

CritterMart is at a design-return checkpoint (two customer-BC impl PRs, retros 033/034). If this idea
advances, the natural first step is **design-shaped, not code**: an Identity event-model increment +
context-map note + a narrative threading the customer's Identity journey — which also satisfies the
design-return the pipeline currently owes.
