# CritterMart — AI Development Guidelines

CritterMart is a single-seller ecommerce storefront built as a teaching reference architecture for event sourcing with the Critter Stack. It exists to anchor an Event Sourcing with Marten talk and to exercise a disciplined Spec-Driven Development pipeline. See [`docs/vision.md`](docs/vision.md) for the canonical source of truth on what the project is and why.

This `CLAUDE.md` is the **routing layer** for AI sessions working on the codebase. Its job is to orient a fresh session-runner — point at the right artifacts and name the architectural non-negotiables. It does not hold the detail. Detail lives in the per-layer artifacts: workshops, narratives, OpenSpec proposals, ADRs, and skills.

The remainder of this document describes the design-and-build pipeline that produces CritterMart's code and artifacts. The pipeline is AI-friendly: durable, version-controlled records of design intent, implementation intent, and retrospective insight.

The goal of the pipeline: ship code that traces back, step by step, to a modeled scenario — without producing more design ceremony than the project earns. Each layer below has a distinct job; none is decorative.

---

## Two-Phase Shape

The pipeline is two loops running at different cadences:

1. **Pre-code design phase** — runs **once per bounded context**, in order. Produces the durable, multi-session artifacts that downstream sessions reference.
2. **Per-slice implementation loop** — iterates per vertical slice. Each iteration is one working session and produces one PR.

The two phases are not a waterfall. Retrospectives at the end of each implementation session loop findings back into the upstream design artifacts when reality contradicts the model. A **design-return cadence rule** (below) forces the loops to interleave so the implementation runs don't sail away from the design.

---

## Pre-Code Design Phase (per bounded context)

### 1. Context Mapping

Name the cross-context relationships in DDD strategic-design vocabulary: *partnership, customer-supplier, conformist, shared-kernel, published-language, anti-corruption-layer, separate-ways, open-host-service*.

- **Artifact:** `docs/context-map/README.md` (one rolled-up artifact, amended as new BCs appear).
- **Why:** the relationship pattern between two contexts determines what crosses the boundary (events, calls, shared types), who owns the contract, and where translation lives. Naming it early prevents downstream services from accidentally coupling to each other's internals.
- **Cadence:** updated in the same PR as any workshop that adds a new BC or changes an existing relationship.

### 2. Domain Storytelling

Surface the language boundaries between contexts before populating an event model. Useful when the domain has multiple actors with different vocabularies for the same nouns (driver vs. rider, buyer vs. seller, applicant vs. reviewer).

- **Artifact:** `docs/workshops/` (story-shaped captures).
- **Why:** Event Modeling is unforgiving of language ambiguity. Walking the story first makes the ambiguity explicit before it gets frozen into event names.
- **Optional** if the domain language is already clear; mandatory when it isn't.
- **Round-one status (CritterMart):** explicitly skipped. The single-seller storefront has unambiguous shared language across all four bounded contexts — customer, product, cart, order, stock all mean one thing to one actor. This step remains available if a future bounded context introduces actors that speak divergent vocabularies (e.g., adding a vendor portal, returns workflow with separate fulfillment actors, or marketplace listings).

### 3. Event Modeling Workshop

Adam Dymitruk-style. Produces a timeline of **events**, **commands**, **views/read models**, **swim lanes** (one per actor or external system), **vertical slices**, and **GWT scenarios** per slice.

- **Artifact:** `docs/workshops/NNN-{bc}-event-model.md`.
- **Why:** the workshop output is the single source of truth that every later artifact references. Slices become the unit of implementation; GWT scenarios become the unit of test.
- **Output discipline:** each slice has a number (e.g., 5.1, 5.2), reads-from list, writes-to list, and at least one GWT happy path. Failure paths are explicit, not implied.

---

## Per-Slice Implementation Loop

### 4. OpenSpec proposal and Narrative

Per slice, two sibling artifacts are produced from the event model before any code is written. They share a source (the workshop slice) and a scope (one vertical slice), but address two audiences: machines and humans. Both are reference points for the implementation prompt; both must agree.

#### 4a. OpenSpec proposal

A precise, machine-friendly spec. Capabilities and requirements are expressed as SHALL statements. The GWT scenarios authored in the event-modeling workshop become spec scenarios here. The proposal is the authoritative spec the implementation prompt references and the implementation must satisfy.

