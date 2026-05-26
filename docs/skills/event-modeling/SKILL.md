---
name: event-modeling
description: "Facilitate an Event Modeling workshop. Use when running, simulating, planning, or guiding any phase — brain dump, timeline, slicing, scenario writing — or when multi-persona facilitation is needed for system design."
cluster: core
tags: [event-modeling, design, workshops, methodology, slices, ddd]
---

# Event Modeling Workshop

Event Modeling is a collaborative workshop technique created by Adam Dymitruk (Adaptech Group) for designing information systems. It produces a visual, timeline-based blueprint showing how data flows through a system — from user intent through state changes to read-side projections. It works for any information system, not just event-sourced ones, but maps naturally onto CQRS and event sourcing patterns.

The project's pipeline authors an Event Modeling workshop pass before any per-slice code is written. Workshop output flows into per-slice OpenSpec proposals (`docs/specs/`) and sibling narratives (`docs/narratives/`); both flow into prompts (`docs/prompts/`); prompts drive implementation. This skill covers the workshop technique itself.

## When to apply this skill

Use this skill when:

- Running, simulating, or facilitating an Event Modeling session.
- Planning a workshop scope (which BCs, which journey).
- Defining slices from a complete event model.
- Writing Given/When/Then scenarios from slice definitions.
- Verifying or extending the event vocabulary against a user journey.
- Using multi-persona facilitation to surface conflicts and edge cases.

This skill is a methodology skill. The implementation skills downstream (`marten-aggregates`, `wolverine-message-handlers`, etc.) consume its outputs but are not themselves activated by it.

## The Four Building Blocks

| Block | Color | Meaning |
|---|---|---|
| **Events** | Orange | Facts that occurred — past tense, immutable |
| **Commands** | Blue | User intentions or system requests that cause events |
| **Views / Read Models** | Green | Projections of event data back to the UI |
| **UI Wireframes / Screens** | White | What the user actually sees and interacts with |

Arrange these in chronological order on a horizontal timeline, in swim lanes:
`UI → Command → Event Stream → View → UI`

---

## Workshop Phases

### Phase 1 — Brain Dump

Everyone writes events as fast as possible — no ordering, no judgment. Events are facts: past tense, concrete, meaningful to the domain.

**Input:** A domain or feature area to explore (e.g., "customer places an order during stock contention", "Orders BC internals from cart through completion").
**Process:** Each persona calls out events. No filtering, no sequencing — volume over accuracy.
**Output:** Unordered list of candidate events (expect 15–60 for a single bounded context).

> When CritterMart develops an established event vocabulary (in `docs/vision.md` or a dedicated event-vocabulary doc), Phase 1 of journey workshops becomes a **verification pass** — walk the journey and confirm the vocabulary accounts for everything. Add missing events as discovered.

### Phase 2 — Storytelling

Arrange events into a coherent narrative on the timeline. Ask: *"What happened first? What does this enable next?"* Gaps in the story reveal missing events.

**Input:** Unordered event list from Phase 1 (or verified vocabulary for journey workshops).
**Process:** Place events left-to-right on the timeline. Fill gaps: "What happened between X and Y?"
**Output:** Chronologically ordered event timeline with gap markers resolved.

### Phase 3 — Storyboarding

Add UI wireframes above the timeline and views below. Connect them to their triggering commands and resulting events. This makes the full user journey visible.

**Input:** Ordered event timeline from Phase 2.
**Process:** For each event, ask "What UI triggered this?" (add screen above) and "What does the user see after?" (add view below). Connect with commands.
**Output:** Full storyboard: `UI → Command → Event(s) → View → UI` for the entire flow.

### Phase 4 — Identify Slices

Draw vertical cuts through the model — each slice is one complete feature: `UI → Command → Event(s) → View`. Slices become work units (narratives, prompts, PRs).

**Input:** Complete storyboard from Phase 3.
**Process:** Draw vertical lines. Each slice must be independently deliverable and testable.
**Output:** Slice table (see Structured Output Format below).

### Phase 5 — Scenarios (Given/When/Then)

For each slice, write acceptance scenarios:
- **Given**: the events already in the stream (preconditions).
- **When**: the command issued.
- **Then**: the new events produced and/or the view state.

**Input:** Slice definitions from Phase 4.
**Process:** Write happy path first, then edge cases and failure modes per slice.
**Output:** Given/When/Then scenarios per slice.

---

## Two Workshop Types

Two complementary workshop formats are supported.

### User Journey Workshop

