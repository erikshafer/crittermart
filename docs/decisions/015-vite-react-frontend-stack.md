# ADR 015: Vite + React SPA as the Round-Two Frontend Stack

**Status**: Accepted

## Context

The vision doc named the frontend stack as the one unresolved structural choice — "the specific frontend stack is TBD for round one." Round one shipped its seventeen modeled slices without a frontend; round two's forcing function is that a functional UI must exist before the second talk (the online .NET user group). [Research 002](../research/ecommerce-frontend-stack.md) produced the decision-evidence: ten landscape axes and seven candidate stacks evaluated against CritterMart-specific constraints, deliberately naming no winner. This ADR is the call that document was written to enable.

The constraint set that decides it is already locked by earlier ADRs and the success criteria:

- **The OpenTelemetry trace spanning the network is a hard success criterion**, not a nice-to-have (`vision.md` § Success criteria). [ADR 006](006-wolverine-http-per-service-no-bff.md) has the frontend call the three Wolverine.Http services **directly**, with no BFF. The combination — a client calling three services over a real HTTP boundary — *is* the shape the trace-demo beat depends on. A stack whose natural pattern shortcuts that boundary weakens a load-bearing piece of the talk.
- [ADR 004](004-dotnet-aspire-orchestrator.md) orchestrates the topology locally; the demo story requires one command to boot everything with the frontend visible in the Aspire dashboard.
- [ADR 009](009-polecat-deferred-for-round-one.md) puts the hardcoded customer ID **in the frontend**, so the chosen stack is the home of the identity stub (see [ADR 016](016-frontend-full-pipeline-ui-first-class.md) and the ADR 009 amendment for the seam).
- The talk audience is a .NET shop; talk budget spent explaining the frontend is budget not spent on event sourcing with Marten, which is the subject.

The decision clears the ADR bar on all three counts: reversing it touches every user-facing surface, the SPA-over-three-services / no-BFF shape is non-obvious, and a later contributor would otherwise re-derive it.

## Decision

The round-two frontend is **Vite + React + TypeScript + TanStack Query + Tailwind CSS v4 + shadcn/ui** — Candidate A in Research 002, the CritterBids sibling-project precedent.

It is a client-side-rendered single-page application that calls the three Wolverine.Http services directly over HTTP. It integrates into the Aspire AppHost via `AddViteApp`, appearing as a managed resource in the dashboard alongside the services, RabbitMQ, and PostgreSQL. TanStack Query owns server state and supplies the `onMutate` / `setQueryData` rollback pattern that makes add-to-cart feel instant. Tailwind v4 plus shadcn/ui supplies accessible, Radix-backed components without hand-wiring ARIA.

## Consequences

The cross-network OpenTelemetry trace beat is preserved: the SPA-to-three-services boundary is a real HTTP hop, so the trace spans it exactly as the talk needs. [ADR 006](006-wolverine-http-per-service-no-bff.md) stays intact — the SPA is the cross-service orchestrator, no BFF is introduced. The CritterBids precedent eliminates the "what library for X" debates and gives the round-two timeline its best chance. The stack is LLM-friendly (a large training corpus), which the project's AI-assisted authoring depends on.

The accepted costs: a React SPA ships JavaScript to the client and pays the full hydration cost on first load (acceptable at round-one/round-two scale, a Core Web Vitals consideration only if the storefront grows content-heavy), and a pure SPA needs extra work for SEO (out of scope now, a round-two-plus concern). The choice also teaches the audience nothing novel — which is deliberate: it keeps the talk's teaching budget on the Critter Stack rather than the frontend.

Rejected alternatives. **Blazor United / Blazor Server** would let the audience stay in C# in a single process with the simplest demo ergonomics, but its natural pattern injects services rather than calling them over HTTP — shortcutting the very network boundary the trace demo needs — and the four render modes are a teaching topic that competes with Marten for a 50-minute slot. **HTMX + ASP.NET Core Razor** has the smallest client runtime and a genuine "you don't need a JS framework" beat, but a weaker component-test story and hand-rolled optimistic UI. **Next.js (App Router)** was set aside on principle: its React Server Components server tier becomes a de-facto BFF, which fights [ADR 006](006-wolverine-http-per-service-no-bff.md) head-on. **Astro Islands** and **SvelteKit** carry novelty cost for a .NET audience and a smaller LLM corpus. The full comparison, with sourced trade-offs, is in [Research 002](../research/ecommerce-frontend-stack.md).

This ADR resolves the `vision.md` "frontend stack TBD" line; that line is updated in the same PR or an immediately-following `tidy:` commit. The frontend's *modeling grain* — how it threads through the SDD pipeline — is a separate decision in [ADR 016](016-frontend-full-pipeline-ui-first-class.md).
