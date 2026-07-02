# ADR 009: Polecat Deferred for Round One

**Status**: Accepted

## Context

Polecat is the JasperFx identity stack; CritterCab uses it. CritterMart's Identity bounded context could be implemented as a Polecat-backed deployed service, but the six-day round-one timeline makes that a stretch and pulls focus away from the event-sourcing material the talk is built on.

## Decision

Polecat is explicitly deferred. Identity is implemented as a hardcoded customer ID in the frontend for round one, with no deployed Identity service. The customer ID is used in commands, narratives, and OpenSpec scenarios as if it came from a real identity system.

## Consequences

One less service to scaffold; one less stack to learn for the talk. Tradeoff: the talk cannot demonstrate real identity flows. Promoting the stubbed Identity context into a deployed Polecat-backed service is queued in the Vision doc's Long Road as a natural sequel.

> **Amendment (ADR 015 / ADR 016 PR, 2026-06-04).** Now that a real frontend is chosen ([ADR 015](015-vite-react-frontend-stack.md), Vite + React), the hardcoded customer ID is realized behind a single React indirection — a `useCurrentCustomer` seam (a context provider / hook) that today returns the stubbed ID. Components consume the hook; none know the value is stubbed. This keeps the decision above intact while making the eventual Polecat promotion a one-file change rather than a call-site sweep. The seam is idiomatic React, so the forward-compat insurance is effectively free; it does not warrant its own ADR.

> **Amendment ([ADR 022](022-convention-sagas-additive-to-pmvh.md), 2026-07-02).** This ADR's "no deployed Identity service" clause is stale as a matter of fact, not stance: the customer registry (slices 5.1–5.4; retros [`implementations/033`](../retrospectives/implementations/033-slices-5-1-5-2-customer-registry.md) / [`034`](../retrospectives/implementations/034-slices-5-3-5-4-customer-data.md)) shipped a real, deployed EF-Core-backed service. What survives from the original Decision is the framing that matters — Identity as the deliberately **boring, non-event-sourced** foil to the other three services' Marten-backed event sourcing — not the "no service" detail.
>
> Saga #2 (Identity email-change confirmation, EF-Core-backed) gives Identity its **first stateful consumer**, prompting the revisit this amendment settles: **the "deliberately boring CRUD" stance holds.** The `EmailChange` saga is EF Core doing relational things the Wolverine way — a `DbSet`-mapped `Saga`-derived entity, string-keyed, riding the existing `AddDbContextWithWolverineIntegration` wiring, with no event sourcing introduced. It *extends* this ADR's persistence-agnostic thesis (Wolverine's handler model is not coupled to Marten) to sagas specifically, rather than reversing the "boring foil" stance. The binding guard, carried from the research that de-risked this: the saga must stay relational-things-the-Wolverine-way and must not drift into re-implementing event sourcing on SQL. See [ADR 022](022-convention-sagas-additive-to-pmvh.md) for the cross-cutting convention-saga policy this revisit is paired with.
