# CritterWatch UI/UX feedback from CritterMart

**From:** Erik Shafer (CritterMart) · **For:** Jeremy & Babu (JasperFx)
**CritterWatch version:** 1.0.0-alpha.3 (trial) · **Date:** 2026-06-24
**Evidence:** [`cw-screenshots/`](cw-screenshots/README.md) holds 27 automated full-page captures. Each entry below cites a file under `cw-screenshots/shots/`.

---

## Why this packet exists

You asked for larger, real-world projects to pressure-test CritterWatch's UI/UX. CritterMart is a
four-service, event-sourced Critter Stack reference app (Catalog, Inventory, Orders, Identity on
Marten + Wolverine + RabbitMQ). It isn't enterprise scale, but it's bigger and messier than the
Critter Stack samples: inline and async projections, a multi-stream fan-out, a flat-table
`EventProjection`, cross-BC RabbitMQ fan-out, and a deliberate poison path. That gave the console
real, varied telemetry to render while we walked it.

This is a heuristic UI/UX review done from an operator's task perspective ("is my system healthy?",
"why did this dead-letter?", "what did this stream look like at step 12?"). It is deliberately not a
redesign. Every item is a targeted change (a component swap, a label, a state, a tooltip, or a bit of
progressive disclosure), sized for beta, v1.1, or v1.2.

### How the console was lit (to reproduce)

On branch `research/cw-telemetry-spike` with `Cw__Telemetry=true`, the Marten async daemon, an
`OrderPlacedSignal` broadcast, and a poison endpoint all run. Traffic is then driven with
`docs/demo-traffic.ps1 -Continuous -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 7`, and screenshots
are regenerated with `docs/research/cw-screenshots/capture-cw.cjs` (Playwright). See
[`cw-telemetry-fodder.md`](cw-telemetry-fodder.md) for the full setup.

---

## What's already working (please keep)

The team has good instincts worth reinforcing before the critiques:

- **Empty states coach the user.** For example, "Pick a projection, choose a source mode, and click
  Run Projection" (`shots/ex-orders-projection-stepper.png`). Most tools skip this.
- **The Source selector is a segmented button group** (Stream / Stream Slice / Tag Query,
  `shots/st-stocklevel-1-configured.png`). It's the right control for three mutually-exclusive modes.
- **Inline help already exists in one spot:** the ⓘ next to "Progress / Gap" on the Projections
  table (`shots/03-projections.png`). The instinct is there, it just isn't applied consistently.
- **The Explorer's Projection Statuses tab leads with `Lifecycle`** (`shots/ex-orders-projection-statuses.png`),
  which is a cleaner model than the global Projections table. Lean into it.

---

## Feedback entries (prioritized)

### 1. Inputs that already know the answer should offer it
**Target:** beta (stream id) / v1.1 (rehydrate) · **Effort:** S/M
**Where:** `shots/st-stocklevel-1-configured.png`, `shots/ex-inventory-rehydrate-aggregate.png`, `shots/ex-inventory-recent-streams.png`

- **Observation:** The Projection Stepper's Stream Id is a free-text box ("Enter a stream id to step
  through"), yet the Recent Streams tab already lists this service's stream ids (`crit-001`,
  `crit-deluxe`, `crit-rare`). Rehydrate Aggregate goes further and asks the operator to type a
  fully-qualified CLR type name from memory (`e.g. MyApp.Domain.Trip`).
- **Why it matters:** Every free-text field that accepts only values the system already enumerates is
  a memory test imposed on the user, and it's the most "backend-shaped" friction in the UI.
- **Recommendation:** Make Stream Id a filterable combobox seeded from the recent-streams query (type
  a character or two to filter, still allow free-type). You already ship a filterable `el-select` (the
  service picker), so this reuses an existing component. Make Aggregate Type a typeahead of the known
  aggregate types for the selected service (you already enumerate them for the Stepper's projection
  dropdown). Bonus: make stream ids in Recent Streams clickable so they deep-link into the Stepper
  pre-filled.

