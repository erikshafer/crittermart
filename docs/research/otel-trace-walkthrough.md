---
version: v1.0
status: Active
date: 2026-06-17
references:
  - docs/decisions/005-opentelemetry-tracing-enabled.md
  - docs/decisions/004-dotnet-aspire-orchestrator.md
  - docs/decisions/003-wolverine-rabbitmq-transport.md
  - docs/workshops/001-crittermart-event-model.md
  - docs/narratives/004-customer-purchase.md
  - docs/retrospectives/chore/002-infra-bundle-aspire-otel.md
---

# OpenTelemetry Trace Walkthrough — the cross-service purchase

> Compiled 2026-06-17 in the `feat: complete OTel teaching pass` session (implementations/027).
> This is a **teaching artifact** — the in-browser trace visual that
> [chore/002's retro](../retrospectives/chore/002-infra-bundle-aspire-otel.md) (item #4)
> left owed to the user: "*Dashboard/trace visual confirmation = user (browser)*". It pairs
> a CLI-confirmed reproduction of the W1→W4 purchase journey with the exact Aspire-dashboard
> navigation for capturing the trace + metric screenshots that go on the talk's slides.
>
> It is **research / talk-evidence**, not a decision and not a build order.

## Why this exists

The talk's thesis (ADR 005) is that *event sourcing across services produces a coherent,
traceable story*. The infrastructure to show it — Aspire's dashboard collector, Wolverine +
Marten OpenTelemetry sources, OTLP export — landed in [chore/002](../prompts/chore/002-infra-bundle-aspire-otel.md).
But two things were missing for the visual to actually *teach*:

1. **Marten's event-store layer was invisible in the trace.** ADR 005 named
   `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` + `TrackEventCounters()`, but
   chore/002 only realized the ASP.NET + Wolverine + HttpClient half (its retro item #3 logged the
   Marten half as a deferred "fast follow"). Without it the trace stopped at HTTP and Wolverine
   message spans — you could see *that* a handler ran, but not the event appends it committed.
2. **The metrics meters were never registered.** `ConfigureOpenTelemetry` added the `"Wolverine"`
   and `"Marten"` *ActivitySources* (tracing) but no *meters*, so `marten.event.append` and the
   Wolverine message counters emitted into a void (the
   [opentelemetry-setup skill](../skills/) flags this exact "source without meter" anti-pattern).

implementations/027 closed both (see [ADR 005](../decisions/005-opentelemetry-tracing-enabled.md),
now fully realized). This document is the proof-of-life and the screenshot guide.

## What changed in implementations/027

| File | Change |
|---|---|
| `src/CritterMart.{Catalog,Inventory,Orders}/Program.cs` | `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose;` + `opts.OpenTelemetry.TrackEventCounters();` in each `AddMarten` block (`using JasperFx.OpenTelemetry;` — the `TrackLevel` enum lives there in Marten 9, not `Marten`). |
| `src/CritterMart.ServiceDefaults/Extensions.cs` | `WithMetrics` now registers `.AddMeter("Marten")` + `.AddMeter("Wolverine:*")`. The Wolverine **meter** name is `Wolverine:{ServiceName}` (≠ the `"Wolverine"` ActivitySource), so the wildcard catches `Wolverine:Catalog/Inventory/Orders` from the shared ServiceDefaults without hardcoding a name. |

## The reproduction (CLI-confirmed)

Boot the stack (`dotnet run --project src/CritterMart.AppHost --launch-profile http`), wait for
all three services to report `Healthy` (`/health` on `:5101/:5102/:5103`), then drive:

```pwsh
$sku = "OTEL-DEMO-001"; $cust = "cust-otel-001"
# 1. Catalog (:5101) — publish a product
Invoke-RestMethod -Method Post -Uri "http://localhost:5101/products" -ContentType application/json `
  -Body '{"sku":"OTEL-DEMO-001","name":"Telemetry Tortoise","description":"A trace-loving critter","price":42.50}'
# 2. Inventory (:5102) — receive stock
Invoke-RestMethod -Method Post -Uri "http://localhost:5102/stock/$sku/receipts" -ContentType application/json `
  -Body '{"quantity":100}'
# 3. Orders (:5103) — add to cart (identity via X-Customer-Id header; productSnapshot is the cart's only product truth)
Invoke-RestMethod -Method Post -Uri "http://localhost:5103/carts/mine/items" -ContentType application/json -Headers @{ "X-Customer-Id" = $cust } `
  -Body '{"sku":"OTEL-DEMO-001","quantity":2,"productSnapshot":{"name":"Telemetry Tortoise","price":42.50}}'
# 4. Orders (:5103) — place the order; this one call cascades the whole saga
Invoke-RestMethod -Method Post -Uri "http://localhost:5103/orders" -ContentType application/json `
  -Body '{"customerId":"cust-otel-001"}'
```

**Observed result (2026-06-17):** order reached `confirmed`; stock moved `available 100 → 98`,
`reserved 2 → 0`, `committed 0 → 2`. No telemetry/export errors in the boot log. The single
`POST /orders` produced the cross-service trace below.

## The trace you will see — span hierarchy

One `POST /orders` is the trace root. It fans out across all three services and back, tied
together by the **`CorrelationId`** Wolverine propagates onto every envelope it sends over
RabbitMQ (so the Inventory spans share the originating request's trace id):

```
POST /orders                                    (Orders — ASP.NET Core span)
└─ PlaceOrder handler                           (Orders — Wolverine)
   ├─ marten.connection  [StartStream Order: OrderPlaced]   ← verbose: the append is tagged here
   ├─ marten.connection  [Cart: CartCheckedOut]             ← the multi-stream atomic write
   └─ send ReserveStock ───────────────────────► (RabbitMQ, correlation propagated)
        └─ ReserveStock handler                 (Inventory — Wolverine)
           ├─ marten.connection [StockReserved × line]
           └─ send StockReserved ──────────────► (RabbitMQ, back to Orders)
                └─ StockReserved handler         (Orders — Wolverine)
                   ├─ marten.connection [Order: StockReserved]
                   └─ AuthorizePayment           (Orders — in-process, local routing)
                      └─ PaymentDecision          (Orders — in-process; stubbed provider approves)
                         ├─ marten.connection [Order: PaymentAuthorized, OrderConfirmed]
                         └─ send CommitStock ────► (RabbitMQ, to Inventory)
                              └─ CommitStock handler   (Inventory — Wolverine)
                                 └─ marten.connection [StockCommitted × line]
```

Three broker hops (`ReserveStock`, `StockReserved`, `CommitStock`) + two in-process hops
(`AuthorizePayment`, `PaymentDecision`) under one trace id. The `marten.connection` spans —
new this session — are where the event appends become visible; before implementations/027 the
trace stopped at the Wolverine handler spans.

## The metric you will see

`marten.event.append` (a Counter, tagged `event_type` + `tenant.id`) ticks once per appended
event across the journey:

| Service | event_type values appended in the journey |
|---|---|
| Catalog | `ProductPublished` |
| Inventory | `StockReceived`, `StockReserved`, `StockCommitted` *(and `StockReleased` on the decline path)* |
| Orders | `CartCreated`, `CartItemAdded`, `CartCheckedOut`, `OrderPlaced`, `StockReserved`, `PaymentAuthorized`, `OrderConfirmed` |

Grouping the counter by `event_type` on the Metrics screen gives a live histogram of the
domain's event vocabulary — a strong teaching beat alongside the trace.

## Capturing the screenshots (Aspire dashboard)

The dashboard is at **`http://localhost:15090`**. The login token is **per-boot** — copy the
`login?t=…` URL printed in the AppHost console / boot log each time. The dashboard's telemetry is
**in-memory and ephemeral**: it is captured live while the stack runs and is gone on teardown, so
screenshot before tearing down (or re-boot and re-drive with the script above).

1. **Distributed trace (the money shot).** Left nav → **Traces**. Find the row for `POST /orders`
   (or filter by the orderId). Click it to open the **span waterfall** — this is the screenshot
   that shows the request crossing Orders → Inventory → Orders → Inventory in one timeline.
   Expand a `marten.connection` span to show the tagged write op.
2. **Structured logs / scopes.** Left nav → **Structured** (Console logs) — filter by the same
   trace id to show the correlated log lines per service.
3. **Event-append metric.** Left nav → **Metrics** → select the **Orders** (or Inventory)
   resource → meter **`Marten`** → instrument **`marten.event.append`**. Group/split by
   `event_type` to show the per-event-type counts.
4. **Wolverine throughput (optional).** Same Metrics screen → meter **`Wolverine:Orders`** →
   `wolverine-messages-succeeded` / `wolverine-execution-time` to show the message pipeline.

## Caveats / non-goals

- The Aspire dashboard is the round-one sink (ADR 005). A production setup would route OTLP to a
  durable backend (Jaeger / Tempo / Grafana). Out of scope here.
- `TrackLevel.Verbose` is the *teaching* level (it tags every write op); a production deployment
  would likely use `TrackLevel.Normal` to cut span volume.
- This artifact does not embed images — by the chore/002 precedent, the owner captures the slide
  screenshots in-browser. The navigation above is the guide for doing so.