- **Artifact:** an openspec change at `openspec/changes/{change}/`, authored and validated with the openspec CLI (the adopted tool convention). Each change holds `proposal.md` (Why/What/Capabilities/Impact) and a per-capability SHALL delta at `openspec/changes/{change}/specs/{capability}/spec.md`. The `openspec/` workspace is a peer to `docs/` at the repo root (the CLI hardcodes the directory name; it is not relocatable under `docs/`). The companion `design.md` and `tasks.md` artifacts are authored in the implementation session, not the proposal session.
- **Why:** narratives are persuasive but imprecise; an event-model slice is precise but fragmented. The OpenSpec proposal is the unambiguous, testable contract that bridges them. It is what the code is checked against.
- **Capability granularity:** one capability per aggregate (or document type), not per bounded context. A BC with one aggregate carries one capability (Catalog → `product-catalog`, Inventory → `stock-management`); a BC with several carries several (Orders → `shopping-cart` for Cart, `order-lifecycle` for Order). Confirmed across all round-one capabilities (retros implementations/003, implementations/004, docs/004, implementations/013).

#### 4b. Narrative

A journey-scoped spec, written from one actor's perspective, threading multiple workshop slices into one coherent experience. NDD-informed (Narrative-Driven Development, Sam Hatoum / Xolvio — principles, not the commercial tool). The narrative is the human-readable companion to the OpenSpec proposal.

- **Artifact:** `docs/narratives/NNN-{actor}-{journey}.md`.
- **Format:** frontmatter (status, version, slices covered) + a sequence of *Moments*, each with context / interaction / system response.
- **Why:** workshop slices are correct but fragmented — one slice doesn't tell a contributor what a session-runner needs to know about the wider journey. The narrative is the durable, prose-shaped spec that prompts reference; it persists across many implementation sessions.
- **Versioning:** each session that touches the journey bumps the narrative version and appends to `## Document History`.

### 5. Prompt

A task-scoped build order for **one session**. References the narrative(s) and skill files it will satisfy, names what files to read first, what to produce, and what's out of scope.

- **Artifact:** `docs/prompts/{kind}/NNN-{slug}.md` where `{kind}` is `workshops`, `narratives`, `skills`, `decisions`, or `implementations`.
- **Required sections:** metadata block, framing, goal, **spec delta**, orientation files, working pattern, deliverable plan, out-of-scope list.
- **Why:** prompts are the durable, version-controlled record of *intent at session start*. They are not living documents — once the session runs, the prompt is frozen as a historical record; corrections happen in the *next* prompt.

### 6. Execute + Retrospective

The session runs. The retrospective is authored before the PR opens.

