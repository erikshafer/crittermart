# CritterMart — Vision

A single-seller storefront, built as a teaching reference architecture for event sourcing with the Critter Stack.

## What this is

CritterMart is an ecommerce reference application designed to demonstrate event sourcing patterns through a recognizable, approachable domain. It runs as four separate .NET 10 services — Catalog, Inventory, Orders, and Identity — that communicate via Wolverine over RabbitMQ; there is no synchronous service-to-service HTTP. Persistence is a shared PostgreSQL instance with schema-per-service, accessed through Marten (Catalog, Inventory, Orders) and EF Core + Npgsql (Identity — the deliberately non-event-sourced foil). Wolverine.Http exposes each service's HTTP surface. A frontend client sits in front of the services as the customer-facing storefront: a Vite + React single-page application that calls each service's Wolverine.Http surface directly ([ADR 015](decisions/015-vite-react-frontend-stack.md)), modeled through the full SDD pipeline as first-class UI ([ADR 016](decisions/016-frontend-full-pipeline-ui-first-class.md)). .NET Aspire orchestrates the services, the broker, and the database locally, with OpenTelemetry tracing visible in the Aspire dashboard.

The aim is not to be a complete ecommerce platform. The aim is to teach. Each piece exists because it earns its place in a story about event sourcing, the Decider and Process Manager patterns, projections, and how the Critter Stack supports them in practice.

## Why this exists

CritterMart has two deliberate purposes, in priority order:

1. **It is the working example for an Event Sourcing with Marten talk** delivered first at ImprovingU (round one), then to an online .NET user group (round two). The talk runs 50 minutes with a 30-minute cut, no live coding, and uses CritterMart code and design artifacts as its anchoring material. Slice walkthroughs, in particular the Place Order flow, carry most of the pedagogical weight.

2. **It is a sandbox for a more disciplined Spec-Driven Development pipeline** than what CritterSupply used. Each slice is produced via Context Mapping, an Event Modeling workshop pass, OpenSpec proposals, and a sibling narrative, in that order, before any code is written. The pipeline itself doubles as the talk's section on what AI-assisted .NET development looks like in 2026.

Both purposes shape the same codebase, and both audiences benefit from the same artifacts.

## What this deliberately is not

CritterMart is a single-seller storefront. That choice rules out several things by design:

- No vendor portal, vendor identity, marketplace listings, or multi-channel sales
- No backoffice or admin UI
- No real payment integration; payment is stubbed
- No returns, no promotions, no shipping rate calculations, no real-time storefront updates
- No Polecat for round one; Identity is a real customer-registry service that (in round-one shipped code) still carries no authentication — the frontend sends a hardcoded customer id. Real authentication is now decided as the next increment ([ADR 023](decisions/023-real-authentication-for-identity.md): ASP.NET Core Identity + self-validated JWT), not yet built; see Long road below

Many of these cuts will make excellent follow-up blog posts and future enhancements. Cutting them is what kept the demo, the talk, and the round-one timeline honest.

## Bounded contexts

Four bounded contexts, each deployed as its own service:

- **Catalog**: products, prices, descriptions. Marten document store. The "when CRUD is fine" example.
- **Inventory**: stock per SKU. Event sourced. Inline snapshot. The textbook case. Also home to the `Replenishment` saga — CritterMart's first convention `Wolverine.Saga`, an additive counterpart to the Order aggregate's Process Manager via Handlers.
- **Orders**: Cart and Order, both event sourced. Order serves as the process manager for fulfilling a purchase, using the Process Manager via Handlers pattern.
- **Identity**: a real customer-registry service backed by EF Core + Npgsql — the "when relational CRUD is fine, the Wolverine way" example, and an Open-Host Service publishing `CustomerRegistered` over RabbitMQ. It carries no authentication or authorization: a customer identifier is still hardcoded into the frontend, and Polecat remains deferred (ADR 009). Round one originally stubbed this context entirely; it was promoted to a kept BC via Workshop 002, which now also carries its own convention saga, `EmailChange` (slices 5.5–5.7) — CritterMart's second `Wolverine.Saga`, EF-Core-backed, proving the saga store is swappable across a Marten and an EF Core BC.

Three event-sourced aggregates total (Cart, Order, Stock). One document type (Product). One relational entity (Customer). Two convention sagas (`Replenishment` on Marten, `EmailChange` on EF Core). The split is intentional. The project itself is the answer to "when should I reach for event sourcing and when shouldn't I?"

## Success criteria for round one

By the first talk delivery, CritterMart should support (all met — the round-one modeled implementation set shipped in full, June 2026):

- Browsing the catalog and viewing a product
- Adding items to a cart, with cart abandonment events captured from day one
- Checking out and starting an order
- Order placement that coordinates stock reservation and a stubbed payment authorization across bounded contexts
- Order completion when both gates close, or order cancellation if payment times out
- A working OpenTelemetry trace of the above flow spanning multiple services, visible in the Aspire dashboard
- Design artifacts for each slice: an OpenSpec proposal and a narrative, traceable to the resulting code
- One async projection somewhere in the codebase as a teaser for the "and you can also rebuild asynchronously" beat of the talk

The code does not need to be polished, comprehensive, or production-grade. It needs to be coherent enough that someone reading the repo can follow the journey from concept through implementation to insight.

## Long road

After round one ships, the candidate enhancements are open: giving the Identity service real authentication (the registry service itself already shipped — promoted from stub via Workshop 002; the auth mechanism is now **chosen and modeled** — [ADR 023](decisions/023-real-authentication-for-identity.md): **ASP.NET Core Identity** issuing a self-validated, asymmetrically-signed JWT the other services verify offline, modeled in Workshop 002 §§ 5.8–5.11, implementation the next increment; ADR 009's 2026-07-07 correction earlier struck a factual error attributing this to Polecat, a SQL Server document/event store unrelated to identity), broader async projection use for replay demonstrations, a separate BFF promoting the Wolverine.Http surface, a returns slice, a promotions slice using Dynamic Consistency Boundaries, multi-tenant scaffolding, richer frontend interactions. Two post-round-one increments have already landed on this road, both convention sagas proving `Wolverine.Saga` additive to PMvH and its storage backend swappable: the Inventory `Replenishment` saga (slices 2.5–2.7, Marten-backed) and the Identity `EmailChange` saga (slices 5.5–5.7, EF-Core-backed). The next increment is not yet chosen among the candidates above.

CritterMart is a vehicle. The talk is the destination for round one.
