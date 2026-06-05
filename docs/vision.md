# CritterMart — Vision

A single-seller storefront, built as a teaching reference architecture for event sourcing with the Critter Stack.

## What this is

CritterMart is an ecommerce reference application designed to demonstrate event sourcing patterns through a recognizable, approachable domain. It runs as three separate .NET 10 services — Catalog, Inventory, and Orders — that communicate via Wolverine over RabbitMQ; there is no synchronous service-to-service HTTP. Persistence is a shared PostgreSQL instance with schema-per-service, accessed through Marten. Wolverine.Http exposes each service's HTTP surface. A frontend client sits in front of the services as the customer-facing storefront: a Vite + React single-page application that calls each service's Wolverine.Http surface directly ([ADR 015](decisions/015-vite-react-frontend-stack.md)), modeled through the full SDD pipeline as first-class UI ([ADR 016](decisions/016-frontend-full-pipeline-ui-first-class.md)). .NET Aspire orchestrates the services, the broker, and the database locally, with OpenTelemetry tracing visible in the Aspire dashboard.

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
- No Polecat for round one; identity is intentionally minimal

Many of these cuts will make excellent follow-up blog posts and future enhancements. Cutting them is what keeps the demo, the talk, and the six-day timeline honest.

## Bounded contexts

Three bounded contexts, each deployed as its own service, with a fourth (Identity) intentionally stubbed for round one:

- **Catalog**: products, prices, descriptions. Marten document store. The "when CRUD is fine" example.
- **Inventory**: stock per SKU. Event sourced. Inline snapshot. The textbook case.
- **Orders**: Cart and Order, both event sourced. Order serves as the process manager for fulfilling a purchase, using the Process Manager via Handlers pattern.
- **Identity** *(stubbed)*: not a running service in round one. A customer identifier is hardcoded into the frontend; there is no auth flow, no customer record store, and no Identity service deployed. Promoting Identity into a real service backed by Polecat is queued in the Long Road section.

Three event-sourced aggregates total (Cart, Order, Stock). One document type (Product). The split is intentional. The project itself is the answer to "when should I reach for event sourcing and when shouldn't I?"

## Success criteria for round one

By the first talk delivery, CritterMart should support:

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

After round one ships, the candidate enhancements are open: promoting the stubbed Identity context into a real service backed by Polecat, broader async projection use for replay demonstrations, a separate BFF promoting the Wolverine.Http surface, a returns slice, a promotions slice using Dynamic Consistency Boundaries, multi-tenant scaffolding, richer frontend interactions. None of those are in scope this week. All of them could be.

CritterMart is a vehicle. The talk is the destination this week.
