# Prompt: Event Modeling Workshop 001 — CritterMart Round-One Rolled-Up Model

**Kind**: pre-code design (workshop)
**Files touched**: `docs/workshops/001-crittermart-event-model.md` (new); `docs/retrospectives/workshops/001-crittermart-event-model.md` (new)
**Mode**: solo multi-persona — a single session simulates the Facilitator, Domain Expert, Architect, Backend Developer, Frontend Developer, QA, Product Owner, and UX voices as documented in the Event Modeling skill. The artifact captures the *result* of that facilitation, not the transcript.

## Framing

This is CritterMart's first Event Modeling workshop. CLAUDE.md's pipeline requires an Event Modeling pass before per-slice code is written. Round one rolls all four bounded contexts into a single artifact because (a) the integration topology is narrow — only Orders ↔ Inventory has active BC-level message flow per the context map — and (b) the talk benefits from pointing at one workshop file alongside the context map.

Domain Storytelling is explicitly skipped per CLAUDE.md Section 2: CritterMart's single-seller domain has unambiguous shared vocabulary across the BCs.

## Goal

Produce `docs/workshops/001-crittermart-event-model.md` as a single rolled-up Event Modeling artifact covering the four bounded contexts (Catalog deployed, Inventory deployed, Orders deployed, Identity stubbed) and the round-one slices implied by the vision's success criteria.

The artifact must follow the format conventions in `docs/skills/event-modeling/SKILL.md`. Slices must be numbered, with reads-from / writes-to columns, command / event / view shapes, BC ownership (or chain for cross-BC slices), and at least one happy-path GWT scenario per slice. Failure paths are explicit, not implied — especially for the Orders ↔ Inventory bidirectional flow and the `OrderPaymentTimeout` temporal automation from ADR 007.

## Orientation

Read these in this order before beginning the workshop:

1. **`docs/skills/event-modeling/SKILL.md`** — the authoring conventions. Phases 1–5, slice-table format, GWT scenario format, multi-persona facilitation, the Klefter and Bruun adjunct patterns (both apply to CritterMart and should be cited where they appear in the slices).
2. **`docs/vision.md`** — the four bounded contexts and round-one success criteria. The success criteria list is the spine of the slices the workshop must produce.
3. **`docs/context-map/README.md`** — the integration backdrop. Specifically:
   - Orders ↔ Inventory is Customer-Supplier with bidirectional event flow over RabbitMQ
   - Catalog has *no* BC-level integration in round one — product data flows through the frontend and is snapshotted into Cart commands at add-to-cart time
   - Identity is Conformist and stubbed; customer IDs flow through commands as if from a real identity system
4. **Selected ADRs** (paths under `docs/decisions/`):
   - `001-separate-services-topology.md` — three deployed services; Identity stubbed
   - `003-wolverine-rabbitmq-transport.md` — RabbitMQ for cross-service messaging
   - `005-opentelemetry-tracing-enabled.md` — OTel spans across services; the Place Order trace is a centerpiece talk asset
   - `007-process-manager-via-handlers-for-order.md` — **critical for the slices.** The Order aggregate IS the process manager; PMvH; `OrderPaymentTimeout` self-message; idempotency via state guards; named events `StockReserved`, `PaymentAuthorized`, `OrderConfirmed`, `OrderCancelled`
   - `008-inline-projections-async-teaser-no-daemon.md` — inline snapshot projections; one async projection as teaser (pick one in the workshop and note which)
   - `009-polecat-deferred-for-round-one.md` — Identity is hardcoded
5. **CLAUDE.md Section 3** — workshop discipline notes (numbered slices, reads-from / writes-to, explicit failure paths, GWT per slice).

## Out of scope

- Do not edit any other file. The session produces the workshop artifact and the retrospective. Nothing else.
- Do not author the OpenSpec proposals or narratives that will follow per-slice — those are separate prompts.
- Do not pre-decide implementation specifics (table schemas, projection internals, exact handler signatures). The workshop output describes domain behavior, not code.
- Do not introduce new bounded contexts beyond the four named in vision.md. If a candidate emerges, capture it in the parking lot, not as a fifth BC.
- Do not include round-two long-road slices (returns, promotions, marketplace, vendor) except as one-line parking-lot entries.
- Do not contradict ADR 007's named events: `StockReserved`, `PaymentAuthorized`, `OrderConfirmed`, `OrderCancelled` are load-bearing names. `OrderPaymentTimeout` is the self-scheduled message name. Other event names are the workshop's to choose, but these must remain consistent across slices and the event vocabulary section.
- Do not commit any code. This is design.

## Output structure

The single workshop file at `docs/workshops/001-crittermart-event-model.md` should contain, in this order:

