#requires -Version 7.0
<#
.SYNOPSIS
  CritterMart demo-traffic generator — drives a visible stream of orders so the live message
  flow lights up in CritterWatch (Topology / Services) and the Aspire OpenTelemetry dashboard.

.DESCRIPTION
  Places orders against the RUNNING Orders service using the real /carts + /orders endpoints —
  there is NO special "generate traffic" endpoint in the app, so the demo stays honest. Each
  order kicks off the full cross-BC saga (reserve -> authorize -> commit -> confirm) over
  RabbitMQ, so a steady run paints continuous, real message traffic onto the monitoring surfaces.

  Most orders are happy-path confirms. Every Nth order (-DeclineEvery, default 5) instead snapshots
  a single unit at -DeclinePrice ($250 default, above the AppHost's $200 Payment:DeclineOverAmount),
  so the order total trips the payment-decline affordance and exercises the cancel + compensating
  stock-release branch. Sizing the decline by price (not quantity) keeps it to one reserved unit, so
  a busy run's held reservations (the payment auth-delay holds them for minutes) can't starve it into
  a stock_unavailable cancel. The decline path only fires while that knob is set; with it removed the
  stub always approves, so declines simply confirm. Declines RELEASE their reservation, so they are
  stock-neutral; only happy orders consume stock.

  CW-TELEMETRY SPIKE (research/cw-telemetry-spike). Three OPT-IN knobs feed the projections and
  failure surfaces the spike added. They DEFAULT OFF, so a bare `./demo-traffic.ps1` is byte-for-byte
  the talk's single-line, decline-every-5th flow — the spike richness only appears when you pass the
  flags. See docs/research/cw-telemetry-fodder.md.

    -LinesPerOrder N  A happy order carries a RANDOM 1..N distinct SKUs (not just one). One
                      OrderPlaced then fans out to one ProductSalesLeaderboard document PER SKU and
                      writes one orders.order_line_items row per line — the width that makes the
                      fan-out projection and the flat table actually interesting in CritterWatch.
    -MaxQuantity N    Each happy line reserves a RANDOM 1..N units (not always one), so the
                      leaderboard's per-SKU UnitsSold / GrossRevenue diverge even when order counts
                      match — a leaderboard worth watching climb.
    -PoisonEvery N    Every Nth iteration also POSTs /spike/poison, whose handler always throws.
                      Wolverine exhausts its retries and dead-letters it, so Dead Letters and the
                      Projection-Statuses Error column (always `—` on a healthy system) keep climbing
                      during the run. 0 disables. (Spike branch only; a non-spike stack 404s the
                      endpoint and the script logs a one-time skip.)

  Only the two high-stock seed SKUs (crit-001 / crit-deluxe, 100 each) are rotated; the scarce
  crit-rare (seeded at 3) is deliberately omitted so a sustained run never stalls on an exhausted
  SKU. Richer orders consume stock faster (up to LinesPerOrder x MaxQuantity units each) — stock is
  reseeded on every AppHost boot, so a fresh `dotnet run` restores it. This script does NOT poll
  orders to completion — it fires and moves on, so the saga runs asynchronously in the background and
  the flourish stays visible on the dashboards.

.PARAMETER Count
  How many orders to place (ignored when -Continuous is set). Default 12.

.PARAMETER DelaySeconds
  Pause between orders. Lower = denser traffic. Default 1.5.

.PARAMETER Continuous
  Stream orders until you press Ctrl+C. Best for "run it while I talk through the slides."

.PARAMETER DeclineEvery
  Make every Nth order a payment-decline — one unit snapshotted at -DeclinePrice (shows the
  compensation flow). 0 disables.

.PARAMETER DeclinePrice
  Snapshot unit price for decline orders. Must exceed the AppHost's Payment:DeclineOverAmount ($200
  in the demo) for the order to actually decline. Default 250.

.PARAMETER LinesPerOrder
  CW spike: max DISTINCT SKUs on a happy order; the actual count is randomized 1..N per order.
  Default 1 (single-line, the talk default). Capped at the rotation size (2 high-stock SKUs).

.PARAMETER MaxQuantity
  CW spike: max units per happy line; randomized 1..N per line. Default 1 (the talk default).

.PARAMETER PoisonEvery
  CW spike: also fire POST /spike/poison every Nth iteration to populate Dead Letters. 0 disables
  (default). Requires the spike branch; a non-spike stack 404s and the script logs a one-time skip.

.PARAMETER OrdersUrl
  Orders service base URL. Default http://localhost:5103 (the pinned demo port).

.EXAMPLE
  ./demo-traffic.ps1
  12 single-line orders, ~1.5s apart, a decline every 5th. The talk default — no spike surfaces.

.EXAMPLE
  ./demo-traffic.ps1 -Continuous
  Stream until Ctrl+C — the "set it running during the demo" mode.

.EXAMPLE
  ./demo-traffic.ps1 -Continuous -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 7
  The CW-spike profile: multi-line, multi-unit happy orders feeding the fan-out leaderboard and the
  flat table, a decline every 5th, and a poison message every 7th feeding Dead Letters.

.EXAMPLE
  ./demo-traffic.ps1 -Count 30 -DelaySeconds 0.8 -DeclineEvery 0
  30 dense happy-path orders, no declines.
#>
param(
    [int]$Count = 12,
    [double]$DelaySeconds = 1.5,
    [switch]$Continuous,
    [int]$DeclineEvery = 5,
    [double]$DeclinePrice = 250.0,
    [int]$LinesPerOrder = 1,
    [int]$MaxQuantity = 1,
    [int]$PoisonEvery = 0,
    [string]$OrdersUrl = "http://localhost:5103"
)

function ConvertTo-CompactJson($value) { $value | ConvertTo-Json -Compress -Depth 6 }

