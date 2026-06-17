---
title: CritterMart Demo & Smoke-Test Runbook
status: Active
date: 2026-06-17
audience: human operator OR AI coding agent
references:
  - README.md (§ Getting Started — prerequisites + the bare run command)
  - docs/research/otel-trace-walkthrough.md (the OpenTelemetry trace deep-dive + screenshot guide)
  - docs/decisions/004-dotnet-aspire-orchestrator.md
  - docs/decisions/005-opentelemetry-tracing-enabled.md
  - docs/decisions/017-critterwatch-integrated.md
---

# CritterMart — Demo & Smoke-Test Runbook

> **What this is.** A repeatable, copy-pasteable procedure to boot the whole CritterMart stack,
> drive a real order end-to-end (happy path **and** the cancel saga), and verify every demo
> surface renders — the Swagger UIs, the Aspire dashboard, the CritterWatch console, and the
> storefront SPA. It is the written form of the smoke test, usable by a human before a talk or by
> an AI coding agent verifying a change.
>
> **Bottom line:** one `dotnet run` + the seed + one `POST /orders` proves the entire
> event-sourced, message-driven system — handlers, cross-BC RabbitMQ hops, projections, and the
> compensation path — is working. Aspire's Postgres is **ephemeral** (fresh empty DB every boot),
> so the seed must be re-run each boot. Seed *automation* does not exist yet (see
> [§ Known gaps](#known-gaps)); this runbook seeds manually.
>
> For the OpenTelemetry trace span-hierarchy + the metrics/screenshot guide, this runbook hands
> off to [`docs/research/otel-trace-walkthrough.md`](research/otel-trace-walkthrough.md).

Commands are **PowerShell** (the author's shell; the repo is developed on Windows). A portable
`curl` quick-reference is in [§ curl quick reference](#curl-quick-reference). Anything an AI agent
should re-derive rather than trust blindly is flagged **[verify]**.

---

## Prerequisites

- **Docker Desktop running** — Aspire orchestrates PostgreSQL and RabbitMQ as containers; nothing
  boots without it.
- **.NET 10 SDK** (see [README § Prerequisites](../README.md#prerequisites)).
- Repo on the branch/commit you want to demo (the talk demos `main`).
- Ports free: **5101, 5102, 5103** (services), **5173** (SPA), **15090** (Aspire dashboard). The
  CritterWatch console takes a **dynamic** port (see [§ URLs & ports](#urls--ports)).

---

## Step 0 — Clean slate (do this before a talk)

Orphaned processes from a previous boot lock build DLLs (`MSB3026` on the next build) and squat on
`:5173`. `Stop-Process` on the CritterMart services does **not** reap the Vite dev server's `node`
workers, which accumulate across boots.

```powershell
# Kill leftover service hosts (safe — only CritterMart.* processes)
Get-Process -Name CritterMart.* -ErrorAction SilentlyContinue | Stop-Process -Force

# Vite/node orphans: inspect first — only sweep if you have no OTHER node apps running
Get-Process -Name node -ErrorAction SilentlyContinue | Measure-Object | % Count
# If that number is high and you're sure: Get-Process -Name node | Stop-Process -Force
```

> A full machine reboot is the surest clean slate before a talk. Mass-killing `node` can take down
> an editor or Electron app (e.g. Claude Desktop), so do it deliberately, not reflexively.

---

## Step 1 — Boot the stack

One command boots Postgres, RabbitMQ, the CritterWatch console, all three services, and the SPA:

```powershell
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"
dotnet run --project src/CritterMart.AppHost --launch-profile http
```

The `http` launch profile already sets this env var; exporting it first is belt-and-suspenders for
a background/detached launch. Boot order is gated: `postgres` → `rabbitmq` + the `critterwatch`
database → **`critterwatch-console`** → `catalog`/`inventory`/`orders` (each `WaitFor`s the console)
→ `storefront` (waits for the three services). First boot pulls container images; allow a couple of
minutes. **[verify]** the resource list against `src/CritterMart.AppHost/Program.cs` — it is the
source of truth for what boots and how it's wired.

**Capture the dashboard login URL** printed in the console (the token is **per-boot**):

```
Login to the dashboard at http://localhost:15090/login?t=<token>
```

> **AI agent:** launch this in the background with output redirected to a log, then poll (next
> step) and `Select-String` the log for the `login?t=` line — do not block on the foreground
> process.

---

## Step 2 — Wait for healthy

Poll the three service health endpoints until all report `Healthy`:

```powershell
$ports = @{ Catalog=5101; Inventory=5102; Orders=5103 }
$deadline = (Get-Date).AddSeconds(240); $ready = @{}
while ((Get-Date) -lt $deadline -and $ready.Count -lt 3) {
  foreach ($s in $ports.Keys) {
    if (-not $ready[$s]) {
      try { if ((Invoke-WebRequest "http://localhost:$($ports[$s])/health" -TimeoutSec 3 -UseBasicParsing).StatusCode -eq 200) {
        $ready[$s] = $true; Write-Host "$s healthy" } } catch {}
    }
  }
  if ($ready.Count -lt 3) { Start-Sleep -Seconds 5 }
}
"ready: $($ready.Count)/3"
```

If a service never comes healthy, tail the boot log for the failure (`fail:`, `exception`).

---

## URLs & ports

| Surface | URL | Port | Notes |
|---|---|---|---|
| **Aspire dashboard** | `http://localhost:15090/login?t=<token>` | 15090 (fixed) | Token is **per-boot** — copy from the console/log. Traces, logs, metrics, resource graph. |
| **Catalog** service | `http://localhost:5101` | 5101 (fixed) | `/` → 302 → `/swagger` |
| **Inventory** service | `http://localhost:5102` | 5102 (fixed) | `/` → 302 → `/swagger` |
| **Orders** service | `http://localhost:5103` | 5103 (fixed) | `/` → 302 → `/swagger` |
| **Storefront SPA** | `http://localhost:5173` | 5173 (fixed) | Vite dev server; the human-facing demo |
| **CritterWatch console** | via dashboard | **dynamic** | `WithExternalHttpEndpoints`, no pinned port — open it from the Aspire dashboard's `critterwatch-console` resource. |
| **RabbitMQ management** | via dashboard | dynamic | Open from the Aspire dashboard's `rabbitmq` resource. |

> **Finding the CritterWatch port from a script (AI agent fallback):**
> ```powershell
> $p = (Get-Process CritterMart.CritterWatch).Id
> Get-NetTCPConnection -OwningProcess $p -State Listen | ? LocalPort -gt 1024 | Select -Expand LocalPort -Unique
> ```

---

## Step 3 — Seed data (manual; re-run every boot)

The ephemeral DB starts empty. Seed a product (Catalog) and stock (Inventory). The order flow does
not strictly need the Catalog row — the storefront snapshots name+price into the cart — but seeding
it makes the SPA's browse page populate.

```powershell
$cat="http://localhost:5101"; $inv="http://localhost:5102"; $sku="crit-001"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }

# 1. Publish a product (Catalog — Marten document store)
Invoke-RestMethod "$cat/products" -Method Post -ContentType application/json `
  -Body (J @{ sku=$sku; name="Cosmic Critter Plush"; description="A plush from the cosmos."; price=24.99 })

# 2. Receive stock (Inventory — event-sourced)
Invoke-RestMethod "$inv/stock/$sku/receipts" -Method Post -ContentType application/json `
  -Body (J @{ quantity=100 })

# Confirm: available=100, reserved=0, committed=0
Invoke-RestMethod "$inv/stock/$sku"
```

**Request shapes** (the authoritative source is `src/**/Features/*.cs`; **[verify]** if a 400 appears):

| Endpoint | Method | Body |
|---|---|---|
| `/products` (Catalog) | POST | `{ sku, name, description, price }` |
| `/stock/{sku}/receipts` (Inventory) | POST | `{ quantity }` |
| `/carts/{customerId}/items` (Orders) | POST | `{ sku, quantity, productSnapshot: { name, price } }` |
| `/orders` (Orders) | POST | `{ customerId }` → returns `{ orderId }` |

---

## Step 4 — Drive an order (happy path)

```powershell
$ord="http://localhost:5103"; $cust="demo-buyer"; $sku="crit-001"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }

# Add to cart (productSnapshot is the cart's only product truth — the cart never reads Catalog)
Invoke-RestMethod "$ord/carts/$cust/items" -Method Post -ContentType application/json `
  -Body (J @{ sku=$sku; quantity=2; productSnapshot=@{ name="Cosmic Critter Plush"; price=24.99 } })

# Place the order — this ONE call cascades the whole cross-BC saga
$orderId = (Invoke-RestMethod "$ord/orders" -Method Post -ContentType application/json -Body (J @{ customerId=$cust })).orderId
"orderId = $orderId"

# Poll to terminal status (the saga runs async over RabbitMQ; usually < 1s)
$o=$null; for ($i=0; $i -lt 25; $i++){ Start-Sleep -Milliseconds 800; $o=Invoke-RestMethod "$ord/orders/$orderId"; if ($o.status -in 'confirmed','cancelled'){break} }
"status = $($o.status)"

# The 'My Orders' list (header-keyed read, GET /orders/mine)
Invoke-RestMethod "$ord/orders/mine" -Headers @{ "X-Customer-Id"=$cust }
```

**Expected:** `status = confirmed`; the order walked `awaiting_confirmation → stock_reserved →
payment_authorized → confirmed`. Stock moves **available 100 → 98, reserved → 0, committed 0 → 2**
(`Invoke-RestMethod "$inv/stock/$sku"`). No service called another directly — Orders cascaded
`ReserveStock`/`CommitStock` messages over RabbitMQ and reacted to the replies. That stock movement
is the proof the message round-trip completed.

---

## Step 5 — Drive the cancel saga (two live routes)

Two of the three cancel routes are triggerable live. Both are the *same handler machinery* as the
happy path, just reacting to a failure event — no special error path.

### 5a — Insufficient stock (`stock_unavailable`)

Order more than the available stock; the reservation refuses.

```powershell
$cat="http://localhost:5101"; $inv="http://localhost:5102"; $ord="http://localhost:5103"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }
$cust="demo-fail"; $sku="crit-rare"

Invoke-RestMethod "$cat/products" -Method Post -ContentType application/json -Body (J @{ sku=$sku; name="Rare Critter"; description="Limited."; price=49.99 })
Invoke-RestMethod "$inv/stock/$sku/receipts" -Method Post -ContentType application/json -Body (J @{ quantity=1 })
Invoke-RestMethod "$ord/carts/$cust/items" -Method Post -ContentType application/json -Body (J @{ sku=$sku; quantity=3; productSnapshot=@{ name="Rare Critter"; price=49.99 } })
$id = (Invoke-RestMethod "$ord/orders" -Method Post -ContentType application/json -Body (J @{ customerId=$cust })).orderId
$o=$null; for ($i=0;$i -lt 25;$i++){ Start-Sleep -Milliseconds 800; $o=Invoke-RestMethod "$ord/orders/$id"; if ($o.status -in 'confirmed','cancelled'){break} }
"status=$($o.status) reason=$($o.cancelReason)"
```

**Expected:** `status=cancelled reason=stock_unavailable`. Stock is **unchanged** — reservation is
all-or-nothing, so a refusal reserved nothing → **no** compensating release.

### 5b — Payment declined (`payment_declined`) — the compensation beat

This is the richer route: stock **is** reserved, then payment declines, so the order cancels **and the
reserved stock is released back** (a compensating `ReleaseStock` to Inventory). It is enabled by the
**`Payment:DeclineOverAmount`** demo affordance (default **$100**, set by the AppHost — see the box
below): order over the threshold → the stub declines.

```powershell
$cat="http://localhost:5101"; $inv="http://localhost:5102"; $ord="http://localhost:5103"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }
$cust="demo-decline"; $sku="crit-deluxe"

Invoke-RestMethod "$cat/products" -Method Post -ContentType application/json -Body (J @{ sku=$sku; name="Deluxe Critter"; description="Premium."; price=24.99 })
Invoke-RestMethod "$inv/stock/$sku/receipts" -Method Post -ContentType application/json -Body (J @{ quantity=100 })
# 5 × $24.99 = $124.95, over the $100 threshold → payment will decline
Invoke-RestMethod "$ord/carts/$cust/items" -Method Post -ContentType application/json -Body (J @{ sku=$sku; quantity=5; productSnapshot=@{ name="Deluxe Critter"; price=24.99 } })
$id = (Invoke-RestMethod "$ord/orders" -Method Post -ContentType application/json -Body (J @{ customerId=$cust })).orderId
$o=$null; for ($i=0;$i -lt 25;$i++){ Start-Sleep -Milliseconds 800; $o=Invoke-RestMethod "$ord/orders/$id"; if ($o.status -in 'confirmed','cancelled'){break} }
"status=$($o.status) reason=$($o.cancelReason)"
"stock after: $(Invoke-RestMethod "$inv/stock/$sku" | ConvertTo-Json -Compress)"
```

**Expected:** `status=cancelled reason=payment_declined`, and stock returns to **available 100,
reserved 0** — it was reserved at the stock gate, then **released back** when payment failed. That
release (`reserved 5 → 0`) is the compensation the audience sees flow back to Inventory in the trace /
CritterWatch. Contrast with 5a, where nothing was reserved so nothing came back.

> ### ⚙️ The `Payment:DeclineOverAmount` demo affordance — read this so it's never a surprise
>
> By default the stubbed payment provider **always approves** (round-one behavior). To make the
> decline route demo-able live, the **AppHost** (`src/CritterMart.AppHost/Program.cs`) injects
> `Payment__DeclineOverAmount = 100` into the Orders service, so the stub declines any order whose
> **total exceeds $100**. This is the *only* thing that makes a decline happen at runtime — the whole
> decline→cancel→release chain is real and was built/tested as slice 4.6.
>
> - **It is ON by default when you run via Aspire.** Orders over $100 **will** cancel with
>   `payment_declined` — that is expected, not a bug.
> - **Change it:** edit the `WithEnvironment("Payment__DeclineOverAmount", "100")` value in the AppHost.
> - **Turn it OFF (restore "always approve"):** delete that one line in the AppHost, or set
>   `Payment:DeclineOverAmount` to empty. **Do this after the talk** if you want production-faithful
>   behavior back.
> - Code: `PaymentDeclinePolicy` + `StubPaymentProvider` in
>   `src/CritterMart.Orders/Ordering/PaymentProvider.cs`; registered in `src/CritterMart.Orders/Program.cs`.
>
> **The third cancel route — payment timeout (`payment_timeout`) — is still config-only and not
> talk-friendly:** it fires only after `Orders:PaymentTimeout` (default **10 min**) elapses. Demo-able
> by shortening that value, but the wait is dead air; prefer the instant decline (5b) on stage.

---

## Step 6 — Verify the demo surfaces

| Surface | How to check | What it shows / talk beat |
|---|---|---|
| **Swagger UI** | Open `http://localhost:5101`, `:5102`, `:5103` (root redirects to `/swagger`). Script: each `/swagger/index.html` returns **200**. | The Wolverine.Http endpoints with OpenAPI **inferred from handler signatures** — the "no controllers, no boilerplate" beat. |
| **Aspire dashboard** | `http://localhost:15090/login?t=<token>` → **Traces** → the `POST /orders` row → span waterfall. | The cross-service trace: Orders → RabbitMQ → Inventory → back, with `marten.connection` spans. **Deep-dive + screenshot guide: [otel-trace-walkthrough.md](research/otel-trace-walkthrough.md).** |
| **CritterWatch console** | Aspire dashboard → `critterwatch-console` resource → open its endpoint. | The Wolverine/Marten monitoring view — messages, handlers, queues, sagas, per-service health. The most on-theme "it's actually working" visual for a messaging talk. **[verify]** it shows the **Trial** tier, not "Development" (it runs with `ASPNETCORE_ENVIRONMENT=Production` so it reads the real license — set in the AppHost). |
| **Storefront SPA** | `http://localhost:5173` | Browse → add to cart → checkout → track → **My Orders**. The human-facing payoff. |
| **Metrics** | Aspire dashboard → **Metrics** → Orders/Inventory → meter `Marten` → `marten.event.append`, split by `event_type`. | A live histogram of the domain's event vocabulary. |

---

## Step 7 — Teardown

```powershell
Get-Process -Name CritterMart.* -ErrorAction SilentlyContinue | Stop-Process -Force
```

This stops the service hosts and cascades Aspire's DCP container cleanup (Postgres + RabbitMQ
containers stop). The dashboard's telemetry is in-memory — it is gone on teardown, so capture any
trace/metric screenshots **before** this step. Vite `node` workers may linger (see [Step 0](#step-0--clean-slate-do-this-before-a-talk)).

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `MSB3026` / locked DLL on build | Orphaned service host from a prior boot — run [Step 0](#step-0--clean-slate-do-this-before-a-talk). |
| `:5173` already in use | Orphaned Vite `node` workers — sweep `node` (carefully) or reboot. |
| Nothing boots / container errors | Docker Desktop isn't running. |
| Dashboard login fails | The `?t=` token is **per-boot** — copy the current one from the console/log, not an old URL. |
| Order stuck at `awaiting_confirmation` | Inventory didn't reply — check it's healthy and RabbitMQ is up; check the boot log for broker errors. |
| Empty catalog / "no stock" | The DB is ephemeral — re-run [Step 3](#step-3--seed-data-manual-re-run-every-boot) after every boot. |
| CritterWatch shows "Development" tier | The console must run as `Production` to read the trial license (set in the AppHost via `WithEnvironment("ASPNETCORE_ENVIRONMENT","Production")`). |

---

## curl quick reference

Portable equivalent of the happy path (bash/curl):

```bash
CAT=http://localhost:5101; INV=http://localhost:5102; ORD=http://localhost:5103
CUST=demo-buyer; SKU=crit-001
curl -s -X POST $CAT/products -H 'content-type: application/json' \
  -d '{"sku":"crit-001","name":"Cosmic Critter Plush","description":"A plush from the cosmos.","price":24.99}'
curl -s -X POST $INV/stock/$SKU/receipts -H 'content-type: application/json' -d '{"quantity":100}'
curl -s -X POST $ORD/carts/$CUST/items -H 'content-type: application/json' \
  -d '{"sku":"crit-001","quantity":2,"productSnapshot":{"name":"Cosmic Critter Plush","price":24.99}}'
ORDER=$(curl -s -X POST $ORD/orders -H 'content-type: application/json' -d '{"customerId":"demo-buyer"}')
echo "$ORDER"   # -> {"orderId":"..."}
# then GET $ORD/orders/<orderId> until status=confirmed; GET $ORD/orders/mine -H "X-Customer-Id: demo-buyer"
```

---

## Known gaps

- **No seed automation.** `src/CritterMart.Seeding` is an abandoned scaffold (no source, not in the
  solution or AppHost). Every boot needs the manual [Step 3](#step-3--seed-data-manual-re-run-every-boot) seed.
  A small seed step/console (or a `.http` file) is a natural follow-up.
- **Payment decline is a DEMO AFFORDANCE, on by default** via `Payment:DeclineOverAmount` (= $100,
  set in the AppHost) — orders over the threshold cancel with `payment_declined` (see
  [Step 5b](#5b--payment-declined-payment_declined--the-compensation-beat)). **Remove that AppHost line
  after the talk** to restore round-one "always approve."
- **Payment timeout still config-only** — fires after `Orders:PaymentTimeout` (default 10 min); demo-able
  only by shortening it, and the wait is dead air (prefer the decline beat live).
- **Last verified:** 2026-06-17 on `main` @ `edab271` — full happy path + insufficient-stock cancel,
  zero errors/DLQ, all four surfaces up. The payment-decline affordance is unit-tested; re-verify the
  full live decline→release once against the current commit before relying on it.
