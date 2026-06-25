# Research: CritterWatch telemetry fodder

**Status:** active spike · **Branch:** `research/cw-telemetry-spike` · **Date:** 2026-06-24
**Owner:** Erik · **Purpose:** generate richer, realistic telemetry so a CritterWatch (CW) UI/UX
review rests on a *fully-lit* console, then feed concrete change requests back to JasperFx
(Jeremy / Babu), who are explicitly seeking larger real-world projects for CW feedback.

> This is **research**, not a round-one deliverable. It deliberately relaxes a round-one
> constraint (ADR 008: no async daemon) **on this branch only**. It is not intended to merge to
> `main`. ADR 008 and the teaching baseline stand untouched there.

---

## Why this spike exists

The first CW review (10 screenshots of the Event Store Explorer, 2026-06-24) was hard to ground
because **CritterMart emits almost no live telemetry for CW to show**:

1. **Every domain projection is inline.** Inline projections run inside the append transaction —
   they have no shard, no async progress, no lag, no error surface. CW's Projection Statuses tab
   therefore shows every row as `Unknown / Processed 0 / Lag = Head`, which *looks* catastrophic
   but is just "inline projections have nothing async to report."
2. **The one async projection never runs.** `CartAbandonmentReportProjection` is registered
   `ProjectionLifecycle.Async`, but ADR 008 means there is **no `AddAsyncDaemon` anywhere**, so it
   is inert. CritterMart's async telemetry is, today, exactly zero.
3. **`StockLevel` breaks the Projection Stepper.** It is a `sealed`, immutable, self-aggregating
   record (the modern Critter Stack aggregate idiom, ADR 020). CW's stepper rehydrates by
   dynamically constructing an instance, which needs a parameterless constructor that immutable
   records don't expose — so the stepper throws `No parameterless constructor defined` on every
   row. CW fails on precisely the aggregate shape JasperFx steers people toward. **(Feedback gold —
   no spike code needed; this is reproducible on `main` today.)**

So: to review CW fairly we must first give it real things to monitor.

---

## What lights up which CW surface

| CW surface (currently dark / misleading) | What lights it | Spike shape |
|---|---|---|
| Projection Statuses — real shards, lag climb→drain, rebuild | a **running async daemon** | daemon ON (gated) + the two async projections |
| Projection Stepper — Stream-Slice / Tag-Query source modes | a **multi-stream** projection | `ProductSalesLeaderboard` (fan-out) |
| Dead Letters + the always-`—` Error column | an **actual failure** | `PoisonPing` poison path |
| Store Inspector — non-JSONB read models | a **flat / SQL** projection | `OrderLineItemsProjection` (`EventProjection`) |
| Topology edges · Listeners queues · Durability inbox/outbox | **multi-subscriber** messaging | `OrderPlacedSignal` broadcast → Inventory + Catalog |
| Stepper on record aggregates | already exercised — and *fails* | `StockLevel` (feedback entry, no code) |

---

## The four shapes (+ the daemon)

All live under `CritterMart.Orders.Analytics`, `CritterMart.Orders.Spike`,
`CritterMart.Contracts`, and `*/Spike` in Inventory/Catalog. Every file is header-commented
`CW-TELEMETRY SPIKE`.

1. **`ProductSalesLeaderboard`** — `MultiStreamProjection`, **fan-out** (`Identities<IEvent<OrderPlaced>>`):
   one `OrderPlaced` → one document **per SKU**. The mirror of `CartAbandonmentReport`
   (many-streams → one-doc). Async.