# Add one line to the customer's open cart. AddToCart resolves the open cart by X-Customer-Id and
# APPENDS a line (or starts the cart on the first add), so several distinct-SKU calls under one
# customer accumulate into one multi-line cart that checks out as a single multi-line OrderPlaced.
function Add-CartLine($CustomerId, $Sku, $Name, $Price, $Quantity) {
    Invoke-RestMethod "$OrdersUrl/carts/mine/items" -Method Post -ContentType application/json `
        -Headers @{ "X-Customer-Id" = $CustomerId } `
        -Body (ConvertTo-CompactJson @{ sku = $Sku; quantity = $Quantity; productSnapshot = @{ name = $Name; price = $Price } }) | Out-Null
}

# High-stock SKUs from the canonical seed (crit-001 / crit-deluxe start at 100 each). crit-rare
# (seeded at 3) is deliberately omitted so a sustained run never stalls on an exhausted SKU.
$catalog = @(
    @{ sku = 'crit-001'; name = 'Cosmic Critter Plush'; price = 24.99 },
    @{ sku = 'crit-deluxe'; name = 'Deluxe Critter'; price = 24.99 }
)

# Clamp the spike knobs to sane floors (1) and the rotation size, so bad input can't wedge the loop.
if ($LinesPerOrder -lt 1) { $LinesPerOrder = 1 }
if ($LinesPerOrder -gt $catalog.Count) { $LinesPerOrder = $catalog.Count }
if ($MaxQuantity -lt 1) { $MaxQuantity = 1 }
$poisonSkipped = $false   # one-time hint when /spike/poison is absent (non-spike stack)

Write-Host "demo-traffic -> $OrdersUrl  (Ctrl+C to stop)" -ForegroundColor Cyan
if ($LinesPerOrder -gt 1 -or $MaxQuantity -gt 1 -or $PoisonEvery -gt 0) {
    Write-Host ("  CW-spike profile: up to {0} line(s)/order, up to {1} unit(s)/line{2}" -f `
        $LinesPerOrder, $MaxQuantity, $(if ($PoisonEvery -gt 0) { ", poison every $PoisonEvery" } else { "" })) -ForegroundColor DarkCyan
}

$i = 0
while ($Continuous -or $i -lt $Count) {
    $i++
    $decline = ($DeclineEvery -gt 0) -and ($i % $DeclineEvery -eq 0)
    $customer = "traffic-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        if ($decline) {
            # A decline is about the order TOTAL clearing Payment:DeclineOverAmount, NOT the quantity.
            # Always a SINGLE unit (so a busy run's held reservations under the AppHost's payment
            # auth-delay can never starve a decline into a stock_unavailable cancel) at an inflated
            # snapshot price above the threshold. The cart trusts the productSnapshot price (it never
            # reads Catalog), so the frozen order total = price -> payment_declined.
            $product = $catalog | Get-Random
            Add-CartLine $customer $product.sku $product.name $DeclinePrice 1
            $summary = "{0} x1 @ {1:N2}" -f $product.sku, $DeclinePrice
        }
        else {
            # Happy order: a random 1..LinesPerOrder DISTINCT SKUs (Get-Random -Count returns distinct
            # elements), each a random 1..MaxQuantity units. At the defaults (1/1) this is exactly the
            # talk's single-unit order; with the spike knobs it becomes the multi-line, multi-unit shape
            # that fans the leaderboard out and fills the flat table. Distinct SKUs keep one line per SKU
            # (no same-SKU merge question), and 2 SKUs x <=2 units stays well under the $200 threshold.
            $lineCount = Get-Random -Minimum 1 -Maximum ($LinesPerOrder + 1)
            $lines = @($catalog | Get-Random -Count $lineCount)
            $parts = foreach ($product in $lines) {
                $qty = Get-Random -Minimum 1 -Maximum ($MaxQuantity + 1)
                Add-CartLine $customer $product.sku $product.name $product.price $qty
                "{0} x{1}" -f $product.sku, $qty
            }
            $summary = $parts -join ', '
        }

        # POST /orders resolves identity from the X-Customer-Id HEADER (PR #87) — there is no request
        # body; a missing header is a 400. The cart POST(s) above already sent the same header.
        $orderId = (Invoke-RestMethod "$OrdersUrl/orders" -Method Post -Headers @{ "X-Customer-Id" = $customer }).orderId
        $kind = if ($decline) { 'DECLINE' } else { 'happy  ' }
        Write-Host ("[{0,4}] {1}  {2}  {3}" -f $i, $kind, $orderId.Substring(0, 8), $summary)
    }
    catch {
        Write-Host ("[{0,4}] error: {1}" -f $i, $_.Exception.Message) -ForegroundColor Yellow
    }

    # CW spike: every Nth iteration, lob a poison message onto the bus. Its handler always throws, so
    # Wolverine dead-letters it after exhausting retries — feeding Dead Letters and the Error column.
    if ($PoisonEvery -gt 0 -and $i % $PoisonEvery -eq 0) {
        try {
            Invoke-RestMethod "$OrdersUrl/spike/poison" -Method Post | Out-Null
            Write-Host ("[{0,4}] POISON  -> /spike/poison (dead-letter expected)" -f $i) -ForegroundColor Magenta
        }
        catch {
            if (-not $poisonSkipped) {
                Write-Host "  note: /spike/poison not found — poison firing needs the cw-telemetry-spike branch. Skipping." -ForegroundColor DarkYellow
                $poisonSkipped = $true
            }
        }
    }

    if ($Continuous -or $i -lt $Count) { Start-Sleep -Seconds $DelaySeconds }
}

Write-Host "done — placed $i order(s)." -ForegroundColor Cyan
