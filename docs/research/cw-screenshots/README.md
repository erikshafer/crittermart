# CritterWatch screenshots — JasperFx feedback evidence

Automated full-page captures of the **CritterWatch** console for the UI/UX feedback packet to
JasperFx (Jeremy / Babu). Part of the CW-telemetry spike (`research/cw-telemetry-spike`); see
[`../cw-telemetry-fodder.md`](../cw-telemetry-fodder.md) for why the console is lit and what each
spike shape exercises.

These were captured against the **now-lit** console — async daemon + `OrderPlacedSignal` broadcast
+ poison path running under `Cw__Telemetry=true`, after driving
`docs/demo-traffic.ps1 -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 7`. A baseline ("before") set
can be produced by setting `Cw__Telemetry=false` and re-running the capture.

## Regenerating

```powershell
$env:NODE_PATH = "C:\Code\crittermart\client\node_modules"   # reuse the SPA's Playwright + chromium
$env:CW_BASE   = "http://localhost:5104"                      # Aspire proxy of critterwatch-console
$env:CW_OUT    = "C:\Code\crittermart\docs\research\cw-screenshots\shots"
node C:\Code\crittermart\docs\research\cw-screenshots\capture-cw.cjs
```

Prereqs: the AppHost stack is up on this branch (so `Cw__Telemetry=true` is wired) and traffic has
run so the surfaces have data. The script writes `shots/*.png` + `shots/manifest.json`.

### How it drives the console (notes for the next editor)

CW is an **Element-Plus (Vue) SPA** served at `http://localhost:5104`. Every server path returns the
same shell; it is client-routed. Consequences the script works around:

- **Navigate by route** — the discovered map: `/` `services` `topology` `projections` `durability`
  `listeners` `events` `explorer` `scheduled` `dlq` `timeline` `audit-log` `raw`.
- **Service / projection pickers are `<el-select>`** — the readonly `<input>` is covered by a
  placeholder span, so click the `.el-select__wrapper` and pick a `.el-select-dropdown__item`. The
  explorer's service select is the wrapper at y≈88 (index 0 is the global "All Services" header one).
- **Stepper option labels are `name+lifecycle`** with no space (e.g. `StockLevelInline`).

## The shots (27)

### Top-level surfaces

| File | Surface |
|---|---|
| `00-dashboard.png` | System Overview — Total Services, Global Msgs/hr, DLQ Total, Top-5 busiest |
| `01-services.png` | Connected Services |
| `02-topology.png` | Message-flow topology (incl. the `OrderPlacedSignal` fan-out edge) |
| `03-projections.png` | Projections & Subscriptions — **inline rows render Health/State = Unknown** next to lit Async rows |
| `04-durability.png` | Inbox / Outbox monitor |
| `05-listeners.png` | Listeners & Endpoints |
| `06-events.png` | Event Store feed |
| `07-explorer.png` | Event Store Explorer (lands on Catalog / Recent Streams) |
| `08-scheduled.png` | Scheduled messages |
| `09-dead-letters.png` | Dead Letter Queue — **8 × `PoisonPing` / `InvalidOperationException`** |
| `10-timeline.png` | System timeline |
| `11-audit-log.png` | Audit log |
| `12-store-inspector.png` | Store Inspector (`/raw`) |

### Event Store Explorer, per service

`ex-orders-*` and `ex-inventory-*`, each across the five tabs: `recent-streams`, `stream-events`,
`projection-statuses`, `projection-stepper`, `rehydrate-aggregate`.

- `ex-orders-projection-statuses.png` — the `Lifecycle` column cleanly tags our three async spike
  projections (`ProductSalesLeaderboard`, `OrderLineItemsProjection`, `CartAbandonmentDailyReport`).

### Projection Stepper (driven)

| File | Shows |
|---|---|
| `st-stocklevel-1-configured.png` | Stepper set to Inventory `StockLevel`, stream `crit-001`, before Run |
| `st-stocklevel-2-after-run.png` | **Every row red** — repeated `No parameterless constructor` CLR exception on the immutable record aggregate (Step 502/502) |
| `st-leaderboard-1-configured.png` | Stepper set to Orders `ProductSalesLeaderboard`, stream `crit-001`, before Run |
| `st-leaderboard-2-after-run.png` | **"No timeline rows / Step 0/0"** — multi-stream projection won't step by Stream id, no hint why |

## Which shot anchors which feedback seed

The five seeds are defined in [`../cw-telemetry-fodder.md`](../cw-telemetry-fodder.md#seeds-for-the-jasperfx-feedback-artifact).

| Seed | Anchored by |
|---|---|
| #1 Inline projections shown as max-lagged / Unknown | `03-projections.png`, `ex-orders-projection-statuses.png` |
| #2 Per-row exception spam | `st-stocklevel-2-after-run.png` |
| #3 Stepper can't rehydrate immutable record aggregates | `st-stocklevel-2-after-run.png` |
| #4 Two service selectors that can disagree | `07-explorer.png` (explorer select) vs the global header select |
| #5 "Nothing changed" indistinguishable from a no-op | `st-leaderboard-2-after-run.png` |