- **Artifacts:** the session's deliverables (code, a new artifact, edited docs) **plus** `docs/retrospectives/{kind}/NNN-{slug}.md`.
- **Retrospective format:** metadata, outcome summary, what worked, what was harder than expected, methodology refinements that emerged, outstanding items / next-session inputs, **spec-delta — landed?** line.
- **Why:** the retro is the feedback edge. It records what the session actually shipped (often divergent from the prompt's plan), surfaces methodology refinements before they evaporate, and explicitly closes — or honestly fails to close — the spec delta the prompt named.

---

## Supporting Layers

### Skills — component-scoped patterns

Per-technology or per-component implementation patterns and conventions. Examples: "how we register Wolverine handlers in this project," "how we structure Marten projections," "how Alba integration tests are scaffolded."

- **Artifact:** `docs/skills/{topic}/SKILL.md`.
- **Why:** narratives capture *what* a slice should do; skills capture *how* the code structurally expresses it. Without skills, every session re-derives conventions from scratch.
- **Authoring template:** keep one at `docs/skills/_template/SKILL.md` and clone it.
- **Companion library:** if the project sits on a stack with a published skills library (e.g., JasperFx ai-skills for Critter Stack), defer library mechanics to the upstream skill and write only project-specific conventions in your local skill files. Don't duplicate. **CritterMart round one** defers to the upstream JasperFx Critter Stack ai-skills library. Local skill files under `docs/skills/` are authored only when a CritterMart-specific convention diverges from the upstream skill or when a project-specific methodology needs its own home; five exist (event-modeling, frontend, marten-projection-conventions, updating-critter-stack-dependencies, wolverine-cross-bc-cascading). A sparsely populated `docs/skills/` is intentional, not debt.

### Rules — AI-optimized structural constraints

Terse, machine-readable encodings of the architectural non-negotiables. Things like: "services do not reference each other's projects," "cross-service comms only via gRPC or messages," "identity provider is swappable."

- **Artifact:** `docs/rules/structural-constraints.md`.
- **Why:** the rationale for these rules lives in ADRs and prose; an AI session-runner benefits from a flat list of imperatives it can read in seconds.

### ADRs — architectural decisions

Record significant decisions when they're made. Format already-established in the repo wins; don't invent a new one.

- **Artifact:** `docs/decisions/NNN-{slug}.md`.
- **Threshold:** capture an ADR when (a) reversing the decision would require touching multiple bounded contexts, (b) the tradeoff is non-obvious, or (c) the next contributor would otherwise have to re-derive the reasoning. Below that bar, the decision lives in the prompt/retro pair that made it.

### Research — exploratory work

Spikes, comparisons, technology investigations, this-isn't-a-decision-yet drafts.

- **Artifact:** `docs/research/{slug}.md`.
- **Why:** keeps exploratory work out of the canonical document layers while still version-controlling it.

---

## Operating Disciplines

These are the rules that make the pipeline behave under AI session pressure. Each exists because its absence has previously caused a problem in a real session.

### One prompt = one session = one PR

A prompt corresponds to one working session. A session produces one PR. The PR contains exactly the prompt's named deliverables plus the retrospective.

**Two named exceptions:**

- **Skeleton + first slice.** The bootstrap PR for a new service may include both the skeleton and the first vertical slice — this is the "blueprint architecture" step (build one slice by hand before turning the per-slice loop loose).
- **Session-runner-blocking skill fix.** A skill-file fix the current session *had* to make to unblock itself rides in that session's PR. Larger skill rewrites — including gaps merely *surfaced* but not blocking — go in a dedicated `tidy: skills` PR. Surfaced-but-not-fixed gaps land in `docs/skills/DEBT.md`.

### Design-return cadence

After every 2–3 implementation PRs against one bounded context, the next PR must be one of:

- A new narrative for that BC,
- The next BC's workshop,
- A skill-tidy or design-tidy PR.

A fourth consecutive implementation PR against the same BC without a design interleave is a signal the design has drifted. The retro can override this rule explicitly when implementation pressure warrants — but never silently.

### No opportunistic edits

A session's edits stay within the files the prompt's deliverable plan named. Same-file edits adjacent to the primary change are in-bounds; edits to *other* files surfaced mid-session are not. They become a new session or a `DEBT.md` row.

**Why:** opportunistic edits expand session scope unpredictably, dilute PR review, and prevent cleanly reverting individual changes.

### Spec-delta closure loop

Every prompt names its **spec delta** in 2–4 lines: what the canonical spec (the narrative or workshop the session satisfies) will gain when this session ships. Spec-shaped terms — *new moment, new slice, amended GWT, new ADR cross-reference* — not process-shaped.

At session close, the retro confirms whether the named delta landed. The narrative or workshop records the amendment in its `## Document History`. Four steps: **prompt names → session executes → retro confirms → spec records.**

Edge case: a pure-housekeeping session honestly names "no spec delta" and the retro forward-confirms the named-none. Don't confabulate.

### `tidy:` commit subjects for maintenance

Use `tidy: <area> — <details>` for sessions whose deliverable is maintenance of existing artifacts. Examples:

- `tidy: skills` — draining DEBT rows.
- `tidy: housekeeping` — README updates, index entries.
- `tidy: encode-<rule>` — lifting a refinement into a convention file.

New artifacts (workshops, narratives, ADRs, slices) do **not** use `tidy:` — those carry their own subjects.

### Tidy ceremony rule

A tidy session that authors **spec content** — a workshop or narrative amendment, a spec `## Purpose`, a convention encoding — carries the full prompt/retro pair. A tidy that is **purely mechanical** — file moves, index counts, archive commands with no content authored — may run light (no prompt/retro pair).

The distinction is what the session *writes*, not how long it takes. Settled in retro docs/007 and held across five consecutive tidies (retros docs/007–010) before being encoded here.

### Capture intent in durable, structured form

Design decisions, domain behavior, and user journeys go into version-controlled artifacts — not chat windows, not ticketing systems. The principle is event-sourcing's: durable append-only records of intent outlive any implementation, and current state is reconstructible from them.

---

## Directory Layout

```
docs/
  vision.md         ← project overview, goals, principles
  context-map/      ← cross-BC relationships in DDD vocab
  workshops/        ← Event Modeling / Domain Storytelling output
  narratives/       ← NDD-informed journey specs
  skills/           ← component-scoped patterns
    _template/      ← skill authoring template
    DEBT.md         ← skill-file gaps surfaced but deferred
  rules/            ← AI-optimized structural constraints
  decisions/        ← ADRs
  prompts/          ← per-session intent (workshops/, narratives/, specs/, rules/, docs/, research/, …)
  retrospectives/
    (mirrors prompts/)
  research/         ← spikes and exploratory work
  handoffs/         ← durable session-to-session handoff docs
  demo-runbook.md   ← boot → seed → drive → verify → teardown procedure
  demo-traffic.ps1  ← scripted demo traffic generator

openspec/           ← OpenSpec workspace (peer to docs/), authored + validated via the openspec CLI
  changes/          ← per-change artifacts: proposal.md, specs/<capability>/spec.md, design.md, tasks.md
    archive/        ← archived (shipped) changes
  specs/            ← main specs, synced from a change on archive
```

Subdirectories appear as their first artifact lands; don't pre-create empty ones.

---

## Routing Layer

This section is the routing layer's payload — the actual orientation a fresh session-runner needs.

### Vision

[`docs/vision.md`](docs/vision.md) — canonical source of truth for what CritterMart is, why it exists, and what it deliberately is not. Read this first.

### Artifact layer map

| Layer | Path | What it holds |
| --- | --- | --- |
| Vision | [`docs/vision.md`](docs/vision.md) | Project purpose and non-goals |
| Context map | [`docs/context-map/README.md`](docs/context-map/README.md) | Cross-BC relationships in DDD strategic-design vocab |
| Workshops | [`docs/workshops/`](docs/workshops/README.md) | Event Modeling output (Domain Storytelling skipped for round one) |
| OpenSpec proposals | [`openspec/changes/`](openspec/) | Per-capability machine-readable SHALL specs, authored + validated via the openspec CLI; workspace is a peer to `docs/` |
| Narratives | [`docs/narratives/`](docs/narratives/README.md) | NDD-informed journey specs, one per actor journey |
| Prompts | [`docs/prompts/`](docs/prompts/README.md) | Per-session intent records, frozen at session start |
| Retrospectives | [`docs/retrospectives/`](docs/retrospectives/README.md) | Per-session outcome records, spec-delta closure |
| Skills | [`docs/skills/`](docs/skills/README.md) | Component-scoped patterns local to CritterMart (five current: event-modeling, frontend, marten-projection-conventions, updating-critter-stack-dependencies, wolverine-cross-bc-cascading) |
| Rules | [`docs/rules/structural-constraints.md`](docs/rules/structural-constraints.md) | AI-optimized structural constraints |
| ADRs | [`docs/decisions/`](docs/decisions/README.md) | Significant architectural decisions (indexed in the folder README) |
| Research | [`docs/research/`](docs/research/README.md) | Spikes and exploratory work (indexed in the folder README) |
| Handoffs | [`docs/handoffs/`](docs/handoffs/) | Durable session-to-session handoffs (current: Saga #2) |
| Demo runbook | [`docs/demo-runbook.md`](docs/demo-runbook.md) | Boot → seed → drive an order → verify every surface → teardown |

### External-skill path overrides

Some installed skills assume artifact paths that don't match CritterMart's layout. Current divergence:

- **ADRs.** The mattpocock skill family (`improve-codebase-architecture`, `grill-with-docs`, `diagnose`, `tdd`, etc.) assumes `docs/adr/`. CritterMart uses [`docs/decisions/`](docs/decisions/) — treat these as equivalent when invoking those skills. CritterMart has no per-context ADR folders in round one, so any `src/<context>/docs/adr/` reference in those skills is also a no-op.

This is the only path override in round one. Other Critter Stack reference architectures the author maintains use the same `docs/decisions/` convention; do not propose a rename without an explicit ADR.

### Tech stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 10, C# 14 |
| Messaging | Wolverine, RabbitMQ transport |
| Persistence | Marten on PostgreSQL (Catalog, Inventory, Orders); EF Core + Npgsql (Identity) — shared database, schema-per-service |
| HTTP | Wolverine.Http |
| Testing | Alba (integration), xUnit + Shouldly (unit) |
| Orchestration | .NET Aspire |
| Observability | OpenTelemetry + CritterWatch console (ADR 017; Wolverine version is pinned to CritterWatch's build target — see the pin note in `Directory.Packages.props` before bumping WolverineFx) |
| Frontend | Vite + React SPA (TS, TanStack Query, Tailwind v4, shadcn/ui), per-service Wolverine.Http, no BFF — ADR 015/016 |

### Architectural non-negotiables

- Four separate services (Catalog, Inventory, Orders, Identity); Identity is a real EF-Core-backed customer registry (Open-Host Service + Published Language) **and** the system's auth issuer — it uses ASP.NET Core Identity + mints a self-validated JWT the other services verify offline against a config-distributed public key (ADR 023). The frontend now authenticates and sends `Authorization: Bearer`; the `sub` claim is the trust boundary. `X-Customer-Id` survives only as a dev-only fallback on Orders (DEBT-tracked for removal). AuthZ (roles/policies) is still deferred (ADR 009/023)
- Cross-service communication via Wolverine over RabbitMQ; no synchronous service-to-service HTTP
- Shared PostgreSQL with schema-per-service
- Process Manager via Handlers pattern for the Order aggregate (ADR 007); convention `Wolverine.Saga` is an additive counterpart used elsewhere (Inventory's `Replenishment`, slices 2.5–2.7), not a replacement
- Inline snapshot projections; no async daemon for round one (one async projection as a teaser is acceptable)

### Do Not — round one

- No live coding in the demo
- No Polecat for identity
- No async projection daemon (one async-projection-as-teaser is the exception)
- No separate BFF project
- No real payment integration; payment is stubbed
- No collapse back to monolith without an explicit ADR
- No opportunistic edits outside the named deliverable
- No new bounded contexts without a context map update and workshop pass

---

## Why Each Piece Exists

| Layer | Removed → what fails |
|---|---|
| Context map | Cross-BC contracts drift; services accidentally couple to each other's internals |
| Domain storytelling | Event names freeze language ambiguity into the model |
| Event Modeling workshop | Implementation has no traceable spec; "what is this slice for?" has no answer |
| OpenSpec proposals | Implementation has no machine-readable contract; "is this code correct?" devolves into prose interpretation |
| Narratives | Slices are correct but fragmented; journey-level coherence is lost |
| Prompts | Session intent evaporates; the next contributor re-derives scope from scratch |
| Skills | Conventions re-emerge inconsistently every session |
| Rules | AI session-runner has to read prose to find imperatives |
| ADRs | Decisions are remade, often differently, when the original reasoning is forgotten |
| Retrospectives | Methodology refinements evaporate; spec deltas land unconfirmed; drift compounds silently |
| One-prompt-one-PR | Sessions metastasize; PR review breaks down; reverts entangle |
| Design-return cadence | Implementation runs drift away from upstream design |
| No opportunistic edits | Scope inflates; reverts entangle; review fidelity collapses |
| Spec-delta closure loop | Narratives and workshops drift out of sync with the code that supposedly satisfies them |

---

## How This Pipeline Operates

1. The **vision doc** is authored first and is the single source of truth for what the project is. Update it deliberately, not opportunistically.
2. Run the design phase against the first bounded context: context map → (Domain Storytelling skipped for CritterMart round one) → event modeling workshop.
3. Pick the first slice. Author the OpenSpec proposal and the narrative as siblings, then the implementation prompt, then execute, then retro.
4. After 2–3 implementation PRs, do a design-return PR (next BC workshop, additional narrative, or a tidy session).
5. The disciplines (one-prompt-one-PR, spec-delta closure, no opportunistic edits, `tidy:` convention) earn their keep on the second or third session — keep them from day one even when they feel like overhead.

The pipeline scales **down** as well as up: a tiny project can run the full loop with one-paragraph narratives, three-line OpenSpec proposals, and three-line prompts. The discipline is in the structure, not the volume.