Walks a cross-cutting scenario (e.g., a customer placing an order from cart through to confirmation) end-to-end. Touches multiple BCs. Produces horizontal coverage — the sequence of handoffs and integration events across the system.

**Best for:** Validating the integration topology, defining narrative scope, confirming the event vocabulary covers a complete user scenario.

**Tradeoff:** Does not produce aggregate internals, saga state machine details, or deep failure/compensation paths within a single BC.

### BC-Focused Workshop

Deep-dives into a single bounded context. Produces vertical depth — aggregate design, saga state transitions, DCB boundary model details, compensation events, and edge cases.

**Best for:** Implementation-ready designs for a specific BC. Produces the Given/When/Then scenarios that become test cases.

**Tradeoff:** Does not validate cross-BC integration or end-to-end user experience.

**Recommended sequence:** Run one or two user journey workshops first to establish the horizontal map, then run BC-focused workshops to fill in vertical depth before implementation.

---

## Structured Output Format for Slices

| # | Slice Name | Command | Events | View | BC | Priority |
|---|-----------|---------|--------|------|----|----------|
| 1 | Customer adds item to cart | `AddToCart` | `CartItemAdded` | `CartView` | Orders | P0 |
| 2 | Customer places order | `PlaceOrder` | `OrderPlaced` | `OrderStatusView` (awaiting confirmation) | Orders | P0 |
| 3 | Order reserves stock with Inventory | `ReserveStock` | `StockReserved`, `OrderConfirmed` | `OrderStatusView` (confirmed) | Orders → Inventory | P0 |
| 4 | Order cancels on payment timeout | *(scheduled)* | `OrderCancelled` | `OrderStatusView` (cancelled) | Orders | P1 |

**Column definitions:**
- **Slice Name**: Human-readable feature name.
- **Command**: The command that enters the system (user or system-initiated). Use *(scheduled)* for time-triggered slices and *(external)* for slices triggered by an upstream provider event.
- **Events**: Domain events produced (comma-separated if multiple). Cross-service events show the producer-consumer chain with `→` (e.g., `Orders → Inventory`).
- **View**: The read model or UI state updated after the event.
- **BC**: Bounded context that owns this slice. Verify against the BC list in [`docs/vision.md`](../../vision.md). For slices that span BCs, list the producer-then-consumer chain.
- **Priority**: P0 = must-have for first vertical demo, P1 = should-have, P2 = nice-to-have.

---

## Adjunct Patterns

Beyond the four core building blocks, three named event-modeling patterns recur across event-sourced systems. Naming them here lets workshop prose, narrative authoring, and ADRs refer to each by its published-literature name rather than re-deriving the shape each time.

Sources: Adam Dymitruk (Adaptech Group, the core method), Filip Klefter (translation-decision events), and Anders Bruun Olsen (temporal-automation slice pattern, configuration-as-events).

### Klefter Translation-Decision Events

When a slice coordinates with an external system AND a decision is made locally based on the external input, the local decision is captured as a first-class event in the BC's stream. Names the BC's authority over the decision even though the input came from outside; the event is the audit trail of "I asked X, got Y, decided Z."

**Pattern signal:** an outbound query whose result the BC commits as a local event before any further processing.

**Example:** Orders' stubbed payment authorization. At checkout, the Order process manager (per ADR 007) issues a payment-authorization call to the stubbed payment provider. The result lands as a local event on the Order stream — `PaymentAuthorized` (with provider auth code) or `PaymentAuthFailed` (with reason). Downstream Order logic (confirm fulfillment, cancel, schedule timeout) consumes the local event. The provider's response is never read again outside Orders; the local event is the authority over the decision and the audit trail.

### Bruun Temporal-Automation Slice Pattern

A slice whose trigger is the passage of time, not an incoming domain event. The slice fires when a clock condition is met (`now() >= scheduledFor`) on a row in a todo-list read model. Boards render the pattern with two distinguishing marks: a clock-rewind glyph on the gear (automation) sticky, and an asterisk suffix on the read model's name (e.g., `OffersAwaitingAcceptance*`).

**Pattern signal:** an automation whose trigger is clock state, consuming a todo-list read model whose rows self-remove when the work completes.

**Example:** the Orders payment-timeout (per ADR 007). When the Order aggregate enters its payment-pending state, the process manager schedules a self-message — `OrderPaymentTimeout` — for some future tick. If a `PaymentAuthorized` event hasn't landed by then, the timeout handler commits a terminal `OrderCancelled` event that closes the stream and triggers an outbound message to Inventory to release the prior `StockReserved`. The todo-list projection `OrdersAwaitingPayment*` carries rows added when payment authorization is requested and removed on either `PaymentAuthorized` or `OrderCancelled`. The asterisk convention marks it as a temporal-automation source.

