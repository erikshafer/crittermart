---
retrospective: 001
kind: workshop
prompt: docs/prompts/workshops/001-crittermart-event-model.md
deliverable: docs/workshops/001-crittermart-event-model.md
date: 2026-05-26
mode: solo multi-persona
session-runner: Claude (Opus 4.7)
---

# Retrospective — Workshop 001: CritterMart Round-One Rolled-Up Event Model

## Outcome summary

The session produced `docs/workshops/001-crittermart-event-model.md` (v1.0) as a single rolled-up Event Modeling artifact covering all four bounded contexts named in vision.md, with 17 numbered slices spanning Catalog (3), Inventory (3), Orders Cart (4), and the Orders Place Order journey (7). The Place Order journey is rendered as a Mermaid sequence diagram covering the full cross-service span chain that the OTel demo (ADR 005) walks. All slices have at least one happy-path GWT scenario; all failure-required slices (`StockReservationFailed`, `OrderPaymentTimeout`, cross-BC duplicate-delivery and message-loss) have explicit failure GWT scenarios. `CartAbandonmentReport` was selected as the round-one async projection teaser per ADR 008 — chosen over `OrdersAwaitingPayment*` (must be inline to drive timeout automation) and `StockLevelView` (must be inline for Orders' cross-BC dependence on Inventory).

The retrospective itself is this file.

## What worked

- **Reading the orientation list in the order the prompt specified.** The skill file first established the artifact's structural conventions (phases, persona roles, slice-table format, pattern citations). The vision file then anchored success criteria. The context map narrowed the integration surface to one active chain (Orders ↔ Inventory). The selected ADRs each landed a specific constraint into the workshop content — ADR 007's four named events became immovable anchors; ADR 008 forced an explicit single-projection async choice; ADR 005 named the storyboard's payoff. By the time the writing began, the artifact's content was largely determined.
- **Two named adjunct patterns appearing twice each.** Klefter translation-decision is cited on slices 4.2, 4.3, 4.5, 4.6 (any time Orders commits an external decision). Bruun temporal-automation is cited on slice 3.4 (`CartAbandoned`) and slice 4.7 (`OrderCancelled` from `OrderPaymentTimeout`). The repetition reinforces the pattern naming for downstream artifacts rather than introducing it once and dropping it — pedagogically valuable for the talk.
- **The `Reads-from` / `Writes-to` columns.** These extra columns (added per CLAUDE.md § 3 guidance) make explicit which streams and views each slice touches. They will pay off in downstream OpenSpec proposal authoring, where the "what does this slice depend on / what does it modify" line is a load-bearing input.
- **Holding to ADR 007's named events.** The prompt was explicit that `StockReserved`, `PaymentAuthorized`, `OrderConfirmed`, `OrderCancelled` are load-bearing and `OrderPaymentTimeout` is a self-scheduled message (not an event). Holding that line meant the event vocabulary stayed consistent with the ADR rather than drifting into convenient variations.
- **QA voice surfacing the delayed-`StockReserved`-after-`OrderCancelled` race.** This particular cross-BC race is non-obvious and would have been easy to miss; capturing it in slice 4.7's failure scenarios (and pointing at how Inventory slice 2.3's idempotent no-op handles it) is exactly the kind of edge case the workshop is supposed to surface before code is written.

## What was harder than expected

- **Deciding whether `StockReservationFailed` belongs on the Stock stream.** The natural pull was symmetry — if `StockReserved` is on the Stock stream, surely its failure twin should be too. The correct answer turned out to be "no, because no state changed": the refusal is a cross-BC message but not a state-changing event on Inventory's stream. Orders captures it on its own stream as a Klefter local commit. This took two passes of Architect-voice deliberation to settle and is worth flagging in the event vocabulary's `StockReservationFailed` line.
- **Naming collision between Inventory's `StockReserved` (on the Stock stream) and Orders' `StockReserved` (on the Order stream).** They are the same domain fact stored twice for two BCs' purposes. The vocabulary section calls this out with a parenthetical, but the naming-collision question is real and could confuse a reader skimming the event list. Worth a note in downstream narrative authoring: the OpenSpec proposals for slices 2.2 and 4.2 should reference the shared name explicitly.
- **The `OrderPaymentTimeout` schedule trigger.** Two options were considered: schedule at `OrderPlaced` (timeout starts the moment the customer commits) or at `StockReserved` (timeout starts only after stock is reserved). Round one's narrative is simpler with the first choice (a single global payment deadline per order); the second choice is operationally tighter but adds branching. The workshop committed to scheduling at `OrderPlaced` and noted the alternative as a non-blocking implementation choice for slice 4.1's OpenSpec proposal. This is a subtle but real design call.
- **Whether to include slice 1.3 (`ChangeProductPrice`) at all.** It's not in vision.md's round-one success criteria. Including it at P2 priority illustrates that even a document-store BC benefits from capturing lifecycle moments as audit events — a worthwhile pedagogical thread for the talk's "when CRUD is fine, and when it's almost fine" beat. Excluding it would have kept the slice count tighter. The decision was to include with explicit P2 and a vocabulary entry; the OpenSpec phase can de-prioritize if it doesn't earn its place.
- **Catalog's near-absence of slices.** Catalog only has 3 slices (one of them P2) because its round-one role is the "CRUD is fine" example — there are very few interesting slices to model. The workshop has to honor that without padding the model. This is the right outcome but feels asymmetric next to Orders' 11 slices; the asymmetry is the pedagogical point and should be acknowledged when narrative authoring begins.

## Methodology refinements that emerged

These are observations about the workshop process worth carrying forward, not corrections to the model itself.

