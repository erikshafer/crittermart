# CritterMart

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Marten-336791?logo=postgresql&logoColor=white)](https://martendb.io/)
[![Wolverine](https://img.shields.io/badge/Wolverine-5%2B-512BD4)](https://wolverine.netlify.app/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-4.x-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> An open-source single-seller ecommerce reference architecture and conference-talk anchor, built on the [Critter Stack](https://wolverine.netlify.app/) and exercising a disciplined spec-driven development pipeline.

---

## What Is CritterMart?

CritterMart is a teaching reference architecture for **event sourcing with the Critter Stack** — JasperFx's family of .NET libraries including [Wolverine](https://wolverine.netlify.app/) (messaging and command handling) and [Marten](https://martendb.io/) (event sourcing + document storage over PostgreSQL). The project's two purposes are explicit and in priority order:

1. **It is the working example for an Event Sourcing with Marten talk.** First delivered at ImprovingU (round one), then to an online .NET user group (round two). 50-minute talk, 30-minute cut, no live coding — CritterMart code and design artifacts carry the pedagogical weight, with the Place Order journey as the centerpiece.
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
| **Klefter translation-decision events** | The Order stream commits external decisions (Inventory's grant/refusal, the stubbed payment provider's response) as first-class events on its own stream — audit trail by construction |
| **Bruun temporal automation** | `CartAbandoned` via scheduled `CartActivityTimeout`; `OrderCancelled` via `OrderPaymentTimeout` — both fire on clock conditions, with todo-list projections marked by the asterisk convention |
| **Cross-BC integration via brokered messaging** | Orders ↔ Inventory via Wolverine over RabbitMQ; no synchronous service-to-service HTTP |
| **Mixed projection lifecycles** | Most projections inline (`StockLevelView`, `CartView`, `OrderStatusView`); one async projection (`CartAbandonmentReport`) as a teaser for the rebuild-on-demand demo beat |
| **OpenTelemetry cross-service tracing** | Full Place Order trace spans Orders → RabbitMQ → Inventory and back — visible in the .NET Aspire dashboard |
| **Spec-Driven Development pipeline** | Vision → context map → workshop → OpenSpec proposal + narrative → prompt → execute + retrospective, with every step version-controlled |

The full slice list (17 round-one slices across the four BCs) lives in [`docs/workshops/001-crittermart-event-model.md`](docs/workshops/001-crittermart-event-model.md).

---

## Architecture

CritterMart deploys as **three separate .NET 10 services** — Catalog, Inventory, and Orders — plus a stubbed Identity context. Cross-service communication is Wolverine over RabbitMQ; there is no synchronous service-to-service HTTP. Persistence is a shared PostgreSQL database with schema-per-service, accessed through Marten. .NET Aspire orchestrates the services, broker, and database locally, with OpenTelemetry tracing visible in the Aspire dashboard.

### Bounded Contexts

| Bounded Context | Responsibility | Storage | Status |
|---|---|---|---|
| **Catalog** | Products, prices, descriptions | Marten document store | Workshop complete; implementation forthcoming |
| **Inventory** | Stock per SKU, reservations | Event-sourced (Marten) | Workshop complete; implementation forthcoming |
| **Orders** | Cart, Order (process manager), payment timeout | Event-sourced (Marten) | Workshop complete; implementation forthcoming |
| **Identity** *(stubbed)* | Customer identifier | Hardcoded in frontend (round one) | Stubbed by design; deployed-service promotion queued in [vision.md § Long road](docs/vision.md) |

Catalog has no BC-level integration with Inventory or Orders in round one — product fields cross only via the frontend, which snapshots them into Cart commands at add-to-cart time. The Orders ↔ Inventory relationship is **Customer-Supplier** (Inventory is the supplier, Orders the customer). Identity's relationship to the three deployed services is **Conformist** with no active wire integration. See [`docs/context-map/README.md`](docs/context-map/README.md) for the full topology, integration relationships table, and round-one stubs.

---

## Tech Stack

| Layer | Choice |
|---|---|
| Runtime | C# 14 / .NET 10 |
| Messaging | [Wolverine](https://wolverine.netlify.app/) 5+ over RabbitMQ |
| Persistence | [Marten](https://martendb.io/) 9+ on PostgreSQL (shared database, schema-per-service) |
| HTTP | Wolverine.Http (per service; no BFF for round one) |
| Testing | [Alba](https://jasperfx.github.io/alba/) (integration), xUnit + Shouldly (unit) |
| Orchestration | [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) |
| Observability | OpenTelemetry (traces surface in the Aspire dashboard) |
| Frontend | TBD for round one |

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

> **Status note.** CritterMart is in the **design phase** for round one. The full design artifact suite (vision, context map, workshop, ADRs, rules, folder READMEs, skills, prompts, retrospectives) is complete; the `src/` tree is intentionally empty and will populate per slice as the per-slice implementation loop runs. The instructions below reflect the intended local-development workflow once `src/CritterMart.AppHost` and the per-service projects are in place.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
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

### Run locally (intended workflow)

Once the AppHost project lands, .NET Aspire will boot PostgreSQL, RabbitMQ, and the three services together:

```bash
dotnet run --project src/CritterMart.AppHost
```

The Aspire dashboard will surface OpenTelemetry traces for the Place Order journey across all three services. Until the AppHost is in place, the project is a documentation-and-design artifact — the canonical entry point is [`CLAUDE.md`](CLAUDE.md), not a running app.

---

## Repository Structure

```
CritterMart/
├── docs/                       # The heart of the project right now
│   ├── vision.md               # Canonical source of truth — purpose, BCs, non-goals
│   ├── context-map/            # Cross-BC DDD relationships and topology
│   ├── workshops/              # Event Modeling output (round-one rolled-up model)
│   ├── narratives/             # NDD-informed journey specs (per slice, forthcoming)
│   ├── specs/                  # OpenSpec proposals (per slice, forthcoming)
│   ├── decisions/              # 10 round-one ADRs
│   ├── rules/                  # AI-optimized structural constraints
│   ├── skills/                 # Component-scoped patterns (event-modeling skill local)
│   ├── prompts/                # Per-session intent records, frozen at session start
│   ├── retrospectives/         # Per-session outcome records, spec-delta closure
│   └── research/               # Spikes and exploratory work
├── src/                        # Forthcoming — per-service projects land per slice
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
| [`docs/workshops/`](docs/workshops/README.md) | Event Modeling output ([round-one rolled-up model](docs/workshops/001-crittermart-event-model.md)) |
| [`docs/narratives/`](docs/narratives/README.md) | NDD-informed journey specs (per slice, forthcoming) |
| [`docs/specs/`](docs/specs/) | OpenSpec proposals (per slice, forthcoming) |
| [`docs/decisions/`](docs/decisions/) | 10 round-one ADRs |
| [`docs/rules/structural-constraints.md`](docs/rules/structural-constraints.md) | AI-optimized terse imperative list of architectural constraints |
| [`docs/skills/`](docs/skills/README.md) | Component-scoped patterns (one current local skill: `event-modeling`; others defer to upstream) |
| [`docs/prompts/`](docs/prompts/README.md) | Per-session intent records |
| [`docs/retrospectives/`](docs/retrospectives/README.md) | Per-session outcome records |
| [`CLAUDE.md`](CLAUDE.md) | AI development entry point, pipeline overview, and routing layer |

---

## Spec-Driven Development Pipeline

CritterMart's second deliberate purpose is exercising a disciplined SDD pipeline as a teaching artifact in its own right. Every vertical slice flows through:

1. **Context Mapping** (cross-BC DDD vocabulary) — [`docs/context-map/README.md`](docs/context-map/README.md), amended as new BCs appear
2. **Event Modeling workshop** (one rolled-up artifact for round one) — [`docs/workshops/001-crittermart-event-model.md`](docs/workshops/001-crittermart-event-model.md)
3. **OpenSpec proposal + Narrative siblings** (per slice, must agree) — `docs/specs/{slice}/proposal.md` + `docs/narratives/NNN-{actor}-{journey}.md`
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
- No Polecat for round one; Identity is stubbed (customer ID hardcoded in the frontend)
- No live coding in the demo

Many of the cuts are explicit candidates for future rounds, tracked in [`docs/vision.md`](docs/vision.md) § Long road and [`docs/context-map/README.md`](docs/context-map/README.md) § Long road.

---

## Companion Library: JasperFx ai-skills

CritterMart defers to the [JasperFx ai-skills library](https://github.com/jasperfx/ai-skills) for generic Critter Stack patterns (Wolverine, Marten, Polecat). Local skills under [`docs/skills/`](docs/skills/) are authored only when a CritterMart-specific convention diverges from upstream, or when a project-specific methodology needs its own home (the in-repo [`event-modeling`](docs/skills/event-modeling/SKILL.md) skill is the current example). The project does not duplicate upstream content. See [`docs/skills/README.md`](docs/skills/README.md) for the layering rationale.

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