1. **Frontmatter** — workshop number, scope, status (Draft on first commit), date, participants ("session-runner in solo multi-persona mode")
2. **Scope** — short paragraph: what's in (the four BCs, the Place Order journey, the round-one success-criteria slices) and what's deferred (the long-road items)
3. **Bounded-context summary** — one paragraph per BC: persistence shape, key aggregates (or "stubbed" for Identity), primary events. Catalog will be brief because it is the CRUD/document example. Orders will be richest (Cart and Order aggregates, plus the process-manager state and terminal events from ADR 007).
4. **Timeline / storyboard** — the Place Order journey rendered as `UI → Command → Event(s) → View → UI` flow. Mermaid or table. This is the centerpiece for the talk's OTel demo (per ADR 005). Other BC-internal flows can be more tabular without full storyboards.
5. **Event vocabulary** — alphabetical list of all events identified, grouped by BC, with one-line meanings. This section becomes the authoritative naming source for the OpenSpec proposals and narratives that follow downstream. Past tense, no "Event" suffix, domain-meaningful.
6. **Slice table** — per the skill's Structured Output Format, augmented with reads-from / writes-to columns. Columns: `#`, `Slice name`, `Command`, `Events`, `View`, `BC` (or BC chain), `Reads-from`, `Writes-to`, `Priority`. Slices that span BCs note the chain (e.g., `Orders → Inventory`).
7. **GWT scenarios** — one subsection per slice with at minimum one happy-path scenario. Failure paths required where the slice has them. Minimum required failure-path scenarios: `StockReservationFailed`, the `OrderPaymentTimeout` flow ending in `OrderCancelled`, and any cross-BC message-loss or duplicate-delivery handling that the Architect voice surfaces.
8. **Where the async projection teaser lands** — short subsection naming which projection runs async (per ADR 008), and why that one is pedagogically interesting for the talk. One projection only.
9. **Open questions / parking lot** — anything the QA voice or Architect voice surfaced that wasn't resolved in the session. Long-road slices live here too.
10. **Document History** — initial version stamp (`v1.0` on first commit; subsequent sessions that touch this artifact bump the version per CLAUDE.md Section 4b).

The retrospective at `docs/retrospectives/workshops/001-crittermart-event-model.md` follows CLAUDE.md Section 6's format: metadata, outcome summary, what worked, what was harder than expected, methodology refinements that emerged, outstanding items / next-session inputs, and the **spec-delta — landed?** line.

## Working pattern

Run all five phases per the skill:

1. **Brain dump** — enumerate candidate events per BC. Catalog will be small (per ADR 002 it uses Marten documents, not event sourcing — its "events" are really lifecycle moments like `ProductPublished`, `ProductPriceChanged`, `ProductDiscontinued`, used for audit / projection-feed purposes). Inventory's stock events (`StockReceived`, `StockReserved`, `StockReleased`, etc.). Orders is richest: Cart events, Order events, the process-manager state events from ADR 007.
2. **Storytelling** — sequence events along the Place Order journey timeline. Fill gaps.
3. **Storyboarding** — identify the UI moment that triggers each command and the view that updates after each event. Be explicit about what the customer sees during the Place Order flow because the talk uses this for the OTel cross-service trace demonstration (per ADR 005).
4. **Slice identification** — draw vertical cuts. Each slice is independently deliverable and testable. Slices that span BCs (e.g., Orders → Inventory) note the chain in the BC column.
5. **GWT scenarios** — happy path first. Then failure scenarios and temporal-automation scenarios. The Bruun pattern's `OrdersAwaitingPayment*` todo-list projection from the skill applies directly to ADR 007's `OrderPaymentTimeout`; cite it. The Klefter translation-decision pattern applies to the stubbed `PaymentAuthorized` / `PaymentAuthFailed` decision; cite it.

Use multi-persona voices to surface conflicts before writing the artifact section:
- **QA's job** is to ensure failure paths are explicit, not implied — especially around stock contention, payment timeout, and cross-BC message handling.
- **Architect's job** is to flag cross-service message paths (RabbitMQ per ADR 003), idempotency via state guards (per ADR 007), and projection lifecycle questions (inline vs the one async teaser, per ADR 008).
- **Product Owner's job** is to keep slices small and prioritized against the round-one success criteria; anything else lands in the parking lot.
- **Domain Expert's job** is to name events in past tense, domain-meaningful, no "Event" suffix.

The session is one PR per CLAUDE.md's "One prompt = one session = one PR" discipline. Author the retrospective before opening the PR.

## Spec delta

`docs/workshops/001-crittermart-event-model.md` and `docs/retrospectives/workshops/001-crittermart-event-model.md` are created. The forthcoming `docs/workshops/` and `docs/retrospectives/` directories in CLAUDE.md's artifact-layer map become concrete. The OpenSpec proposals and narratives that will follow per-slice now have an authoritative model to reference; the implementation prompts downstream of those have a traceable spec chain back to modeled scenarios. The event vocabulary section becomes the naming authority for every downstream artifact that mentions events by name.