2. **`OrderLineItemsProjection`** — `EventProjection` writing raw SQL into the flat table
   `orders.order_line_items` (one row per order line; carries `placed_at` + `event_sequence`
   metadata that the declarative `FlatTableProjection` DSL can't reach). Async.
3. **`PoisonPing`** — a self-contained message whose handler always throws; `POST /spike/poison`
   cascades it, Wolverine dead-letters it. Populates Dead Letters + the Error column without
   touching a real domain stream.
4. **`OrderPlacedSignal`** — a Contracts broadcast published by Orders on each placed order
   (gated), fanned out to **two** subscribers (Inventory + Catalog). Gives Catalog its first
   cross-BC edge; thickens Topology / Listeners / Durability.

**Daemon:** `martenConfig.AddAsyncDaemon(DaemonMode.Solo)` in Orders, gated on `Cw:Telemetry`.

---

## The one toggle: `Cw:Telemetry`

A single flag (env `Cw__Telemetry`, set on the `orders` AppHost resource) gates the spike's
**active** behaviour — the async daemon and the `OrderPlacedSignal` broadcast.

- **OFF** → reproduces the baseline CW picture: inline-only, no async progress, no fan-out
  topology. This is the "before" screenshot set.
- **ON** → daemon turns, the three async projections (including the previously-inert
  `CartAbandonmentReport`) catch up from the event head — so you can *watch lag climb then drain*
  in CW — and every placed order broadcasts. This is the "after" set.

The before/after pair is itself a deliverable for the JasperFx packet.

---

## How to run

1. `git checkout research/cw-telemetry-spike`
2. `dotnet run --project src/CritterMart.AppHost` (Aspire boots the stack; `Cw__Telemetry=true`
   is wired on `orders`).
3. Drive traffic with the **CW-spike profile** of the traffic generator, which now exercises every
   spike surface in one run:
   `./docs/demo-traffic.ps1 -Continuous -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 7`
   - `-LinesPerOrder` / `-MaxQuantity` make happy orders multi-line, multi-unit → the fan-out
     leaderboard accumulates per-SKU at divergent rates and the flat table fills with >1 row/order.
   - `-PoisonEvery` fires `POST /spike/poison` on a cadence → Dead Letters + the Error column climb.
   - Each placed order broadcasts `OrderPlacedSignal` (gated) → the Topology/Listeners edge stays warm.
   - Defaults (no flags) reproduce the talk's single-line, decline-every-5th flow — the spike richness
     is opt-in, so the same script serves the demo and the spike.
4. (Optional, manual) hit `POST /spike/poison` directly from Orders Swagger for an ad-hoc dead letter.
5. Open CW (`localhost:<port>/explorer`) and walk: Projection Statuses (lag draining),
   Projection Stepper on `ProductSalesLeaderboard`, Dead Letters, Store Inspector on
   `order_line_items`, Topology (Catalog's new edge).
6. Screenshot for the feedback packet.

To see the "before" baseline picture, set `Cw__Telemetry` to `false` and restart.

---

## Checklist

- [x] `ProductSalesLeaderboard` fan-out projection (async)
- [x] `OrderLineItemsProjection` flat-table EventProjection (async)
- [x] `PoisonPing` poison path + `POST /spike/poison`
- [x] `OrderPlacedSignal` broadcast + Inventory & Catalog subscribers
- [x] Daemon gated on `Cw:Telemetry` (Orders)
- [x] `Cw__Telemetry=true` on the `orders` AppHost resource
- [x] Traffic generator (`docs/demo-traffic.ps1`) extended to drive the new surfaces — opt-in
      `-LinesPerOrder` / `-MaxQuantity` (multi-line/multi-unit → fan-out + flat table) and
      `-PoisonEvery` (→ Dead Letters); defaults preserve the talk's single-line flow
- [x] Solution builds clean (0 warnings / 0 errors)
- [x] Live-verified against a running stack (2026-06-24) — see below
- [x] Screenshots captured (lit/"after" set) — 27 automated full-page captures of every CW surface
      incl. the driven StockLevel stepper failure, via `cw-screenshots/capture-cw.cjs` (Playwright).
      See [`cw-screenshots/README.md`](cw-screenshots/README.md). Baseline ("before",
      `Cw__Telemetry=false`) set not yet captured.
- [x] JasperFx feedback artifact drafted from the lit console —
      [`cw-feedback-jasperfx.md`](cw-feedback-jasperfx.md): 8 prioritized, screenshot-anchored UI/UX
      entries (the 5 seeds + 3 new themes), with a beta shortlist, for Jeremy/Babu.

## Live-verification results (2026-06-24)

Booted the stack on this branch (`Cw__Telemetry=true`), drove ~20 orders + a sustained
continuous loop, fired 5 poison messages. Verified directly against the Orders Postgres:

| Mechanism | Evidence |
|---|---|
| Async daemon running | `orders.mt_event_progression`: `HighWaterMark` + all 3 async shards caught up to head |
| **CartAbandonment teaser revived** | `CartAbandonmentDailyReport:All` now advancing (inert on `main` — ADR 008) |
| Fan-out projection correct | `mt_doc_productsalesleaderboard`: exactly 2 docs (`crit-001`, `crit-deluxe`), per-SKU units/revenue |
| Flat table | `orders.order_line_items`: one SQL row per order line, with `placed_at` + `event_sequence` metadata; decline rows show inflated price |
| Poison → Dead Letters | `orders.wolverine_dead_letters`: 5 × `CritterMart.Orders.Spike.PoisonPing` |
| Broadcast fan-out | RabbitMQ: fanout exchange + queue `CritterMart.Contracts.OrderPlacedSignal`, **2 consumers** (Inventory + Catalog) |

**Known nuance — competing consumers, not broadcast-to-all.** Wolverine conventional routing
derives the SAME queue name in both Inventory and Catalog, so they share ONE queue (2 competing
consumers) rather than each binding its own queue. Each `OrderPlacedSignal` is load-balanced to one
service, not copied to both. Valid topology for CW to render, but if a true per-service fan-out is
wanted, give each subscriber an explicitly-named listening queue bound to the fanout exchange.

**Re-verified via the extended traffic generator (2026-06-24 pt2).** Ran
`demo-traffic.ps1 -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 3` against the live stack and
confirmed the new shapes landed using a confound-proof signature: the *old* single-line/single-unit
script can only ever write `quantity = 1` and one line per order, so any flat-table row with
`quantity > 1` or any `order_id` with >1 line is unambiguously script-driven even with the stray
continuous loop still running. Result: 6 multi-unit lines + 3 multi-line orders in the window
(matching the console log order IDs), and poison dead letters climbed by exactly the number fired.

---

## Teardown

This branch is not for `main`. When the feedback packet is done, either keep the branch as a
durable research record or delete it. If any shape proves worth keeping in the teaching baseline,
that is a deliberate **ADR-008 revisit** on its own PR — not a quiet merge of this spike.

---

## Seeds for the JasperFx feedback artifact

1. **Inline projections shown as max-lagged.** Inline rows report `Lag = Head`, making a healthy
   system look 34k events behind. Inline projections should read `n/a` for shard/lag, not borrow
   the async model and fail it.
2. **Per-row exception spam.** A projection rehydration error (`StockLevel`, no parameterless
   ctor) prints the full CLR exception into every row, repeated, in red. Wants a single banner +
   neutral per-row chips.
3. **Stepper can't rehydrate immutable record aggregates.** The no-parameterless-ctor failure is
   on the *recommended* aggregate idiom. Either construct via the registered
   `Create`/serializer path, or surface a first-class "this aggregate isn't steppable" message.
4. **Two service selectors that can disagree** (header `Catalog` vs explorer `Inventory`).
5. **"Nothing changed, and that's correct" is indistinguishable from a no-op** — needs a
   "this event type isn't handled by this projection" hint.