### Configuration-as-Events (Bruun)

Operator-tunable policy parameters represented as events on a singleton stream rather than rows in a settings table. Each configuration change is an event; the current policy is the latest event's payload. Provides audit trail, version history, and natural integration with event-driven downstream consumers.

**Pattern signal:** policy that needs an audit trail and version history, where downstream consumers should react to changes rather than periodically re-read a settings table.

**Round-one status:** no clear CritterMart use in round one. Operator-tunable policy is minimal in a single-seller storefront. A plausible future candidate: stock-reservation policy (reservation TTL, retry behavior, per-SKU caps) configured as events on a singleton Inventory stream rather than rows in a settings table. Naming the pattern here lets a future ADR adopt it without re-deriving the shape.

This section names patterns; it does not commit CritterMart to implement any of them. Naming makes the model legible when the project encounters these patterns during workshops or when a future ADR proposes adopting one for a specific BC.

---

## Output Artifacts

- **The Event Model** — the full visual blueprint (primary deliverable, captured in `docs/workshops/`).
- **Slice definitions** — vertical feature cuts, each independently deliverable.
- **Given/When/Then scenarios** — acceptance criteria per slice.
- **Narrative drafts** — slices group into journey-scoped narratives in `docs/narratives/`.
- **API contracts** — command shapes, read model schemas, and proto-message candidates emerge naturally.
- **Aggregate / projection sketches** — implementation starting points for the Phase 2 skills.

---

## Multi-Persona Facilitation

When facilitating a workshop, invoke distinct personas to represent different stakeholder perspectives. This surfaces conflicts, blind spots, and richer domain understanding than a single voice would produce.

### Persona Roles

The persona roster typically includes the roles below. The roles themselves are project-agnostic.

| Role | Voice in Workshop |
|---|---|
| **Facilitator** | Leads the workshop, maintains flow, keeps slices small, synthesizes output. |
| **Domain Expert** | Owns the business language; corrects names, validates against ecommerce conventions and storefront reality. |
| **Architect** | Flags BC boundaries, aggregate design, projection feasibility, transport choices, Critter Stack patterns. |
| **Backend Developer** | Asks "how would we build that?", flags implementation concerns, validates handler/saga shapes. |
| **Frontend Developer** | Grounds the model in the storefront UI; asks what users see at each step. |
| **QA** | Stress-tests the model; asks about failures, edge cases, race conditions, timing windows. |
| **Product Owner** | Guards scope, prioritizes slices, enforces demo-first constraints. |
| **UX** | Advocates for customer experience; read model legibility. |

### Which Personas Lead Each Phase

| Phase | Primary Voices | Why |
|---|---|---|
| **Brain Dump** | Facilitator + Domain Expert + Architect | Facilitator keeps pace; Domain Expert knows business events; Architect knows technical/integration events. |
| **Storytelling** | All eight — QA earns their keep here | QA finds gaps; UX maps events to user moments; everyone contributes to sequencing. |
| **Storyboarding** | Frontend Developer + UX + Backend Developer | Frontend designs screens; UX validates experience; Backend confirms view feasibility. |
| **Slicing** | Facilitator + Product Owner + Backend Developer | Facilitator keeps slices crisp; PO prioritizes; Backend validates deliverability. |
| **Scenarios** | Facilitator + QA + Backend Developer + Domain Expert | QA writes edge cases; Backend validates feasibility; Domain Expert validates accuracy. |

### How to Run Multi-Persona Mode

```
[@Facilitator] Let's verify the brain dump. Walk me through what happens
  from the moment a customer taps "Place Order" with items in their cart.

[@DomainExpert] The customer has a cart with one or more line items. The
  tap produces a PlaceOrder command. Orders picks it up. That's OrderPlaced.
  Orders then needs to reserve stock against Inventory before confirming.

[@Architect] OrderPlaced is an Orders BC event. Orders sends ReserveStock
  to Inventory as a Wolverine command over RabbitMQ (per ADR 003) —
  brokered messaging, no synchronous HTTP. Worth flagging on the timeline
  as a cross-service interaction; the OpenTelemetry trace will span both
  services (per ADR 005).

[@QA] What if Inventory can't reserve the full quantity? Do we fail the
  order immediately, or attempt a partial fulfillment? What's the timeout
  if Inventory doesn't respond?

[@Facilitator] Good question. Park it as a candidate slice — "partial or
  failed stock reservation" is its own scenario with its own events and
  view. Continue with the happy path.

[@FrontendDeveloper] After PlaceOrder, the customer sees an "Order being
  confirmed..." view. That's a read model — OrderStatusView or similar.
  Updates as StockReserved and PaymentAuthorized events land.
```