### 2. Collapse the per-row error wall (and the immutable-record crash behind it)
**Target:** beta · **Effort:** M
**Where:** `shots/st-stocklevel-2-after-run.png`

- **Observation:** Stepping `StockLevel` renders the full CLR exception in every one of ~502 rows,
  identical, all red ("Cannot dynamically create an instance… No parameterless constructor defined for
  this object"). One root cause, 502 copies.
- **Why it matters:** It reads as catastrophic and buries the single actionable fact. The underlying
  functional gap is bigger than presentation: the stepper rehydrates by dynamically constructing the
  aggregate, which fails on immutable record aggregates, the very idiom the Critter Stack recommends.
  This will hit nearly every modern aggregate.
- **Recommendation:** Collapse identical consecutive errors into one banner ("Rehydration failed for
  all 502 events: `StockLevel` has no parameterless constructor") with a single expandable trace, and
  stop at the first hard error instead of grinding through 502. Better still, construct via the
  registered `Create`/serializer path, or detect this case and show a first-class "this aggregate
  isn't steppable" message.

### 3. Two service selectors that can disagree
**Target:** beta (relabel), then v1.1 (sync) · **Effort:** S/M
**Where:** `shots/00-dashboard.png` (header), `shots/04-durability.png`, `shots/07-explorer.png`

- **Observation:** There's a global "All Services" selector in the header (every page) and a second
  service selector inside several pages. Durability shows a top-right "All Services" duplicate, and the
  Explorer has its own per-service picker that can read `Inventory` while the header still says
  `All Services`.
- **Why it matters:** Two controls with the same label and no visible relationship is a "which one
  wins?" puzzle. On the Explorer they can hold contradictory scopes.
- **Recommendation:** Pick one ownership model. Either page-level pickers inherit and sync from the
  header scope, or remove the redundant page-level "All Services" on Durability and, on the inherently
  single-service Explorer, relabel it "Viewing service:" (and consider hiding the global header picker
  there so they can't conflict). The label change alone is beta-sized.

### 4. Inline projections shown as `Unknown` in async-shaped columns
**Target:** v1.1 · **Effort:** S/M
**Where:** `shots/03-projections.png`, `shots/ex-orders-projection-statuses.png`

- **Observation:** The global Projections table has `Progress/Gap`, `State`, `Mode`, and `Shards` (all
  async concepts), and inline projections, which are the majority, fill them with `Unknown` / `-`. That
  makes a healthy system look broken or maximally lagged.
- **Why it matters:** "Unknown" implies it should be known but isn't. For an inline projection the
  honest answer is "not applicable": it runs in the write transaction and has no async progress.
- **Recommendation:** Render inline rows as "n/a" with a tooltip ("Inline projections run inside the
  write transaction, so there's no async progress to report"). You already model this better in the
  Explorer's `Lifecycle`-led tab, so pull that framing forward. Minor: this table still clips columns
  past the right edge even at 2000px wide, so consider column priority or hide-on-narrow.

### 5. Idle, empty-result, and error states look identical
**Target:** v1.1 · **Effort:** M
**Where:** `shots/st-leaderboard-2-after-run.png`

- **Observation:** In the Stepper, the same "open box" prompt shows whether you haven't run yet or you
  ran and got zero rows. For example, stepping the multi-stream `ProductSalesLeaderboard` by a Stream
  id returns "No timeline rows / Step 0/0", which looks identical to not having acted.
- **Why it matters:** The operator can't tell "I haven't acted" from "I acted and nothing matched"
  from "I chose the wrong source mode."
- **Recommendation:** Give the three states distinct treatments. For zero-rows, say why: "Stream
  source produced no events for `ProductSalesLeaderboard`. This is a multi-stream projection, so try
  Tag Query or Stream Slice." One sentence turns a dead end into a guided next step.

### 6. Toggle-vs-button consistency
**Target:** beta · **Effort:** S
**Where:** `shots/02-topology.png` (checkboxes), `shots/03-projections.png` ("Flat" switch), `shots/09-dead-letters.png` (auto-refresh switch)

- **Observation:** Binary on/off is sometimes a switch (Projections "Flat", DLQ "Auto-refresh") and
  sometimes a checkbox (Topology "Metrics", "Alerts"), yet all four apply immediately. Same behavior,
  two controls. The "Flat" switch is also unlabeled beyond the word, so its effect (flatten the
  grouped-by-service list) isn't discoverable.
- **Why it matters (your direct questions):** The segmented Source button group is correct: three
  visible options that would be worse hidden in a dropdown, so keep it. For binary state, standardize
  on a labeled switch (state word visible) and reserve checkboxes for "include in a set I'll submit."
  Today nothing submits, so the checkboxes should be switches.
- **Recommendation:** One binary-toggle pattern app-wide, plus a tooltip on "Flat."

### 7. Inline help where the vocabulary leaks
**Target:** beta, then v1.1 (incremental) · **Effort:** S each
**Where:** `shots/03-projections.png`, `shots/02-topology.png`, `shots/ex-inventory-rehydrate-aggregate.png`, `shots/04-durability.png`

- **Observation:** This is a backend tool surfacing Marten/Wolverine internals to operators:
  `High Water Mark`, `Mode: continuous`, `Shards`, `DCB Tag Query`, `Rehydrate`,
  `Inbox/Outbox/Handled`. There's one global ⓘ for the whole app, but no help at the point of
  confusion.
- **Why it matters:** An operator who isn't a Critter Stack expert hits undefined jargon right at the
  point of making a decision.
- **Recommendation:** Add a small ⓘ popover next to the leakiest column headers and tab names, the
  same pattern you already use for "Progress / Gap." Highest-value targets: `High Water Mark`,
  `DCB Tag Query`, the three Source modes, Rehydrate's full-name requirement, and `Inbox/Outbox/Handled`.
  Ship one at a time.

### 8. Show the data first, tuck the apparatus (noise)
**Target:** v1.1 / v1.2 · **Effort:** S/M
**Where:** `shots/09-dead-letters.png`, `shots/03-projections.png`, `shots/12-store-inspector.png`

- **Observation:** On DLQ, an 8-control filter panel and two rows of bulk-action buttons sit above a
  single summary row, so the filter machinery dwarfs the 8 dead letters (same on Topology). The bulk
  buttons (Pause/Restart/Rebuild Selected; Replay/Discard Selected) are shown prominently but stay
  inert until a row is selected, so they're dead affordances drawing the eye. Store Inspector is plainly
  a developer/debug view (raw `batched_web_socket_payload` SignalR frames plus an accordion of internal
  caches).
- **Recommendation:**
  - Collapse Filters into a disclosure showing an active-filter count, and surface the table first.
  - Hide bulk-action buttons until a selection exists, or show a contextual action bar on selection
    ("3 selected · Pause | Restart | Rebuild").
  - Label or gate Store Inspector behind a "developer mode." It's gold for a developer and noise for an
    operator, so don't default both audiences into it.

---

## If you only do five things in beta

1. **Stream Id becomes a searchable picker** seeded from Recent Streams (entry 1). Biggest "it knows
   this already" win.
2. **Collapse the StockLevel error wall** into one banner, and stop on the first error (entry 2).
3. **De-duplicate or relabel the service selectors** so they can't disagree (entry 3).
4. **Show inline projections as `n/a` with a tooltip** instead of `Unknown` (entry 4). Stops healthy
   systems looking broken.
5. **Inline ⓘ popovers** on the five leakiest terms (entry 7). Incremental, one a day.

---

## Appendix: screenshot index and regeneration

All 27 captures, their surface mapping, and the one-command regeneration recipe are in
[`cw-screenshots/README.md`](cw-screenshots/README.md). A baseline ("before") set is the same console
with `Cw__Telemetry=false`: inline-only, no async progress, no fan-out topology. Capture it by
flipping that flag and re-running the capture script. The before/after pair is itself a useful artifact
for the async-projection surfaces.