1. **The `Reads-from` / `Writes-to` columns are an unambiguous improvement** over the skill's stock slice-table format. They should become the default for future workshops. Worth lifting into the skill file as a `tidy: encode-reads-writes-columns` PR after another workshop validates the format.
2. **Citing Klefter and Bruun pattern names inline in the slice table** (not in a separate patterns section) keeps the patterns visible where the next consumer — the OpenSpec proposal — needs them. Tested in this workshop; recommend keeping.
3. **The async projection teaser benefits from an explicit "not chosen" list** with reasons. Section 7 of the workshop names three other projections that were *not* the right async candidate and why. This pre-empts the obvious "why not that one?" questions during talk Q&A and during downstream OpenSpec authoring.
4. **The Place Order storyboard works as the *only* full sequence diagram** in the model. BC-internal slices (Cart manipulation, Catalog browsing, stock receipt) are simple enough that the slice-table row plus GWT scenarios suffice. A full storyboard for every slice would have been over-produced. Recommend the same default for future workshops: full storyboards only for cross-BC journeys.
5. **The failure-path enumeration in cross-BC slices benefited from a dedicated "duplicate delivery" line per slice.** Slices 2.2 and 2.3 both have explicit duplicate-delivery GWT scenarios; this convention should propagate to any future cross-BC slice.

## Outstanding items / next-session inputs

These flow downstream into per-slice OpenSpec proposals, narratives, and prompts.

1. **First slice to author.** Per the typical Event Modeling pipeline, slice 1.1 (`PublishProduct`) is the obvious starter — Catalog is the simplest BC, has no cross-BC integration, and produces a working artifact for Inventory and Orders to depend on at seeding time. Alternatively, slice 2.1 (`ReceiveStock`) is a strong first event-sourced-aggregate slice for the talk's pedagogical progression. The Product Owner voice would pick the one that produces the most teaching value first; recommendation is **2.1 ReceiveStock** as the first OpenSpec proposal because it's the talk's first event-sourcing example.
2. **Place Order journey first narrative.** The narrative that threads slices 4.1 → 4.2 → 4.3 → 4.4 is the centerpiece for the talk; it should be the first narrative authored after the foundational Catalog and Inventory slices are in place. The failure-branch narratives (4.5, 4.6, 4.7) can be split off as separate journey narratives or threaded into the main one — to be decided in the narrative-prompt phase.
3. **Open questions from Section 8 of the workshop are non-blocking for first-slice work but should be resolved as they arise.** Specifically: question 1 (cart abandonment scheduling policy) resolves in slice 3.4's OpenSpec; question 2 (symmetric `OrderCancelled` on stock-failure cancellation) resolves in slice 4.5's; question 3 (`StockCommitted` event) is parked for a future ADR; question 4 (Catalog → Orders price-change events) is long-road.
4. **No `docs/skills/` round-one entries triggered by this session.** CritterMart defers to the upstream JasperFx Critter Stack ai-skills library for round one (per CLAUDE.md "Companion library" line). Nothing in this workshop surfaced a CritterMart-specific convention diverging from upstream skills. If downstream slice implementation surfaces such a divergence, it will land in `docs/skills/` with a `tidy: skills` PR per CLAUDE.md's discipline.
5. **No new ADR triggered by this session.** Workshop modeled within the bounds of ADRs 001, 003, 005, 007, 008, 009. The `OrderPaymentTimeout` schedule-trigger question (at `OrderPlaced` vs `StockReserved`) is a per-slice implementation choice, not an architectural one — does not earn an ADR by the threshold in CLAUDE.md ("reversing the decision would require touching multiple bounded contexts" — this one touches only Orders' internal aggregate logic).
6. **The next PR after this one will be implementation, not design, *only if* the design-return cadence permits it.** Per CLAUDE.md's design-return cadence rule, the first 2–3 implementation PRs against any BC can run consecutively; this workshop counts as a design PR and resets the implementation counter. The first OpenSpec / narrative / implementation chain can begin in the next session.

## Spec-delta — landed?

**Yes.** The prompt's spec delta named four things:

1. `docs/workshops/001-crittermart-event-model.md` created — **landed** at v1.0 with all ten required sections.
2. `docs/retrospectives/workshops/001-crittermart-event-model.md` created — **landed** (this file).
3. The forthcoming `docs/workshops/` and `docs/retrospectives/` directories in CLAUDE.md's artifact-layer map become concrete — **landed** (both directories now contain their first artifact).
4. The event vocabulary section becomes the naming authority for downstream artifacts — **landed** in Workshop §4. Downstream OpenSpec proposals, narratives, and prompts will reference this section as the canonical naming source. ADR 007's four load-bearing event names are preserved verbatim; the additional names (`OrderPlaced`, `StockReservationFailed`, `PaymentAuthFailed`, `CartCreated`, `CartItemAdded`, `CartItemRemoved`, `CartItemQuantityChanged`, `CartCheckedOut`, `CartAbandoned`, `StockReceived`, `StockReleased`, `ProductPublished`, `ProductPriceChanged`) are this workshop's commitments and are now authoritative.

No spec-delta items were dropped or downscoped during execution.

## Process notes

- One prompt, one session, one PR — the PR contains exactly the two named artifacts (workshop + retrospective) and nothing else. No opportunistic edits to other files were made; the orientation files (vision, context map, ADRs, skill) were read but not modified.
- No code committed (per the prompt's out-of-scope list). This is design.
- The `Document History` table in the workshop is stamped v1.0 per CLAUDE.md § 4b. Future sessions that touch the workshop append entries here and bump the version.
