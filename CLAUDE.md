# CritterMart — AI Development Guidelines

CritterMart is a work in progress.

The rest of this document is effectively a "pipeline export template" lifted from another Critter Stack oriented reference architecture. It describes the design-and-build pipeline that produces the code and artifacts in this repo. The pipeline is designed to be AI-friendly, with durable, version-controlled records of design intent, implementation intent, and retrospective insight.

This CLAUDE.md file needs to be updated to reflect this specific project, CritterMart. Again, this is primarily just an exported pipeline that was meant to be a template.

Okay, now everything below is a "copy and paste".

A portable summary of the design-and-build pipeline used in a sister project (a Critter Stack reference architecture). Drop this file into a new repository as a starting point — rename to `CLAUDE.md`, prune what doesn't fit, and expand the parts that do.

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

### 3. Event Modeling Workshop

Adam Dymitruk-style. Produces a timeline of **events**, **commands**, **views/read models**, **swim lanes** (one per actor or external system), **vertical slices**, and **GWT scenarios** per slice.

- **Artifact:** `docs/workshops/NNN-{bc}-event-model.md`.
- **Why:** the workshop output is the single source of truth that every later artifact references. Slices become the unit of implementation; GWT scenarios become the unit of test.
- **Output discipline:** each slice has a number (e.g., 5.1, 5.2), reads-from list, writes-to list, and at least one GWT happy path. Failure paths are explicit, not implied.

---

## Per-Slice Implementation Loop

### 4. Narrative

A journey-scoped spec, written from one actor's perspective, threading multiple workshop slices into one coherent experience. NDD-informed (Narrative-Driven Development, Sam Hatoum / Xolvio — principles, not the commercial tool).

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
- **Companion library:** if the project sits on a stack with a published skills library (e.g., JasperFx ai-skills for Critter Stack), defer library mechanics to the upstream skill and write only project-specific conventions in your local skill files. Don't duplicate.

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

### Capture intent in durable, structured form

Design decisions, domain behavior, and user journeys go into version-controlled artifacts — not chat windows, not ticketing systems. The principle is event-sourcing's: durable append-only records of intent outlive any implementation, and current state is reconstructible from them.

---

## Directory Layout

```
docs/
  vision/           ← project overview, goals, principles
  context-map/      ← cross-BC relationships in DDD vocab
  workshops/        ← Event Modeling / Domain Storytelling output
  narratives/       ← NDD-informed journey specs
  skills/           ← component-scoped patterns
    _template/      ← skill authoring template
    DEBT.md         ← skill-file gaps surfaced but deferred
  rules/            ← AI-optimized structural constraints
  decisions/        ← ADRs
  prompts/
    workshops/
    narratives/
    skills/
    decisions/
    implementations/
  retrospectives/
    (mirrors prompts/)
  research/         ← spikes and exploratory work
```

Subdirectories appear as their first artifact lands; don't pre-create empty ones.

---

## Routing Layer (`CLAUDE.md`)

The root `CLAUDE.md` is a **routing layer**, not a manual. Its job is to point a fresh session-runner at the right artifacts:

1. The vision doc (single source of truth for what the project is).
2. The artifact layer map (workshops / narratives / skills / rules / prompts / retros / ADRs / research — what each is for).
3. The session workflow (the two-phase shape above).
4. Architectural non-negotiables (cross-reference to `rules/`).
5. Technology stack table.
6. A short "Do Not" list.

Keep it short enough that a session-runner reads it on every entry. Push detail down into the per-layer READMEs.

---

## Why Each Piece Exists

| Layer | Removed → what fails |
|---|---|
| Context map | Cross-BC contracts drift; services accidentally couple to each other's internals |
| Domain storytelling | Event names freeze language ambiguity into the model |
| Event Modeling workshop | Implementation has no traceable spec; "what is this slice for?" has no answer |
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

## How to Use This Template

1. Drop it in the new repo (as `CLAUDE.md` or under `docs/`).
2. Prune what doesn't fit — if the project doesn't have multiple bounded contexts yet, the context-map step is premature; defer it.
3. Author the **vision doc** first. Don't proceed without it.
4. Run the design phase against the first bounded context: context map → (optional) domain storytelling → event modeling workshop.
5. Pick the first slice. Author the narrative, then the implementation prompt, then execute, then retro.
6. After 2–3 implementation PRs, do a design-return PR (next BC workshop, additional narrative, or a tidy session).
7. The disciplines (one-prompt-one-PR, spec-delta closure, no opportunistic edits, `tidy:` convention) earn their keep on the second or third session — keep them from day one even when they feel like overhead.

The pipeline scales **down** as well as up: a tiny project can run the full loop with one-paragraph narratives and three-line prompts. The discipline is in the structure, not the volume.
