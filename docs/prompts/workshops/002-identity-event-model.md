# Prompt: Event Modeling Workshop 002 — CritterMart Identity Bounded Context (Spike Promotion)

**Kind**: pre-code design (workshop) — the design-return interleave promoting the EF-Core Identity spike to a kept bounded context
**Files touched**: `docs/workshops/002-identity-event-model.md` (new); `docs/context-map/README.md` (amend — Identity promotion); `docs/retrospectives/workshops/002-identity-event-model.md` (new); `docs/workshops/README.md` (current-population bump)
**Mode**: solo multi-persona — Facilitator, Domain Expert, Architect, Backend Developer, QA, Product Owner. The artifact captures the *result* of that facilitation, not the transcript.

## Framing

The owner promoted the throwaway EF-Core Identity spike (`spike/efcore-identity` @ `0ffe42e`) to a kept service. Per CLAUDE.md, a kept new BC requires a context-map update + an Event Modeling workshop **before** the code lands on `main` — and this is the design-return interleave that the cadence rule had already flagged as due. The spike inverted the pipeline (built-before-modeled, legitimate for a throwaway); this session authors the design layer the kept code must trace back to. The spike code itself re-lands later via a per-slice OpenSpec proposal → narrative → implementation prompt; THIS session produces design only, no code.

## Goal

Produce `docs/workshops/002-identity-event-model.md` — the Identity BC event model — and amend `docs/context-map/README.md` to promote Identity from a stubbed **Conformist** relationship to a deployed **Open-Host Service + Published Language** relationship (the owner's strategic-design call). Identity stays a DATA STORE, not auth (ADR 009).

## Orientation

Read these before authoring:

1. **`docs/workshops/001-crittermart-event-model.md`** — the format precedent: section order, slice-table columns (`#`, slice, command, events, view, BC, reads-from, writes-to, priority), GWT shape (happy path first, explicit failures), Document History stamping.
2. **`docs/context-map/README.md`** — the relationship to amend (Identity Conformist → OHS/PL), the topology diagram, and the Long-road note (which assumed a Polecat/auth Identity and guessed Customer-Supplier).
3. **The spike** — `src/CritterMart.Identity/` on `spike/efcore-identity` @ `0ffe42e`, the reference implementation (slices 5.1/5.2). Read `Program.cs` (the Wolverine + EF-Core bootstrap, the outbox), `Features/RegisterCustomer.cs` (the cascade), `Customers/*` (entity, DbContext, the `CustomerRegistered` event).
4. **ADRs** — `001-separate-services-topology.md` (**no synchronous service-to-service HTTP** — load-bearing for the OHS/PL split), `003-wolverine-rabbitmq-transport.md`, `009-polecat-deferred-for-round-one.md` (Identity is a data store, not auth/Polecat).
5. **CLAUDE.md §§ 1–3** (context-map + workshop pipeline) and the design-return cadence rule.

## Out of scope

- **No code.** The spike code re-lands later via its own OpenSpec/narrative/implementation chain. Do not edit the spike branch or merge it.
- Do **not** introduce authentication, authorization, or Polecat (ADR 009). Identity is a registry.
- Do **not** author the OpenSpec proposal or narrative for slices 5.1–5.4 — those are separate prompts.
- Do **not** wire `X-Customer-Id` resolution or a `CustomerRegistered` consumer — slices 5.3/5.4 are modeled-not-built.
- Do **not** edit Workshop 001 — Identity's promotion lives in Workshop 002 + the context map.

## Output structure

The workshop mirrors 001's sections, scoped to Identity: Frontmatter, Scope, BC Summary, Timeline/Flow (small — Identity has no multi-step journey), Event Vocabulary (`CustomerRegistered`), Slice Table (5.1–5.4, numbered as the 5th BC bucket), GWT Scenarios, Read Models/Projections (none — the row IS the model), Open Questions/Parking Lot, Document History (v1.0). The context-map amendment: promote the Identity bullet, the topology, the relationships-table row (Conformist → OHS/PL), the round-one-stubs line, and the Long-road note (split the Polecat/auth concern from this registry promotion).

## Working pattern

Solo multi-persona. **The Architect voice owns the load-bearing call:** the Open-Host Service (`GET /customers/{id}`) serves the frontend; the Published Language (`CustomerRegistered` subscription) serves cross-BC consumers — because ADR 001 forbids sync service-to-service HTTP, identity resolution across BCs is NOT a sync call into Identity but a read of a local model fed by the PL event. **QA** owns the explicit failure paths (duplicate email, 404). **Product Owner** keeps 5.3/5.4 as future increments, not this-pass work. One prompt = one session = one PR; author the retro before the PR.

## Spec delta

`docs/workshops/002-identity-event-model.md` is created (v1.0) and `docs/context-map/README.md` gains a deployed Identity BC with an **Open-Host Service + Published Language** relationship (superseding the stubbed-Conformist row and the Polecat-assuming Long-road note). The Identity BC gains an authoritative event model — slices 5.1–5.4 and the `CustomerRegistered` vocabulary entry — that the forthcoming OpenSpec proposal + narrative + implementation prompt (which re-land the spike code on `main`) will reference. `docs/workshops/` gains its second workshop; the design-return cadence counter resets.
