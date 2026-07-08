# docs/decisions/

Architecture Decision Records (ADRs) — the durable log of significant architectural decisions. See [CLAUDE.md § ADRs](../../CLAUDE.md) for the routing-layer treatment of where decisions sit in the pipeline.

An ADR records a decision when it is made, so the reasoning survives the moment. Unlike the per-session prompt/retro pair (which captures *session* intent and outcome), an ADR captures a *cross-cutting* decision that outlives any one session and that later contributors would otherwise re-derive.

## When to capture an ADR

Per CLAUDE.md, capture an ADR when **any** of these holds:

- (a) reversing the decision would require touching multiple bounded contexts,
- (b) the tradeoff is non-obvious, or
- (c) the next contributor would otherwise have to re-derive the reasoning.

Below that bar, the decision lives in the prompt/retro pair that made it — not here. Resist minting an ADR for a choice that only affects one slice's internals.

## File naming

`NNN-{slug}.md` where `NNN` is the ADR's number (zero-padded, monotonic) and `{slug}` is a short kebab-case identifier (e.g., `007-process-manager-via-handlers-for-order.md`).

## Format

The house format is terse and prose-shaped:

- Top-line title `# ADR NNN: {Title}`.
- `**Status**: {Accepted | Proposed | Superseded}` immediately under the title.
- `## Context` — the situation and the question.
- `## Decision` — what was chosen.
- `## Consequences` — what follows, including costs, risks, and rejected alternatives folded into prose.

ADRs are append-only. A later ADR may **supersede** an earlier one (mark the old one's status `Superseded` and cross-reference), but the original is not deleted or rewritten — the log is the history.

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [001](001-separate-services-topology.md) | Separate Services Topology | Accepted |
| [002](002-shared-postgres-schema-per-service.md) | Shared PostgreSQL with Schema-per-Service | Accepted |
| [003](003-wolverine-rabbitmq-transport.md) | Wolverine Messaging with RabbitMQ Transport | Accepted |
| [004](004-dotnet-aspire-orchestrator.md) | .NET Aspire as Orchestrator | Accepted |
| [005](005-opentelemetry-tracing-enabled.md) | OpenTelemetry Tracing Enabled | Accepted |
| [006](006-wolverine-http-per-service-no-bff.md) | Wolverine.Http API Surface per Service, No Separate BFF for Round One | Accepted |
| [007](007-process-manager-via-handlers-for-order.md) | Process Manager via Handlers for the Order Aggregate | Accepted |
| [008](008-inline-projections-async-teaser-no-daemon.md) | Inline Snapshot Projections, One Async Teaser, No Daemon for Round One | Accepted |
| [009](009-polecat-deferred-for-round-one.md) | Polecat Deferred for Round One | Accepted |
| [010](010-openspec-narrative-sibling-pipeline.md) | OpenSpec + Sibling Narrative for the SDD Pipeline | Accepted |
| [011](011-openspec-cli-grain-aware-layered-integration.md) | openspec CLI as Proposal Tooling, Grain-Aware Layered Integration | Accepted |
| [012](012-critter-stack-2026-upgrade.md) | Critter Stack 2026 Upgrade (Wolverine 6 / Marten 9) | Accepted |
| [013](013-critterwatch-deferred-to-messaging-slices.md) | CritterWatch Deferred to the 4.x Messaging Slices | Accepted |
| [014](014-published-language-contracts-project.md) | Published-Language Cross-BC Contracts in a Shared `CritterMart.Contracts` Project | Accepted |
| [015](015-vite-react-frontend-stack.md) | Vite + React SPA as the Round-Two Frontend Stack | Accepted |
| [016](016-frontend-full-pipeline-ui-first-class.md) | Frontend Modeled Through the Full Pipeline — UI First-Class in the Event Model | Accepted |
| [017](017-critterwatch-integrated.md) | CritterWatch Integrated — Out-of-Band Trial, Single-Node, nuget.org-Sourced | Accepted |
| [018](018-frontend-three-services-cors-posture.md) | Frontend-to-Three-Services Dev-Server + CORS Posture | Accepted |
| [019](019-wolverine-health-checks-exposed.md) | Wolverine Runtime Health Exposed via ASP.NET Health Checks | Accepted |
| [020](020-domain-write-models-read-views.md) | Aggregates Are Domain-Named Immutable Write Models; Read Models Are Separate `*View` Projections | Accepted |
| [021](021-verb-feature-folders.md) | Feature/Slice Folders Named for the Activity (Verb); Domain Types Keep Canonical Noun Names | Accepted |
| [022](022-convention-sagas-additive-to-pmvh.md) | Convention Sagas Are Additive to PMvH | Accepted |
| [023](023-real-authentication-for-identity.md) | Real Authentication for Identity via ASP.NET Core Identity + Self-Validated JWT | Accepted |

Keep this table in sync when an ADR is added or its status changes — it is the discoverability payload of this README.

**Note on ADR 009 ↔ ADR 023.** ADR 023 supersedes ADR 009's *authentication-deferral* stance only (real auth is now chosen and mechanism-settled). ADR 009 keeps its `Accepted` status because its **Polecat deferral** and **boring-CRUD-foil** framing still hold — only the "auth stays stubbed" clause retires. ADR 009's third amendment carries the cross-reference.

## Cross-references

- [CLAUDE.md § ADRs](../../CLAUDE.md) — ADR routing-layer treatment and capture threshold.
- [CLAUDE.md § External-skill path overrides](../../CLAUDE.md) — the mattpocock skill family assumes `docs/adr/`; CritterMart uses `docs/decisions/`. Treat the two as equivalent when invoking those skills.
- [`../prompts/`](../prompts/) and [`../retrospectives/`](../retrospectives/) — where below-threshold decisions live instead.