Personas may agree, disagree, and build on each other. The goal is productive tension — not consensus for its own sake.

---

## Pipeline Integration

### How Workshop Outputs Connect to CritterMart Artifacts

| Workshop Output | Pipeline Artifact | Location |
|---|---|---|
| **Workshop session record** | Markdown capture of the session | [`docs/workshops/`](../../workshops/) |
| **Slices** | OpenSpec proposals (per-slice) and narrative drafts (journey-scoped); prompts (task-scoped) | [`docs/specs/`](../../specs/), [`docs/narratives/`](../../narratives/), [`docs/prompts/`](../../prompts/) |
| **Scenarios (Given/When/Then)** | Test specifications | `tests/` per service |
| **BC boundary changes** | Update or verify | [`docs/vision.md`](../../vision.md) § Bounded contexts |
| **Event vocabulary changes** | Update or verify | [`docs/vision.md`](../../vision.md) (or future event-vocabulary doc) |
| **Architectural decisions** | ADR markdown files | [`docs/decisions/`](../../decisions/) |
| **Command / event shapes** | C# records in service projects | `src/CritterMart.<ServiceName>/` |
| **View / read model designs** | Marten projections per service | `src/CritterMart.<ServiceName>/` |
| **Cross-service contracts** | Wolverine message records (commands, events) | per-service projects; shared message types as the codebase scaffolds |

### Existing Documents to Load

| Document | When to load |
|---|---|
| [`docs/vision.md`](../../vision.md) | Always — verify BC ownership, technology choices, design principles. |
| [`docs/rules/structural-constraints.md`](../../rules/structural-constraints.md) | Service-boundary rules, transport selection. |
| [`docs/narratives/`](../../narratives/) *(forthcoming)* | Journey workshops — load relevant narratives if the journey extends an existing one. |
| [`docs/decisions/`](../../decisions/) | When the workshop touches a topic an ADR governs (transport, contracts, identity). |

---

## Quick Reference: Common Mistakes to Catch

- **Events named as commands.** "PlaceOrder" is wrong as an event — "OrderPlaced" is correct.
- **"Event" suffix.** "OrderConfirmedEvent" is wrong — "OrderConfirmed" is correct. See `domain-event-conventions`.
- **Missing the "why" behind a command.** Add a UI wireframe to show the trigger.
- **Views that can't be derived from the events on the board.** You're missing events.
- **Slices too large to deliver independently.** Keep slicing. A slice that takes more than one prompt to implement is too large.
- **Scenarios that test infrastructure instead of behavior.** Focus on domain facts, not on whether ASB delivers messages.
- **Assigning a slice to the wrong BC.** Verify against [`docs/vision.md`](../../vision.md) § Bounded contexts.
- **Skipping the QA voice.** Edge cases found late are expensive to fix.
- **Conflating mechanical events with business decisions.** `PaymentTimedOut` (clock fired, system-driven) is mechanical; `CustomerCancelledOrder` (customer changed their mind) is a business decision. Both are events; they have different authority and different downstream consequences.
- **Treating a downstream BC as the originator of upstream data.** Orders doesn't originate stock data — Inventory does, and Orders consumes it via `StockReserved` / `StockReservationFailed` events. Catalog doesn't originate order facts — Orders does, and Catalog has no BC-level integration with Orders in round one (product data flows through the frontend at add-to-cart time).

---

## See also

**Downstream** — natural follow-ups when workshop output is in hand:

- `domain-event-conventions` — naming and shape rules for the events identified in workshops.
- `marten-aggregates` — implementing event-sourced aggregates from workshop output (Phase 2).
- `marten-wolverine-aggregates` — implementing handlers that produce workshop-identified events (Phase 2).
- `wolverine-sagas` — implementing the temporal-automation slices identified by the Bruun pattern (Phase 4).
- `protobuf-contracts` — implementing the cross-service contracts implied by cross-BC slices (Phase 1).

**External:**

- [Adam Dymitruk's Event Modeling site](https://eventmodeling.org/) — the canonical reference for the technique.
- [`docs/vision.md`](../../vision.md) § Why this exists — CritterMart's commitment to Event Modeling and Domain Storytelling.
