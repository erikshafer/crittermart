# CritterMart

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Marten-336791?logo=postgresql&logoColor=white)](https://martendb.io/)
[![Wolverine](https://img.shields.io/badge/Wolverine-6%2B-512BD4)](https://wolverine.netlify.app/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-4.x-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> An open-source single-seller ecommerce reference architecture and conference-talk anchor, built on the [Critter Stack](https://wolverine.netlify.app/) and exercising a disciplined spec-driven development pipeline.

---

## What Is CritterMart?

CritterMart is a teaching reference architecture for **event sourcing with the Critter Stack** — JasperFx's family of .NET libraries including [Wolverine](https://wolverine.netlify.app/) (messaging and command handling) and [Marten](https://martendb.io/) (event sourcing + document storage over PostgreSQL). The project's two purposes are explicit and in priority order:

1. **It is a working example for an Event Sourcing with Marten talk.** CritterMart code and design artifacts carry the pedagogical weight, with the Place Order journey as the centerpiece.
2. **It is a sandbox for a disciplined Spec-Driven Development pipeline.** Each vertical slice is produced via Context Mapping → Event Modeling workshop → OpenSpec proposal + sibling narrative → prompt → execute + retrospective, in that order, before any code is written. The pipeline doubles as the talk's section on what AI-assisted .NET development looks like in 2026.

CritterMart sits alongside other Critter-family reference architectures (CritterBids for auctions, CritterCab for ride-sharing, CritterSupply for broader ecommerce). It is deliberately **the smallest of them in domain scope** — single-seller storefront, four bounded contexts, one of which is stubbed — and the most deliberate about exposing its own design pipeline as a first-class artifact.

The codebase exists to be **read, demonstrated, and learned from**. It is not a complete ecommerce platform, and it is not meant for production use.

---

## Why a Single-Seller Storefront?

Ecommerce is a domain most developers intuitively understand — everyone has placed an order online. That familiarity lets the talk focus on *how* the system is built rather than getting bogged down explaining *what* it does. The single-seller variant is the smallest cut of ecommerce that still exercises the patterns event sourcing teaches well:

| Pattern | How CritterMart demonstrates it |
|---|---|
| **Event sourcing** | Stock per SKU (Inventory) and Cart + Order (Orders) are event-sourced; Catalog uses Marten's document store for contrast — "when CRUD is fine" |
| **Process Manager via Handlers (PMvH)** | The Order aggregate **is** the process manager — no separate saga state stream, no `Wolverine.Saga` base class; state guards on the stream enforce idempotency |
| **Convention `Wolverine.Saga`** | Two sagas prove the pattern additive to PMvH and its store swappable: Inventory's `Replenishment` (slices 2.5–2.7, Marten-backed, SKU-keyed) and Identity's `EmailChange` (slices 5.5–5.7, EF-Core-backed, `CustomerId`-keyed, HTTP-driven) |
| **Klefter translation-decision events** | The Order stream commits external decisions (Inventory's grant/refusal, the stubbed payment provider's response) as first-class events on its own stream — audit trail by construction |
| **Bruun temporal automation** | `CartAbandoned` via scheduled `CartActivityTimeout`; `OrderCancelled` via `OrderPaymentTimeout` — both fire on clock conditions, with todo-list projections marked by the asterisk convention |
| **Cross-BC integration via brokered messaging** | Orders ↔ Inventory via Wolverine over RabbitMQ; no synchronous service-to-service HTTP |
| **Mixed projection lifecycles** | Most projections inline (`StockLevelView`, `CartView`, `OrderStatusView`); one async projection (`CartAbandonmentReport`) as a teaser for the rebuild-on-demand demo beat |
| **OpenTelemetry cross-service tracing** | Full Place Order trace spans Orders → RabbitMQ → Inventory and back — visible in the .NET Aspire dashboard |
| **Spec-Driven Development pipeline** | Vision → context map → workshop → OpenSpec proposal + narrative → prompt → execute + retrospective, with every step version-controlled |

The full slice list (17 round-one slices across the four BCs, plus the post-round-one replenishment-saga increment 2.5–2.7) lives in [`docs/workshops/001-crittermart-event-model.md`](docs/workshops/001-crittermart-event-model.md); the Identity BC carries its own model in [`docs/workshops/002-identity-event-model.md`](docs/workshops/002-identity-event-model.md).

---

## Architecture

CritterMart deploys as **four .NET 10 services** — Catalog, Inventory, Orders, and Identity. Cross-service communication is Wolverine over RabbitMQ; there is no synchronous service-to-service HTTP. Persistence is a shared PostgreSQL database with schema-per-service, accessed through Marten. .NET Aspire orchestrates the services, broker, and database locally, with OpenTelemetry tracing visible in the Aspire dashboard.

### Bounded Contexts

| Bounded Context | Responsibility | Storage | Status |
|---|---|---|---|
| **Catalog** | Products, prices, descriptions | Marten document store | Implemented — slices 1.1–1.3 (publish, browse, change price) |
| **Inventory** | Stock per SKU, reservations, commitments, backorder replenishment | Event-sourced (Marten) | Implemented — slices 2.1 receive, 2.2 reserve, 4.2 cross-BC reserve (responds to Orders' `ReserveStock` over RabbitMQ, all-or-nothing per order), 2.3 release on cancellation (responds to Orders' `ReleaseStock`, per-SKU idempotent), 2.4 commit reserved stock on order confirmation (responds to Orders' `CommitStock`, per-SKU idempotent; invariant `Available + Reserved + Committed = ΣReceived`), 2.5–2.7 `Replenishment` saga (CritterMart's first convention `Wolverine.Saga` — opened on backorder detection, closed on restock arrival or escalated on timeout; saga state in Marten saga storage, not on the Stock stream) |
| **Orders** | Cart, Order (process manager), payment timeout, cart abandonment | Event-sourced (Marten) | Implemented — **both aggregates complete**. Order lifecycle: 4.1 placement, 4.2 / 4.5 cross-BC stock reservation + cancel-on-stock-failure, 4.3 / 4.4 stubbed payment authorization + confirmation (confirmation cascades `CommitStock` to Inventory via slice 2.4), 4.6 cancel-on-payment-decline, 4.7 cancel-on-payment-timeout with the `OrdersAwaitingPayment` Bruun todo-list. Cart lifecycle: 3.1 add-to-cart, 3.2 / 3.3 cart edits (remove item, change quantity — SKU-keyed lines), 3.4 abandonment on inactivity (fire-and-check temporal automation, `CartsAwaitingActivity` todo-list, and the `CartAbandonmentReport` async-projection teaser — rebuild-on-demand, no daemon) |
| **Identity** | Customer registry + auth issuer | EF Core + Npgsql (schema `identity`) + ASP.NET Core Identity | Implemented — slices 5.1 (register customer, email-uniqueness guard), 5.2 (resolve by email); publishes `CustomerRegistered` over RabbitMQ (slices 5.3/5.4 — Orders consumes and maintains a local customer view); 5.5–5.7 `EmailChange` saga (CritterMart's second convention `Wolverine.Saga`, EF-Core-backed — request/confirm/timeout-drop, proves the saga store is swappable); 5.8–5.11 real authentication ([ADR 023](docs/decisions/023-real-authentication-for-identity.md)) — ASP.NET Core Identity + a self-validated JWT (`sub` claim is the trust boundary), `POST /register` / `POST /login` / `POST /logout`; Open-Host Service (`GET /customers/{id}`, storefront-facing) + Published Language (`CustomerRegistered` in `CritterMart.Contracts`). No Polecat ([ADR 009](docs/decisions/009-polecat-deferred-for-round-one.md)); the frontend now sends `Authorization: Bearer`, and `X-Customer-Id` survives only as a dev-only fallback on Orders, DEBT-tracked for removal |

Catalog has no BC-level integration with Inventory or Orders in round one — product fields cross only via the frontend, which snapshots them into Cart commands at add-to-cart time. The Orders ↔ Inventory relationship is **Customer-Supplier** (Inventory is the supplier, Orders the customer). Identity is an **Open-Host Service + Published Language** — it exposes a storefront-facing read API (`GET /customers/{id}`) and publishes `CustomerRegistered` over RabbitMQ; Orders consumes it and maintains a local customer view (slices 5.3/5.4). Identity is also the auth issuer ([ADR 023](docs/decisions/023-real-authentication-for-identity.md)): ASP.NET Core Identity + a self-validated JWT, with the `sub` claim as the trust boundary; the frontend sends `Authorization: Bearer`, and `X-Customer-Id` survives only as a dev-only fallback on Orders. See [`docs/context-map/README.md`](docs/context-map/README.md) for the full topology, integration relationships table, and round-one stubs.

---

## Tech Stack

| Layer | Choice |
|---|---|
| Runtime | C# 14 / .NET 10 |
| Messaging | [Wolverine](https://wolverine.netlify.app/) 6 over RabbitMQ (pinned to CritterWatch's build target — see the pin note in `Directory.Packages.props`) |
| Persistence | [Marten](https://martendb.io/) 9+ on PostgreSQL (Catalog, Inventory, Orders); EF Core + Npgsql (Identity) — shared database, schema-per-service |
| HTTP | Wolverine.Http (per service; no BFF for round one) |
| Testing | [Alba](https://jasperfx.github.io/alba/) (integration), xUnit + Shouldly (unit) |
| Orchestration | [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) |
| Observability | OpenTelemetry (traces surface in the Aspire dashboard) + [CritterWatch](https://jasperfx.net/) monitoring console — per [ADR 017](docs/decisions/017-critterwatch-integrated.md) |
| Frontend | Vite + React SPA (TypeScript, TanStack Query, Tailwind v4, shadcn/ui) — per [ADR 015](docs/decisions/015-vite-react-frontend-stack.md) |

The full tech-stack rationale lives in the round-one ADRs at [`docs/decisions/`](docs/decisions/) and is summarized terse-form in [`docs/rules/structural-constraints.md`](docs/rules/structural-constraints.md).

---

## The Talk

The talk — *Event Sourcing with Marten* — is the project's centerpiece and the destination for round one. 50-minute slot with a 30-minute cut, no live coding. The Place Order journey is rendered as a full OpenTelemetry-traced storyboard: a customer taps "Place Order," the trace fans across Orders → RabbitMQ → Inventory and back, the Order aggregate's process manager logic gates on `StockReserved` and `PaymentAuthorized`, and the order either confirms or cancels through one of three failure paths (insufficient stock, payment declined, or payment timeout).

What the demo shows:

- A working OpenTelemetry trace spanning multiple services, end-to-end in the Aspire dashboard
- The Order aggregate acting as its own process manager (no separate saga stream)
- Klefter translation-decision events capturing external decisions as local stream facts
- Bruun temporal automation handling `CartAbandoned` and `OrderPaymentTimeout`
- One async projection (`CartAbandonmentReport`) demonstrating rebuild-on-demand
- The full design pipeline visible in `docs/` — vision through workshop through slice spec through implementation prompt

The pipeline itself doubles as the talk's section on what AI-assisted .NET development looks like in 2026: each artifact is a durable record of intent, and any session-runner — human or AI — can pick up where the last one left off.

---

## Getting Started

> **Status note.** CritterMart's **round-one modeled implementation set is complete** — every slice in [Workshop 001](docs/workshops/001-crittermart-event-model.md) has shipped. The design artifact suite (vision, context map, workshop, ADRs, rules, folder READMEs, skills, prompts, retrospectives) is complete, and `src/` holds four services (Catalog, Inventory, and Orders on Wolverine + Marten; Identity on Wolverine + EF Core — the one non-event-sourced BC), the `CritterMart.AppHost` Aspire orchestrator, `CritterMart.ServiceDefaults`, `CritterMart.Contracts` (the cross-BC published language), and `CritterMart.Seeding` (demo auto-seeder). Catalog (1.1–1.3) and Inventory (2.1–2.4, plus the post-round-one 2.5–2.7 `Replenishment` saga) are in; the Orders BC's **Order lifecycle is complete** (4.1 place-order, 4.2/4.5 cross-BC stock reservation + cancel-on-stock-failure — the project's first live RabbitMQ traffic — 4.3/4.4 stubbed payment authorization + confirmation cascading `CommitStock` to Inventory (slice 2.4), 4.6 cancel-on-payment-decline with cross-BC stock release, and 4.7 cancel-on-payment-timeout — the project's first temporal automation; every placed order reaches `confirmed` or `cancelled`); and the **Cart lifecycle is complete** (3.1 add-to-cart, 3.2/3.3 cart edits with SKU-keyed lines, and 3.4 abandonment on inactivity — fire-and-check temporal automation plus the `CartAbandonmentReport` async-projection teaser, rebuild-on-demand with no daemon per ADR 008; every cart reaches `CartCheckedOut` or `CartAbandoned`). Beyond the modeled set, the storefront SPA (Vite + React, ADRs 015/016) and CritterWatch monitoring console (ADR 017) have shipped, and three post-round-one increments are implemented and archived into their specs: the Inventory `Replenishment` saga (slices 2.5–2.7, Marten-backed, `stock-management` spec) and the Identity `EmailChange` saga (slices 5.5–5.7, EF-Core-backed, `customer-registry` spec) — the pair proving `Wolverine.Saga` is additive to PMvH and its storage backend is swappable — plus real authentication for Identity ([ADR 023](docs/decisions/023-real-authentication-for-identity.md): ASP.NET Core Identity + self-validated JWT, slices 5.8–5.11, also archived into `customer-registry`). The next post-round-one direction is open among the remaining candidates; see [`docs/vision.md`](docs/vision.md) § Long road. The instructions below reflect the working local-development workflow.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22+](https://nodejs.org/) — Aspire launches the Vite dev server (storefront SPA) automatically; no separate `npm run dev` needed
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or an equivalent OCI runtime) — Aspire orchestrates PostgreSQL and RabbitMQ as containers
- An IDE with C# tooling (JetBrains Rider, Visual Studio, or VS Code with the C# Dev Kit)

### AI session tooling

CritterMart's design pipeline assumes an AI session-runner (Claude Code, in author practice). A fresh machine needs the following before a session can pick up where the last one left off:

- **[Claude Code](https://www.claude.com/product/claude-code)** (or another agent capable of reading `CLAUDE.md`, the local skills directories, and the `docs/` artifacts). The repo commits a shared **`.claude/settings.json`** with a permissions allow-list for the project's Docker, dotnet, and `gh` tooling so a fresh clone starts with a sensible baseline; personal preferences (model, output style, effort level) belong in `.claude/settings.local.json` (per-machine, gitignored) or `~/.claude/settings.json` (user-global).
- **[GitHub CLI (`gh`)](https://cli.github.com/)** — the session pipeline opens PRs via `gh pr create` and reads PR state via `gh api`. Authenticate once per machine with `gh auth login`.
- **[`ctx7`](https://context7.com/)** — `npx ctx7@latest` fetches current docs for any library the session calls into; preferred over web search for API and configuration questions, and used by Erik's global Claude Code rules.
- **JasperFx Critter Stack ai-skills library**, installed per the upstream instructions at [ai-skills.jasperfx.net/install](https://ai-skills.jasperfx.net/install). These are the *knowledge* skills (Marten projections, Wolverine handlers, Polecat setup, EF Core integration, testing patterns) that `CLAUDE.md`'s routing layer defers to for component-scoped patterns.
- **Matt Pocock workflow skills**, installed via `npx skills@latest add mattpocock/skills` (source: [github.com/mattpocock/skills](https://github.com/mattpocock/skills)). These are the *workflow* skills (`grill-me`, `diagnose`, `handoff`, `tdd`, `prototype`, `improve-codebase-architecture`, etc.) that complement the JasperFx knowledge skills.

Both skill collections install globally (to `~/.claude/skills/` and a universal mirror) and are discovered by Claude Code at session start with no per-project registration. Each machine installs them independently — they are not vendored into this repo. The two installers have independent upgrade cycles; do not try to consolidate them.

### Run locally

.NET Aspire boots PostgreSQL, RabbitMQ, all four services, the auto-seeder, and the Vite dev server together:

```bash
dotnet run --project src/CritterMart.AppHost --launch-profile http
```

Key URLs once the stack is healthy:

| Surface | URL |
|---|---|
| Aspire dashboard (traces, logs, resources) | `http://localhost:15090/login?t=<token>` — token printed in the console on each boot |
| Storefront SPA | `http://localhost:5273` |
| Catalog API / Swagger | `http://localhost:5101` |
| Inventory API / Swagger | `http://localhost:5102` |
| Orders API / Swagger | `http://localhost:5103` |
| Identity API / Swagger | `http://localhost:5105` |
| CritterWatch console | dynamic — open from the Aspire dashboard's `critterwatch-console` resource |

For the full **boot → seed → drive an order → verify every surface (Swagger, Aspire dashboard, CritterWatch console, storefront SPA) → teardown** procedure — repeatable by a human or an AI agent — see the [Demo & Smoke-Test Runbook](docs/demo-runbook.md). The canonical entry point for the design pipeline remains [`CLAUDE.md`](CLAUDE.md).

---

## Repository Structure

```
CritterMart/
├── docs/                       # The heart of the project right now
│   ├── vision.md               # Canonical source of truth — purpose, BCs, non-goals
│   ├── context-map/            # Cross-BC DDD relationships and topology
│   ├── workshops/              # Event Modeling output (round-one rolled-up model)
│   ├── narratives/             # NDD-informed journey specs (per slice)
│   ├── decisions/              # Round-one ADRs
│   ├── rules/                  # AI-optimized structural constraints
│   ├── skills/                 # Component-scoped patterns (five local skills; defers to upstream)
│   ├── prompts/                # Per-session intent records, frozen at session start
│   ├── retrospectives/         # Per-session outcome records, spec-delta closure
│   ├── research/               # Spikes and exploratory work
│   ├── handoffs/               # Durable session-to-session handoff docs
│   ├── demo-runbook.md         # Boot → seed → drive → verify → teardown procedure
│   └── demo-traffic.ps1        # Scripted demo traffic generator
├── openspec/                   # OpenSpec workspace (peer to docs/) — CLI-managed
│   ├── changes/                # Per-slice changes (proposal.md + SHALL specs); archive/ holds shipped changes
│   └── specs/                  # Main specs, synced from a change on archive
├── src/                        # Catalog, Inventory, Orders, Identity services + AppHost + ServiceDefaults + Contracts (cross-BC published language) + Seeding (demo auto-seeder) + CritterWatch (monitoring console host)
├── tests/                      # Per-service test projects + CrossBc.Tests (two-host cross-BC smoke)
├── CLAUDE.md                   # AI development entry point and pipeline overview
├── LICENSE                     # MIT
└── README.md                   # You are here
```

---

## Documentation

| Document | Purpose |
|---|---|
| [`docs/vision.md`](docs/vision.md) | Canonical source of truth — purpose, bounded contexts, non-goals |
| [`docs/context-map/README.md`](docs/context-map/README.md) | Cross-BC DDD relationships and integration topology |
| [`docs/workshops/`](docs/workshops/README.md) | Event Modeling output ([round-one rolled-up model](docs/workshops/001-crittermart-event-model.md), [Identity model](docs/workshops/002-identity-event-model.md)) |
| [`docs/narratives/`](docs/narratives/README.md) | NDD-informed journey specs (per slice) |
| [`openspec/changes/`](openspec/changes/) | OpenSpec proposals + per-capability SHALL specs (per slice, CLI-managed) |
| [`docs/decisions/`](docs/decisions/) | Round-one ADRs (indexed in the folder README) |
| [`docs/rules/structural-constraints.md`](docs/rules/structural-constraints.md) | AI-optimized terse imperative list of architectural constraints |
| [`docs/skills/`](docs/skills/README.md) | Component-scoped patterns (five local skills — `event-modeling`, `frontend`, `marten-projection-conventions`, `updating-critter-stack-dependencies`, `wolverine-cross-bc-cascading`; generic stack mechanics defer to upstream) |
| [`docs/prompts/`](docs/prompts/README.md) | Per-session intent records |
| [`docs/retrospectives/`](docs/retrospectives/README.md) | Per-session outcome records |
| [`docs/handoffs/`](docs/handoffs/) | Durable session-to-session handoffs (current: [Saga #2](docs/handoffs/saga2-handoff.md)) |
| [`docs/demo-runbook.md`](docs/demo-runbook.md) | Boot → seed → drive an order → verify every surface → teardown |
| [`CLAUDE.md`](CLAUDE.md) | AI development entry point, pipeline overview, and routing layer |

---

## Spec-Driven Development Pipeline

CritterMart's second deliberate purpose is exercising a disciplined SDD pipeline as a teaching artifact in its own right. Every vertical slice flows through:

1. **Context Mapping** (cross-BC DDD vocabulary) — [`docs/context-map/README.md`](docs/context-map/README.md), amended as new BCs appear
2. **Event Modeling workshop** (one rolled-up artifact for round one) — [`docs/workshops/001-crittermart-event-model.md`](docs/workshops/001-crittermart-event-model.md)
3. **OpenSpec proposal + Narrative siblings** (per slice, must agree) — `openspec/changes/{change}/proposal.md` + `docs/narratives/NNN-{actor}-{journey}.md`
4. **Prompt** (per session, frozen at session start) — [`docs/prompts/README.md`](docs/prompts/README.md)
5. **Execute + Retrospective** (per session, retro before PR opens) — [`docs/retrospectives/README.md`](docs/retrospectives/README.md)

Strong operating disciplines hold the pipeline together: **one prompt = one session = one PR**; **no opportunistic edits**; **spec-delta closure loop** (every prompt names its spec delta, every retro confirms whether it landed); **design-return cadence** after 2–3 implementation PRs against a single bounded context. Full treatment in [`CLAUDE.md`](CLAUDE.md).

---

## Round-One Scope

**In scope** (per [`docs/vision.md`](docs/vision.md) success criteria):

- Browse the catalog and view a product
- Add items to a cart, with cart abandonment events captured from day one
- Check out and start an order
- Order placement coordinating stock reservation and stubbed payment authorization across BCs
- Order completion when both gates close, or cancellation on stock failure / payment decline / payment timeout
- A working OpenTelemetry trace of the Place Order journey, visible in the Aspire dashboard
- Per-slice design artifacts (OpenSpec proposal + sibling narrative)
- One async projection (`CartAbandonmentReport`) as a teaser for the rebuild-on-demand demo beat

**Deliberately out** (per [`docs/vision.md`](docs/vision.md) § What this deliberately is not):

- No vendor portal, vendor identity, marketplace listings, or multi-channel sales
- No backoffice or admin UI
- No real payment integration (payment is stubbed inside Orders)
- No returns, no promotions, no shipping rate calculations, no real-time storefront updates
- No Polecat for round one; customer authentication is stubbed (`X-Customer-Id` header hardcoded in the frontend) — the Identity registry service is real but carries no authN/authZ
- No live coding in the demo

Many of the cuts are explicit candidates for future rounds, tracked in [`docs/vision.md`](docs/vision.md) § Long road and [`docs/context-map/README.md`](docs/context-map/README.md) § Long road.

---

## Companion Library: JasperFx ai-skills

CritterMart defers to the [JasperFx ai-skills library](http://ai-skills.jasperfx.net/) for generic Critter Stack patterns (Wolverine, Marten, Polecat). Local skills under [`docs/skills/`](docs/skills/) are authored only when a CritterMart-specific convention diverges from upstream, or when a project-specific methodology needs its own home — five exist today: [`event-modeling`](docs/skills/event-modeling/SKILL.md), `frontend`, `marten-projection-conventions`, `updating-critter-stack-dependencies`, and `wolverine-cross-bc-cascading`. The project does not duplicate upstream content. See [`docs/skills/README.md`](docs/skills/README.md) for the layering rationale.

---

## Contributing

CritterMart's session-driven workflow is documented in [`docs/prompts/README.md`](docs/prompts/README.md) and [`docs/retrospectives/README.md`](docs/retrospectives/README.md). Before contributing:

- Read [`CLAUDE.md`](CLAUDE.md) for the pipeline overview, architectural non-negotiables, and operating disciplines.
- For implementation work, follow the **one-prompt = one-session = one-PR** rhythm: author a prompt that names the spec delta, execute, write the retro confirming closure (or honest non-closure), open a PR carrying all three.
- Branches follow `{type}/{slug}` matching the conventional-commit prefix (e.g., `tidy/docs-folder-readmes` paired with `tidy: docs — add folder READMEs for routing-layer narrowing`).
- All session intent and outcomes are version-controlled in `docs/prompts/` and `docs/retrospectives/`.

---

## Resources

- **Blog:** [event-sourcing.dev](https://www.event-sourcing.dev)
- **Wolverine:** [wolverine.netlify.app](https://wolverine.netlify.app/)
- **Marten:** [martendb.io](https://martendb.io/)
- **JasperFx:** [github.com/jasperfx](https://github.com/jasperfx)
- **Tools:** [JetBrains Rider](https://www.jetbrains.com/rider/), [DataGrip](https://www.jetbrains.com/datagrip/)

---

## Maintainer

**Erik "Faelor" Shafer**

[LinkedIn](https://www.linkedin.com/in/erikshafer/) · [Blog](https://www.event-sourcing.dev) · [YouTube](https://www.youtube.com/@event-sourcing) · [Bluesky](https://bsky.app/profile/erikshafer.bsky.social)
