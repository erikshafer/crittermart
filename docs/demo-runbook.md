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
> **Bottom line:** one `dotnet run` + one `POST /orders` proves the entire event-sourced,
> message-driven system — handlers, cross-BC RabbitMQ hops, projections, and the compensation path —
> is working. Aspire's Postgres is **ephemeral** (fresh empty DB every boot), so the demo data has to
> be loaded each boot — but the **`seeder` Aspire resource now does that automatically on boot**
> (see [§ Step 3](#step-3--seed-data-auto-on-boot-manual-fallback)), so the stack comes up demo-ready
> with no manual seeding. The manual seed remains documented as a fallback/override.
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
- Ports free: **5101, 5102, 5103** (services), **5273** (SPA — pinned off Vite's `5173` default so it
  coexists with sibling Vite apps like MmoReconnect/CritterBids), **15090** (Aspire dashboard). The
  CritterWatch console takes a **dynamic** port (see [§ URLs & ports](#urls--ports)).

---

## Step 0 — Clean slate (do this before a talk)

Orphaned processes from a previous boot lock build DLLs (`MSB3026` on the next build) and squat on
`:5273`. `Stop-Process` on the CritterMart services does **not** reap the Vite dev server's `node`
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
| **Storefront SPA** | `http://localhost:5273` | 5273 (fixed) | Vite dev server; the human-facing demo. **5273, not Vite's 5173 default** — avoids colliding with sibling Vite apps (`strictPort:true`). |
| **CritterWatch console** | via dashboard | **dynamic** | `WithExternalHttpEndpoints`, no pinned port — open it from the Aspire dashboard's `critterwatch-console` resource. |
| **RabbitMQ management** | via dashboard | dynamic | Open from the Aspire dashboard's `rabbitmq` resource. |

> **Finding the CritterWatch port from a script (AI agent fallback):**
> ```powershell
> $p = (Get-Process CritterMart.CritterWatch).Id
> Get-NetTCPConnection -OwningProcess $p -State Listen | ? LocalPort -gt 1024 | Select -Expand LocalPort -Unique
> ```

---

## Step 3 — Seed data (auto on boot; manual fallback)

**This usually happens automatically.** The Aspire stack includes a one-shot **`seeder`** resource
(`src/CritterMart.Seeding`) that, once Catalog + Inventory are healthy, POSTs the canonical demo seed —
three products + their stock — to the real `/products` and `/stock/{sku}/receipts` endpoints, then exits.
So after [Step 2](#step-2--wait-for-healthy) the DB is already populated and the SPA browse page lists
the products; **you normally do nothing here**. Watch the `seeder` resource in the Aspire dashboard (or
its console log) — it logs `[seed] published …` lines and finishes. The seed is **idempotent** (a
duplicate SKU returns 409 and is skipped), and the `seeder` is a leaf resource nothing waits on, so a
seed hiccup shows red on the dashboard but never blocks the services or storefront.

The canonical seed set (matches the three order routes below):

| SKU | Name | Price | Stock | Used by |
|---|---|---|---|---|
| `crit-001` | Cosmic Critter Plush | $24.99 | 1000 | [Step 4](#step-4--drive-an-order-happy-path) happy path (seeded high for sustained traffic) |
| `crit-rare` | Rare Critter | $49.99 | 3 | [Step 5a](#5a--insufficient-stock-stock_unavailable) insufficient-stock cancel (order **>3** to trigger) |
| `crit-deluxe` | Deluxe Critter | $24.99 | 1000 | [Step 5b](#5b--payment-declined-payment_declined--the-compensation-beat) payment-decline cancel (seeded high for sustained traffic) |

The seeder also defines the demo **coupon** set (Workshop 003 slice 6.1, via `POST /coupons`):

| Code | Discount | Cap | Used by |
|---|---|---|---|
| `FLASH20` | 20% off | **3** | [Step 5d](#step-5d--redeem-a-coupon-dcb-cap--the-race) the DCB cap + race (order **4** → `409 CouponExhausted`; cancel one → slot returns). Do **not** raise the cap. |
| `WELCOME10` | 10% off | 100000 | everyday discounted-order traffic (`demo-traffic.ps1 -CouponEvery`), a high cap so a sustained run never exhausts it |
| `FIRSTORDER` | 15% off | 100000, **1/customer** | [Step 5d](#step-5d--redeem-a-coupon-dcb-cap--the-race) the **second** DCB — the composite `(coupon × customer)` boundary (slice 6.5): a customer's second redemption → `409 CouponAlreadyRedeemedByCustomer`; another customer still succeeds |

**Disable auto-seed** (to seed by hand, or to demo an empty store): set `SEEDING_ENABLED=false` on the
`seeder` resource (e.g. add `.WithEnvironment("SEEDING_ENABLED","false")` in the AppHost, or export it),
and the seeder logs that it skipped and exits without seeding.

**Manual seed (fallback / extra data).** The commands below are what the seeder automates — run them
yourself when auto-seed is disabled, or to add SKUs beyond the canonical set. The order flow does not
strictly need the Catalog row (the storefront snapshots name+price into the cart), but seeding it makes
the SPA's browse page populate.

```powershell
$cat="http://localhost:5101"; $inv="http://localhost:5102"; $sku="crit-001"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }

# 1. Publish a product (Catalog — Marten document store)
Invoke-RestMethod "$cat/products" -Method Post -ContentType application/json `
  -Body (J @{ sku=$sku; name="Cosmic Critter Plush"; description="A plush from the cosmos."; price=24.99 })

# 2. Receive stock (Inventory — event-sourced). 1000 matches the seeder's high-stock SKUs.
Invoke-RestMethod "$inv/stock/$sku/receipts" -Method Post -ContentType application/json `
  -Body (J @{ quantity=1000 })

# Confirm: available=1000, reserved=0, committed=0
Invoke-RestMethod "$inv/stock/$sku"
```

**Request shapes** (the authoritative source is `src/**/Features/*.cs`; **[verify]** if a 400 appears):

| Endpoint | Method | Body |
|---|---|---|
| `/products` (Catalog) | POST | `{ sku, name, description, price }` |
| `/stock/{sku}/receipts` (Inventory) | POST | `{ quantity }` |
| `/register` (Identity) | POST | `{ email, displayName, password }` — mints the shopper the next row logs in as |
| `/login` (Identity) | POST | `{ email, password }` — returns `{ token, customerId }`; the token rides every Orders call |
| `/carts/mine/items` (Orders) | POST | `{ sku, quantity, productSnapshot: { name, price } }` + header `Authorization: Bearer <token>` |
| `/orders` (Orders) | POST | *(no body)* — identity from the token's `sub` claim; returns `{ orderId }`. Missing/invalid token → **401**. |

---

## Step 4 — Drive an order (happy path)

> ### ⚙️ The three demo knobs (set in the AppHost on the `orders` resource — `src/CritterMart.AppHost/Program.cs`)
>
> The AppHost injects **three** environment knobs that shape the demo's timing and the decline beat.
> They are the source of truth — trust them over any number in prose:
>
> | Knob | Demo value | Default (unset) | Effect |
> |---|---|---|---|
> | `Payment__DeclineOverAmount` | **`200`** | always approve | the stub declines any order whose **total > $200** (the [Step 5b](#5b--payment-declined-payment_declined--the-compensation-beat) decline beat) |
> | `Payment__AuthDelay` | **`00:03:00`** | `TimeSpan.Zero` (instant) | artificial pause inside the stub before it returns a decision, so `stock_reserved → payment_authorized → confirmed` is visible at speaking pace |
> | `Orders__PaymentTimeout` | **`00:07:00`** | 10 min | how long an order may sit non-terminal before the scheduled `OrderPaymentTimeout` cancels it |
>
> **Timing consequence:** with `AuthDelay = 3 min`, a placed order sits in **`stock_reserved` for ~3 minutes**
> before it authorizes and confirms — it is **not** "< 1s". Because `3 min < 7 min`, it always confirms before
> the timeout fires. On stage, **screenshot the in-flight `stock_reserved` state** (and the live Scheduled
> Messages / Durability backlog it creates) rather than waiting out the 3 minutes — that backlog is the most
> on-theme CritterWatch visual. `demo-traffic.ps1` is fire-and-forget (no polling) precisely because of this delay.
>
> **A fourth knob lives on a different resource:** `Inventory__ReplenishTimeout` (on the **`inventory`**
> resource, not `orders`) drives the Replenishment saga's escalate clock — it is documented with the saga in
> [§ 5c](#step-5c--backorder--replenishment-the-inventory-saga).

```powershell
$ord="http://localhost:5103"; $idn="http://localhost:5105"; $sku="crit-001"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }

# Mint a shopper (ADR 023 hard cutover: Authorization: Bearer is the ONLY identity transport — the
# X-Customer-Id header is retired). Register a throwaway account, log in, capture the JWT.
$em="demo-buyer-$(Get-Random)@demo.crittermart.local"; $pw="DemoPass1!"
Invoke-RestMethod "$idn/register" -Method Post -ContentType application/json -Body (J @{ email=$em; displayName="Demo Buyer"; password=$pw }) | Out-Null
$auth=@{ Authorization="Bearer $((Invoke-RestMethod "$idn/login" -Method Post -ContentType application/json -Body (J @{ email=$em; password=$pw })).token)" }

# Add to cart (productSnapshot is the cart's only product truth — the cart never reads Catalog)
Invoke-RestMethod "$ord/carts/mine/items" -Method Post -ContentType application/json -Headers $auth `
  -Body (J @{ sku=$sku; quantity=2; productSnapshot=@{ name="Cosmic Critter Plush"; price=24.99 } })

# Place the order — identity is the token's `sub` claim; there is NO request body (missing/invalid token → 401).
# This ONE call cascades the whole cross-BC saga.
$orderId = (Invoke-RestMethod "$ord/orders" -Method Post -Headers $auth).orderId
"orderId = $orderId"

# Poll to terminal status. The saga runs async over RabbitMQ, BUT the demo Payment__AuthDelay=3min holds the
# order in stock_reserved for ~3 min before it authorizes → confirmed, so the poll budget MUST exceed 3 min
# (50 × 5s = ~4.2 min, comfortably under the 7-min timeout). For a talk, prefer fire-and-forget (skip this loop).
$o=$null; for ($i=0; $i -lt 50; $i++){ Start-Sleep -Seconds 5; $o=Invoke-RestMethod "$ord/orders/$orderId"; if ($o.status -in 'confirmed','cancelled'){break} }
"status = $($o.status)"

# The 'My Orders' list (token-keyed read, GET /orders/mine)
Invoke-RestMethod "$ord/orders/mine" -Headers $auth
```

**Expected:** `status = confirmed` (after the ~3-min `AuthDelay` window); the order walked
`awaiting_confirmation → stock_reserved → payment_authorized → confirmed`. Stock moves
**available 1000 → 998, reserved → 0, committed 0 → 2**
(`Invoke-RestMethod "$inv/stock/$sku"`). No service called another directly — Orders cascaded
`ReserveStock`/`CommitStock` messages over RabbitMQ and reacted to the replies. That stock movement
is the proof the message round-trip completed.

---

## Step 5 — Drive the cancel saga (two live routes)

Two of the three cancel routes are triggerable live. Both are the *same handler machinery* as the
happy path, just reacting to a failure event — no special error path.

### 5a — Insufficient stock (`stock_unavailable`)

Order **more than the available stock**; the reservation refuses. `crit-rare` is auto-seeded at **3 units**
([Step 3](#step-3--seed-data-auto-on-boot-manual-fallback)), so ordering **4** overshoots it. (Rely on the
auto-seed — do **not** manually `POST` extra `crit-rare` stock here, or you raise the pool above 4 and the
order succeeds. If you disabled auto-seed, run [Step 3](#step-3--seed-data-auto-on-boot-manual-fallback)'s
manual seed first, seeding `crit-rare` at 3.)

```powershell
$ord="http://localhost:5103"; $idn="http://localhost:5105"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }
$sku="crit-rare"

# Mint a shopper (Bearer-only identity — see Step 4).
$em="demo-fail-$(Get-Random)@demo.crittermart.local"; $pw="DemoPass1!"
Invoke-RestMethod "$idn/register" -Method Post -ContentType application/json -Body (J @{ email=$em; displayName="Demo Fail"; password=$pw }) | Out-Null
$auth=@{ Authorization="Bearer $((Invoke-RestMethod "$idn/login" -Method Post -ContentType application/json -Body (J @{ email=$em; password=$pw })).token)" }

# Order 4 of the 3 seeded crit-rare units → the reservation refuses the whole order.
Invoke-RestMethod "$ord/carts/mine/items" -Method Post -ContentType application/json -Headers $auth -Body (J @{ sku=$sku; quantity=4; productSnapshot=@{ name="Rare Critter"; price=49.99 } })
$id = (Invoke-RestMethod "$ord/orders" -Method Post -Headers $auth).orderId
# This path cancels FAST: the reservation refuses before any payment step, so Payment__AuthDelay never applies.
$o=$null; for ($i=0;$i -lt 25;$i++){ Start-Sleep -Milliseconds 800; $o=Invoke-RestMethod "$ord/orders/$id"; if ($o.status -in 'confirmed','cancelled'){break} }
"status=$($o.status) reason=$($o.cancelReason)"
```

**Expected:** `status=cancelled reason=stock_unavailable`. Stock is **unchanged** — reservation is
all-or-nothing, so a refusal reserved nothing → **no** compensating release.

### 5b — Payment declined (`payment_declined`) — the compensation beat

This is the richer route: stock **is** reserved, then payment declines, so the order cancels **and the
reserved stock is released back** (a compensating `ReleaseStock` to Inventory). It is enabled by the
**`Payment:DeclineOverAmount`** demo affordance (**$200**, set by the AppHost — see the box below): order
total **over $200** → the stub declines. `crit-deluxe` is auto-seeded at 1000 units @ $24.99, so **9 units =
$224.91** clears the threshold (8 = $199.92 would *not*).

```powershell
$ord="http://localhost:5103"; $idn="http://localhost:5105"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }
$sku="crit-deluxe"

# Mint a shopper (Bearer-only identity — see Step 4).
$em="demo-decline-$(Get-Random)@demo.crittermart.local"; $pw="DemoPass1!"
Invoke-RestMethod "$idn/register" -Method Post -ContentType application/json -Body (J @{ email=$em; displayName="Demo Decline"; password=$pw }) | Out-Null
$auth=@{ Authorization="Bearer $((Invoke-RestMethod "$idn/login" -Method Post -ContentType application/json -Body (J @{ email=$em; password=$pw })).token)" }

# 9 × $24.99 = $224.91, over the $200 threshold → payment will decline (relies on the auto-seeded crit-deluxe).
Invoke-RestMethod "$ord/carts/mine/items" -Method Post -ContentType application/json -Headers $auth -Body (J @{ sku=$sku; quantity=9; productSnapshot=@{ name="Deluxe Critter"; price=24.99 } })
$id = (Invoke-RestMethod "$ord/orders" -Method Post -Headers $auth).orderId
# This path settles AFTER Payment__AuthDelay=3min (the stub waits, then declines), so budget the poll past 3 min.
$o=$null; for ($i=0;$i -lt 50;$i++){ Start-Sleep -Seconds 5; $o=Invoke-RestMethod "$ord/orders/$id"; if ($o.status -in 'confirmed','cancelled'){break} }
"status=$($o.status) reason=$($o.cancelReason)"
"stock after: $(Invoke-RestMethod "http://localhost:5102/stock/$sku" | ConvertTo-Json -Compress)"
```

**Expected:** `status=cancelled reason=payment_declined` (after the ~3-min `AuthDelay`), and stock returns to
**available 1000, reserved 0** — it was reserved at the stock gate, then **released back** when payment failed.
That release (`reserved 9 → 0`) is the compensation the audience sees flow back to Inventory in the trace /
CritterWatch. Contrast with 5a, where nothing was reserved so nothing came back.

> ### ⚙️ The `Payment:DeclineOverAmount` demo affordance — read this so it's never a surprise
>
> (One of the [three demo knobs](#step-4--drive-an-order-happy-path); this box is the decline-specific detail.)
>
> By default the stubbed payment provider **always approves** (round-one behavior). To make the
> decline route demo-able live, the **AppHost** (`src/CritterMart.AppHost/Program.cs`) injects
> `Payment__DeclineOverAmount = 200` into the Orders service, so the stub declines any order whose
> **total exceeds $200**. This is the *only* thing that makes a decline happen at runtime — the whole
> decline→cancel→release chain is real and was built/tested as slice 4.6.
>
> - **It is ON by default when you run via Aspire.** Orders over $200 **will** cancel with
>   `payment_declined` — that is expected, not a bug.
> - **Change it:** edit the `WithEnvironment("Payment__DeclineOverAmount", "200")` value in the AppHost.
> - **Turn it OFF (restore "always approve"):** delete that one line in the AppHost, or set
>   `Payment:DeclineOverAmount` to empty. **Do this after the talk** if you want production-faithful
>   behavior back.
> - **Timing:** the decision is **not instant** — the `Payment__AuthDelay=00:03:00` knob makes the stub
>   wait ~3 min before it declines, so the order sits in `stock_reserved` for that window first.
> - Code: `PaymentDeclinePolicy` + `StubPaymentProvider` in
>   `src/CritterMart.Orders/Ordering/PaymentProvider.cs`; registered in `src/CritterMart.Orders/Program.cs`.
>
> **The third cancel route — payment timeout (`payment_timeout`) — is still config-only and not
> talk-friendly:** it fires only after `Orders:PaymentTimeout` elapses (the AppHost sets this to **7 min**;
> the unset default is 10 min). Because `AuthDelay 3min < 7min`, normal orders authorize or decline before
> the timeout fires, so this route effectively never triggers in the demo. Demo-able by shortening the
> timeout *below* the auth delay, but the wait is dead air; prefer the decline beat (5b) on stage — it
> settles after the 3-min `AuthDelay` rather than the full timeout.

---

## Step 5c — Backorder & replenishment (the Inventory saga)

The **same refused reservation as [5a](#5a--insufficient-stock-stock_unavailable)**, seen from the
**Inventory** side. When an order's quantity exceeds a SKU's available stock, `ReserveStockHandler` refuses
the order (`stock_unavailable`, exactly as 5a) **and additionally** emits a `BackorderDetected`, which opens a
**`Replenishment` saga** for that SKU (CritterMart's first `Wolverine.Saga`, slices 2.5–2.7). One customer
action, two reactions in two bounded contexts — the order cancels in **Orders**, a backorder opens in
**Inventory**.

> 5a's `crit-rare × 4` already opens a saga (outstanding **1**) — the saga shipped after this runbook's prior
> revision. This section orders **more** so the outstanding is visible and a partial-then-cover is demoable.

The saga has three beats:

- **open** — `BackorderDetected` with no open saga → records `Outstanding`, fires `RequestRestock` (the
  supplier-notification stub — logs *“Supplier notified: restock …”*), and schedules a `ReplenishTimeout`.
- **resolve** — a stock receipt (`POST /stock/{sku}/receipts`) publishes `RestockArrived`. A receipt that
  **covers** `Outstanding` completes the saga (its doc is deleted); a **partial** receipt reduces `Outstanding`
  and the saga stays open (no fresh `RequestRestock`).
- **escalate** — if no covering receipt arrives before the deadline, `ReplenishTimeout` fires →
  `ReplenishmentEscalated` (logs *“Operator alert: SKU … went unreplenished”* at **Warning**) and the saga
  completes.

> ### ⚙️ The fourth demo knob — `Inventory__ReplenishTimeout` (on the `inventory` resource)
>
> The AppHost injects `Inventory__ReplenishTimeout = 00:00:25` so the **escalate** beat fires in ~25s instead
> of the unset **2-minute** default (`ReplenishDeadline.Default`). The service already binds
> `Inventory:ReplenishTimeout` (`src/CritterMart.Inventory/Program.cs`) via the `__`→`:` convention; the
> AppHost just supplies the demo value. **Delete that one AppHost line after the talk** to restore the
> production-faithful 2-minute default. (It is the saga sibling of the [three order-timing
> knobs](#step-4--drive-an-order-happy-path), but lives on `inventory`, not `orders`.)

**Drive it with the script (easiest):**

```powershell
# open the saga (orders crit-rare × 10 over the seeded 3 → outstanding 7), then print the follow-ups:
pwsh docs/demo-traffic.ps1 -Backorder

# open AND resolve in one command (fires the covering receipt):
pwsh docs/demo-traffic.ps1 -Backorder -Cover
```

**Or drive each beat by hand:**

```powershell
$ord="http://localhost:5103"; $inv="http://localhost:5102"; $idn="http://localhost:5105"; $sku="crit-rare"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }

# Mint a shopper (Bearer-only identity — see Step 4).
$em="demo-backorder-$(Get-Random)@demo.crittermart.local"; $pw="DemoPass1!"
Invoke-RestMethod "$idn/register" -Method Post -ContentType application/json -Body (J @{ email=$em; displayName="Demo Backorder"; password=$pw }) | Out-Null
$auth=@{ Authorization="Bearer $((Invoke-RestMethod "$idn/login" -Method Post -ContentType application/json -Body (J @{ email=$em; password=$pw })).token)" }

# open: order 10 of the 3 seeded crit-rare → refuses (stock_unavailable) AND opens the saga (outstanding 7)
Invoke-RestMethod "$ord/carts/mine/items" -Method Post -ContentType application/json -Headers $auth -Body (J @{ sku=$sku; quantity=10; productSnapshot=@{ name="Rare Critter"; price=49.99 } })
$id = (Invoke-RestMethod "$ord/orders" -Method Post -Headers $auth).orderId
$o=$null; for ($i=0;$i -lt 25;$i++){ Start-Sleep -Milliseconds 800; $o=Invoke-RestMethod "$ord/orders/$id"; if ($o.status -in 'confirmed','cancelled'){break} }
"order: status=$($o.status) reason=$($o.cancelReason)"   # → cancelled / stock_unavailable

# resolve: receive the outstanding (7) → RestockArrived covers it → saga completes (doc deleted)
Invoke-RestMethod "$inv/stock/$sku/receipts" -Method Post -ContentType application/json -Body (J @{ quantity=7 })
#   partial instead: receive < 7 → outstanding reduces, saga stays open
#   escalate instead: send NO receipt → after ~25s ReplenishTimeout fires → ReplenishmentEscalated
```

**Expected:** the order is `cancelled · stock_unavailable` (stock untouched — a refusal reserves nothing, as
in 5a), and a `Replenishment` saga opens for `crit-rare`. The covering receipt drives it to completion; with
no receipt it escalates after ~25s. The receipt path also raises `crit-rare`'s available stock, so re-running
needs a fresh boot (reseed) or a larger `-BackorderQuantity`.

**What to watch (the saga's surfaces):**

| Surface | What shows |
|---|---|
| **Inventory logs** | `Supplier notified: restock crit-rare x7` (Information) on open; `Operator alert: SKU crit-rare went unreplenished, N still short` (Warning) on escalate. |
| **CritterWatch — Messaging Explorer** | the `RequestRestock` / `RestockArrived` / `ReplenishmentEscalated` bus messages, plus the scheduled `ReplenishTimeout` in the Durability/Scheduled backlog (like `OrderPaymentTimeout` in [Step 6](#step-6--verify-the-demo-surfaces)). Saga *instances* are not yet surfaced in beta.1 — the Explore → Workflow page is a pre-1.0 stub (observed message flow only, no structural discovery); see [`critterwatch-saga-visibility-beta1.md`](research/critterwatch-saga-visibility-beta1.md). |
| **Marten** | the saga doc in `inventory.mt_doc_replenishment` (`Id` = SKU, `Outstanding`) while open; **deleted** on completion. |

---

## Step 5d — Redeem a coupon (DCB cap + the race)

CritterMart's **first Dynamic Consistency Boundary** (ADR 024, Workshop 003 slices 6.1/6.3/6.4): a coupon is
redeemable at most **N times, ever, across all orders** — an invariant no single order stream could enforce.
The seeded **`FLASH20`** (20% off, **cap 3**) is the flash-sale coupon; driving it to its cap by hand is the
demo. The coupon rides checkout as an optional `?couponCode=` on `POST /orders`; an undiscounted order (no
code) is [Step 4](#step-4--drive-an-order-happy-path) unchanged.

```powershell
$ord = "http://localhost:5103"; $idp = "http://localhost:5105"
function J($o){ $o | ConvertTo-Json -Compress -Depth 6 }
# Place one FLASH20 order per fresh shopper; returns the HTTP status (201 = redeemed, 409 = refused).
function Place-Coupon($code) {
  $email = "flash-$([guid]::NewGuid().ToString('N').Substring(0,8))@crittermart.com"
  Invoke-RestMethod "$idp/register" -Method Post -ContentType application/json -Body (J @{ email=$email; password='Demo!pass1'; displayName='Flash Shopper' }) | Out-Null
  $auth = @{ Authorization = "Bearer $((Invoke-RestMethod "$idp/login" -Method Post -ContentType application/json -Body (J @{ email=$email; password='Demo!pass1' })).token)" }
  Invoke-RestMethod "$ord/carts/mine/items" -Method Post -ContentType application/json -Headers $auth -Body (J @{ sku='crit-001'; quantity=1; productSnapshot=@{ name='Cosmic Critter Plush'; price=24.99 } }) | Out-Null
  try { $r = Invoke-WebRequest "$ord/orders?couponCode=$code" -Method Post -Headers $auth; "  $($r.StatusCode)  redeemed  order $(( $r.Content | ConvertFrom-Json).orderId.Substring(0,8))" }
  catch { "  $([int]$_.Exception.Response.StatusCode)  refused   $code exhausted" }
}
1..3 | ForEach-Object { Place-Coupon 'FLASH20' }   # three succeed → the cap is now full
Place-Coupon 'FLASH20'                             # the fourth → 409 CouponExhausted
```

**Expected:** the first **3** print `201 redeemed`; the **4th** prints `409 refused` — the cap held. Each
redeemed order's `GET /orders/{id}` shows `subtotal 24.99`, `discount 5.00`, `total 19.99`, `couponCode FLASH20`.

**The slot returns on cancellation (slice 6.4).** Cancel a redeemed order and its slot goes back to the pool —
a failed sale never permanently burns a flash-sale slot. Easiest live route: place a FLASH20 order whose total
clears the `$200` decline threshold (e.g. `quantity=9` at `price=24.99` → `$224.91` before discount), let it
decline (~3-min `AuthDelay`), and its `CouponRedemptionReleased` frees the slot — a **5th** FLASH20 order then
succeeds where the 4th was refused. (Any cancel route works: stock-unavailable via `crit-rare`, or timeout.)

**The race (the talk's money moment).** Two shoppers reaching for the *same last slot* both see room, both
redeem, and exactly **one** wins — the loser's commit hits `DcbConcurrencyException`, retries into the now-full
cap, and gets `409`. To force it, drive `FLASH20` to cap-1 (two orders), then fire two `Place-Coupon 'FLASH20'`
in parallel (`Start-Job`/`Start-ThreadJob`): exactly one returns `201`, the other `409`. The integration test
`CouponTests.concurrent_redemptions_never_exceed_the_cap` pins this (6 racers, cap 3 → exactly 3 survive).

**One redemption per customer — the SECOND DCB (slice 6.5, ADR 024 §38).** Where `FLASH20` counts redemptions
against a *coupon*, **`FIRSTORDER`** (15% off, `1/customer`) counts against a `(coupon × customer)` **pair** — a
composite tag, CritterMart's first. The invariant: a given customer may redeem it **at most once, ever**, even
across separate orders. The `Place-Coupon` helper above registers a *fresh* shopper each call, so to show the
per-customer refusal you must reuse **one** identity across two checkouts:

```powershell
# Reuse ONE shopper across two FIRSTORDER checkouts to show the per-customer boundary bite.
$email = "firstorder-$([guid]::NewGuid().ToString('N').Substring(0,8))@crittermart.com"
Invoke-RestMethod "$idp/register" -Method Post -ContentType application/json -Body (J @{ email=$email; password='Demo!pass1'; displayName='Repeat Shopper' }) | Out-Null
$auth = @{ Authorization = "Bearer $((Invoke-RestMethod "$idp/login" -Method Post -ContentType application/json -Body (J @{ email=$email; password='Demo!pass1' })).token)" }
function Redeem-FirstOrder {
  Invoke-RestMethod "$ord/carts/mine/items" -Method Post -ContentType application/json -Headers $auth -Body (J @{ sku='crit-001'; quantity=1; productSnapshot=@{ name='Cosmic Critter Plush'; price=24.99 } }) | Out-Null
  try { $r = Invoke-WebRequest "$ord/orders?couponCode=FIRSTORDER" -Method Post -Headers $auth; "  $($r.StatusCode)  redeemed" }
  catch { "  $([int]$_.Exception.Response.StatusCode)  refused   $((($_.ErrorDetails.Message | ConvertFrom-Json).title))" }
}
Redeem-FirstOrder   # 201 redeemed — first time
Redeem-FirstOrder   # 409 refused   CouponAlreadyRedeemedByCustomer — same customer, second attempt
Place-Coupon 'FIRSTORDER'   # 201 — a DIFFERENT (fresh) customer still succeeds; their pair is independent
```

**Expected:** the same shopper's **second** `FIRSTORDER` checkout returns `409 CouponAlreadyRedeemedByCustomer`
and creates no order; a different customer still gets `201`. Cancel the first order (any route) and its release
carries the composite tag too, so **that** customer may redeem `FIRSTORDER` again — the reserve/release symmetry,
now on the per-customer boundary. Pinned by `CouponTests.a_per_customer_coupon_admits_a_customer_once_then_rejects_a_second_redemption`
and `…_lets_the_customer_redeem_again`.

**What to watch:**

| Surface | What shows |
|---|---|
| **Marten** | the coupon definitions in `orders.mt_doc_couponview` (`FLASH20` → cap 3; `FIRSTORDER` → `oneRedemptionPerCustomer: true`); the tagged `CouponRedeemed` / `CouponRedemptionReleased` events on **order** streams in `orders.mt_events`; the DCB tag tables `orders.mt_event_tag_coupon` (the `CouponId`) and, for a `FIRSTORDER` redemption, `orders.mt_event_tag_couponcustomer` (the composite `(coupon × customer)` value); the advisory net count in `orders.mt_doc_couponusageview`. |
| **CritterWatch — event append metric** | `CouponDefined` / `CouponRedeemed` / `CouponRedemptionReleased` in the `marten.event.append` counter (tagged by event type). |
| **The order view** | `GET /orders/{id}` on a redeemed order returns the `subtotal` / `discount` / `couponCode` breakdown; the 4th (refused) order was never created. |

> ### ⚙️ The `FLASH20` cap is a demo knob
>
> `FLASH20`'s **cap 3** is deliberately tiny so the breach + race are hand-demonstrable. The seeder
> (`src/CritterMart.Seeding/Program.cs`) defines it; **do not raise the cap** or the breach stops being
> reachable by hand. `WELCOME10` (cap 100000) is the everyday-discount counterpart for sustained traffic
> (`demo-traffic.ps1 -CouponEvery 3`); `FIRSTORDER` (cap 100000, `1/customer`) is the second-DCB
> counterpart — its high global cap keeps the *composite* boundary the only one that bites. Unlike the
> payment/timeout knobs, the coupon set is real domain data (configuration-as-events), not a runtime toggle —
> it survives as `CouponDefined` events, so a reseed re-establishes it and a re-run is idempotent (duplicate
> code → 409 → skip).

---

## Step 6 — Verify the demo surfaces

| Surface | How to check | What it shows / talk beat |
|---|---|---|
| **Swagger UI** | Open `http://localhost:5101`, `:5102`, `:5103` (root redirects to `/swagger`). Script: each `/swagger/index.html` returns **200**. | The Wolverine.Http endpoints with OpenAPI **inferred from handler signatures** — the "no controllers, no boilerplate" beat. |
| **Aspire dashboard** | `http://localhost:15090/login?t=<token>` → **Traces** → the `POST /orders` row → span waterfall. | The cross-service trace: Orders → RabbitMQ → Inventory → back, with `marten.connection` spans. **Deep-dive + screenshot guide: [otel-trace-walkthrough.md](research/otel-trace-walkthrough.md).** |
| **CritterWatch console** | Aspire dashboard → `critterwatch-console` resource → open its endpoint. | The Wolverine/Marten monitoring view — messages, handlers, queues, sagas, per-service health. The most on-theme "it's actually working" visual for a messaging talk. **[verify]** it shows the **Trial** tier, not "Development" (it runs with `ASPNETCORE_ENVIRONMENT=Production` so it reads the real license — set in the AppHost). |
| **Storefront SPA** | `http://localhost:5273` | Browse → add to cart → checkout → track → **My Orders**. The human-facing payoff. |
| **Metrics** | Aspire dashboard → **Metrics** → Orders/Inventory → meter `Marten` → `marten.event.append`, split by `event_type`. | A live histogram of the domain's event vocabulary. |

> **⏱️ A `POST /orders` trace's waterfall stays tight (~tens of milliseconds) — screenshot it anytime.**
> Every placed order schedules a durable `OrderPaymentTimeout` self-message
> `DelayedFor(Orders:PaymentTimeout)` (the AppHost sets **7 min** in the demo; the unset default is 10 min —
> `src/CritterMart.Orders/Features/PlaceOrder.cs`). Wolverine stamps the placement request's trace context
> onto that delayed envelope, so *by default* the fired timeout's span would parent back **into** the
> placement trace and balloon its **Duration** to the whole timeout window. The `PaymentTimeoutHandler` (and its cart sibling `CartAbandonmentHandler`) now
> prevent that: `[WolverineLogging(telemetryEnabled: false)]` suppresses Wolverine's parented span and the
> handler opens its own **span-linked root trace** instead
> (`src/CritterMart.Orders/Observability/TemporalAutomationTracing.cs`). So the placement waterfall reads
> the clean ~50 ms Orders→Inventory cascade no matter when you open it, and the fired deadline shows up as
> its **own** `order.payment.timeout` (or `cart.activity.timeout`) trace, a span *link* away from the
> request that armed it — the OpenTelemetry idiom for deferred follow-up work. (Realized in the
> deferred-timeout linked-traces change; see `docs/retrospectives/implementations/031-*`.)

---

## Live-traffic flourish — `demo-traffic.ps1`

To paint **continuous live message flow** onto CritterWatch (Topology / Services) and the OTel dashboard —
e.g. while you talk through the closing slides — run the traffic generator. It places orders against the
real `/orders` endpoints in a loop (there is **no special "generate traffic" endpoint** in the app; every
order runs the genuine cross-BC saga), so the monitoring surfaces light up with real traffic:

```powershell
# one burst (12 orders, a payment-decline every 5th):
pwsh docs/demo-traffic.ps1

# stream until Ctrl+C — the "set it running during the demo" mode:
pwsh docs/demo-traffic.ps1 -Continuous

# denser, happy-path only:
pwsh docs/demo-traffic.ps1 -Count 30 -DelaySeconds 0.8 -DeclineEvery 0

# drive the Replenishment saga instead of happy traffic (orders crit-rare over stock → backorder opens):
pwsh docs/demo-traffic.ps1 -Backorder          # open the saga + print resolve/partial/escalate follow-ups
pwsh docs/demo-traffic.ps1 -Backorder -Cover   # open AND fire the covering receipt — open→resolve in one go
```

> **`-Backorder` is a focused saga lever, not traffic.** It ignores `-Count`/`-Continuous`/`-Decline*`, orders
> a low-stock SKU (`crit-rare`) above its stock to open the Replenishment saga, and (with `-Cover`) resolves
> it — the full beat-by-beat walkthrough is [§ 5c](#step-5c--backorder--replenishment-the-inventory-saga).

Most orders are happy-path confirms (each consumes 1 unit of `crit-001`/`crit-deluxe`); every Nth
(`-DeclineEvery`, default 5) snapshots **one unit at `-DeclinePrice` ($250, above the $200 threshold)** so the
order total trips the decline affordance, exercising the **compensating stock-release** branch too. Sizing
the decline by price (not quantity) keeps it to one reserved unit. Declines are stock-neutral (they release
what they reserved), and the boot reseeds stock — so a fresh boot restores full stock. The script
fires-and-forgets (no polling), so sagas run async in the background and the flow stays visible.

> **ℹ️ Sustained-run stock note:** only happy orders consume stock (1 unit each, held for the full ~3-min
> `Payment__AuthDelay` before committing), and the rotation uses `crit-001` + `crit-deluxe`, both **seeded
> high at 1000 (= 2000 units)** precisely so a long run keeps painting traffic without draining the pool —
> that buys roughly tens of minutes of continuous default-rate (`-DelaySeconds 1.5`) traffic. It is still
> **finite**: a very long or very dense (`-Continuous -DelaySeconds 0.2`) run can eventually exhaust it, after
> which the script silently flips to `stock_unavailable` cancels. For a marathon session, reboot to reseed or
> raise `-DelaySeconds`. (The high seed quantity is the durable mitigation for this — see
> `src/CritterMart.Seeding/Program.cs`.)

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
| `:5273` already in use | Orphaned Vite `node` workers — sweep `node` (carefully) or reboot. (A *sibling* Vite app on `5273` would be unusual — CritterMart deliberately moved here off the shared `5173`.) |
| Nothing boots / container errors | Docker Desktop isn't running. |
| Dashboard login fails | The `?t=` token is **per-boot** — copy the current one from the console/log, not an old URL. |
| Order stuck at `awaiting_confirmation` | Inventory didn't reply — check it's healthy and RabbitMQ is up; check the boot log for broker errors. |
| Empty catalog / "no stock" | The `seeder` resource auto-populates on boot — check it finished OK in the Aspire dashboard (`[seed]` log lines). If auto-seed is off (`SEEDING_ENABLED=false`) or you need extra data, run the manual [Step 3](#step-3--seed-data-auto-on-boot-manual-fallback). |
| CritterWatch shows "Development" tier | The console must run as `Production` to read the trial license (set in the AppHost via `WithEnvironment("ASPNETCORE_ENVIRONMENT","Production")`). |

---

## curl quick reference

Portable equivalent of the happy path (bash/curl):

```bash
CAT=http://localhost:5101; INV=http://localhost:5102; ORD=http://localhost:5103; IDN=http://localhost:5105
SKU=crit-001
curl -s -X POST $CAT/products -H 'content-type: application/json' \
  -d '{"sku":"crit-001","name":"Cosmic Critter Plush","description":"A plush from the cosmos.","price":24.99}'
curl -s -X POST $INV/stock/$SKU/receipts -H 'content-type: application/json' -d '{"quantity":1000}'
# Mint a shopper (Bearer-only identity — ADR 023): register a throwaway account, log in, capture the JWT.
EM="demo-buyer-$RANDOM@demo.crittermart.local"; PW='DemoPass1!'
curl -s -X POST $IDN/register -H 'content-type: application/json' \
  -d "{\"email\":\"$EM\",\"displayName\":\"Demo Buyer\",\"password\":\"$PW\"}" > /dev/null
TOKEN=$(curl -s -X POST $IDN/login -H 'content-type: application/json' \
  -d "{\"email\":\"$EM\",\"password\":\"$PW\"}" | sed -E 's/.*"token":"([^"]+)".*/\1/')
curl -s -X POST $ORD/carts/mine/items -H 'content-type: application/json' -H "Authorization: Bearer $TOKEN" \
  -d '{"sku":"crit-001","quantity":2,"productSnapshot":{"name":"Cosmic Critter Plush","price":24.99}}'
# /orders takes NO body — identity is the token's `sub` claim (a missing/invalid token is a 401).
ORDER=$(curl -s -X POST $ORD/orders -H "Authorization: Bearer $TOKEN")
echo "$ORDER"   # -> {"orderId":"..."}
# then GET $ORD/orders/<orderId> until status=confirmed (~3 min under the demo Payment__AuthDelay); GET $ORD/orders/mine -H "Authorization: Bearer $TOKEN"
```

---

## Known gaps

- **Seed automation — DONE.** `src/CritterMart.Seeding` is a one-shot console wired as the `seeder`
  Aspire resource; it auto-seeds the canonical set on boot (see
  [Step 3](#step-3--seed-data-auto-on-boot-manual-fallback)). Set `SEEDING_ENABLED=false` to disable and
  seed by hand. (Seeds products + stock only; carts/orders are still driven live in the demo.)
- **Payment decline is a DEMO AFFORDANCE, on by default** via `Payment:DeclineOverAmount` (= **$200**,
  set in the AppHost) — orders over the threshold cancel with `payment_declined` (see
  [Step 5b](#5b--payment-declined-payment_declined--the-compensation-beat)). **Remove that AppHost line
  after the talk** to restore round-one "always approve."
- **Payment timeout still config-only** — fires after `Orders:PaymentTimeout` (the AppHost sets **7 min**;
  unset default 10 min). Because `Payment__AuthDelay=3min` settles orders first, this route effectively never
  triggers in the demo; demo-able only by shortening the timeout *below* the auth delay, and the wait is dead
  air (prefer the decline beat live).
- **Replenishment-saga escalate is a DEMO KNOB** — `Inventory__ReplenishTimeout` (= **25s**, set in the
  AppHost on the **`inventory`** resource) shortens the saga's escalate deadline from the unset 2-min default
  so the escalate beat is demoable at speaking pace. **Remove that AppHost line after the talk** to restore
  the production-faithful 2-min default. The backorder/replenishment route itself is [§ 5c](#step-5c--backorder--replenishment-the-inventory-saga).
- **Last verified (Bearer-only identity):** 2026-07-14 on the hard-cutover branch — full live pass on the
  Aspire stack after retiring the `X-Customer-Id` fallback. The migrated `demo-traffic.ps1` registered a
  throwaway shopper, logged in, and placed 3 Bearer-authenticated orders; a hand-driven register → login →
  add-to-cart → change-quantity → checkout journey confirmed end-to-end (`status=confirmed` after the 3-min
  `AuthDelay`, stock `available 1000→995 / committed 5`, `customerName` enriched from `LocalCustomerView`).
  Every fallback probe was rejected: header-only add/view/place/list, token-less, and garbage-token calls all
  `401`; the anonymous automation reads (`/orders/awaiting-payment`) still serve. SPA served 200 (auth flow
  untouched this pass). CritterWatch console not exercised (trial expired 2026-07-10).
- **Last verified (saga demo affordances):** 2026-06-30 on `chore/saga-demo-affordances` — the [§ 5c](#step-5c--backorder--replenishment-the-inventory-saga)
  Replenishment-saga route driven live on the full Aspire stack (auto-seeded `crit-rare` = 3): **open** (saga
  doc present in `inventory.mt_doc_replenishment` with `Outstanding = 7`), **escalate** (after the
  `Inventory__ReplenishTimeout = 25s` knob the doc was deleted — saga completed — with stock untouched at 3),
  and **resolve** via `demo-traffic.ps1 -Backorder -Cover` (the covering receipt completed the saga in ~3s,
  far inside the 25s window; `crit-rare` climbed 3 → 10). The orphaned post-resolve `ReplenishTimeout` fired
  into the saga's `NotFound` no-op with all three services still healthy (no crash, no zombie saga), and the
  `-Backorder` guard correctly refused a non-shortfalling order. (Order/cancel routes + SPA not re-run this
  session — they stand at the full pass below.)
- **Last verified (full pass):** 2026-06-17 on `192d2f0` (`main`) — **full live pass**. Clean boot, auto-seed of all
  three SKUs, and all three saga routes driven live: happy → `confirmed`; insufficient → `cancelled ·
  stock_unavailable` (stock untouched); and **decline → `cancelled · payment_declined` with the reserved
  stock released back** — this closes the prior "re-verify decline→release against the current commit"
  item (`reserved 5→0` confirmed). First **real-browser render** of the SPA (headless Chromium) across all
  five routes — browse, cart, My Orders (both terminal states), and both track screens — with live
  cross-origin data, confirming CORS and the `X-Customer-Id` identity seam in an actual browser. **OTel:**
  the `POST /orders` trace stitches **two resources** (orders + inventory) across the RabbitMQ hop into one
  ~50ms trace (see the trace-duration note in [Step 6](#step-6--verify-the-demo-surfaces)). **CritterWatch:**
  **Trial** tier (license read; expires 7/10/2026) with catalog/inventory/orders all connected. A 15-order
  `demo-traffic.ps1` burst (3 declines) settled with `reserved 0` on every SKU — no leaked reservations.
  Zero boot errors.
