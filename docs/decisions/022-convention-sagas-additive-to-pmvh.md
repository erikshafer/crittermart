# ADR 022: Convention Sagas Are Additive to PMvH

**Status**: Accepted

## Context

[ADR 007](007-process-manager-via-handlers-for-order.md) chose Process Manager via Handlers (PMvH) for the
Order aggregate — the Order aggregate *is* its own process manager, with state-flag events and a scheduled,
idempotent timeout — and named the tradeoff explicitly: "forgoes the `Wolverine.Saga` base class;
intentional — the PMvH pattern is the point." Round one shipped with zero `Wolverine.Saga` usages, an
encoded non-negotiable (`docs/rules/structural-constraints.md:76`).

Round two's replenishment work (slices 2.5–2.7, retro
[`implementations/035`](../retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md))
introduced CritterMart's **first** convention `Wolverine.Saga` — a SKU-keyed, Marten-stored `Replenishment`
saga in Inventory that opens on a reservation shortfall, resolves on a covering restock, and escalates on a
config-driven timeout. It shipped without a dedicated ADR; the additive-not-PMvH-conversion stance existed
only scattered across `docs/research/wolverine-saga-feasibility.md`, Workshop 001 v1.12 § 2, and retro 035
itself. Saga #2 (Identity email-change confirmation, EF-Core-backed — gated on the paired
[ADR-009 revisit](009-polecat-deferred-for-round-one.md)) is about to become the *second* convention saga in
the codebase. Formalizing the policy now, before a second instance normalizes an undocumented pattern, is
the point of this ADR.

## Decision

CritterMart uses the convention `Wolverine.Saga` base class **additively**, alongside PMvH (ADR 007) — never
as a conversion of an existing PMvH flow. Order and Cart keep PMvH; they are not candidates for conversion to
`Wolverine.Saga`. New coordination flows that genuinely need state to survive across messages and time (a
confirm-or-expire window, a shortfall-and-restock wait) are free to reach for a convention saga instead,
judged case by case — not as a blanket replacement policy.

Two sagas exist as of this ADR, deliberately backed by two different stores, because the contrast is itself
part of what CritterMart demonstrates:

- **Saga #1 — Inventory replenishment** (Marten-backed): saga state lives in Marten saga storage, separate
  from the `Stock` event stream — the saga is *not* event-sourced itself.
- **Saga #2 — Identity email-change confirmation** (EF-Core-backed, pending its own implementation session):
  saga state lives as a `DbSet`-mapped entity on `IdentityDbContext`, riding the same
  `AddDbContextWithWolverineIntegration` wiring the customer registry already uses.

Two backing stores hosting the same `Wolverine.Saga` programming model is deliberate proof that the saga
store is swappable — extending [ADR 009](009-polecat-deferred-for-round-one.md)'s persistence-agnostic
thesis (Wolverine's handler model is not coupled to Marten) from ordinary handlers to sagas specifically.

**Guard, binding on every convention saga in this codebase:** a saga must do relational or document things
*the Wolverine way* — plain state, `Handle`/`StartOrHandle` methods, `TimeoutMessage`, `MarkCompleted()` — and
must not drift into re-implementing event sourcing on top of SQL or ad hoc document storage. If a flow needs
event-sourced history, it belongs on a Marten stream with a projection, not inside saga state.

## Consequences

Formalizes, retroactively, the decision Saga #1 shipped without: CritterMart now has three named coordination
patterns side by side for the talk — cascading messages (per-hop), PMvH (Order/Cart as their own process
manager), and convention sagas (Inventory, Identity) — each with a documented reason for its shape rather
than an implicit one. Clears the gate for Saga #2's Session 2 (Workshop 002 amendment → OpenSpec → narrative
→ implement → retro); that session inherits this ADR's guard as a binding constraint on the `EmailChange`
saga's design, not a fresh question to re-litigate. Tradeoff: a fourth non-negotiable pattern for a
newcomer to the codebase to learn, mitigated by the "additive, not a replacement" framing and by
`structural-constraints.md` still naming Order/Cart's PMvH-only stance as unchanged.
