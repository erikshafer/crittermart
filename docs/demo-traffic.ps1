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

  Most orders are happy-path confirms (1 unit at the catalog price). Every Nth order (-DeclineEvery,
  default 5) instead snapshots a single unit at -DeclinePrice ($250 default, above the AppHost's $200
  Payment:DeclineOverAmount), so the order total trips the payment-decline affordance and exercises
  the cancel + compensating stock-release branch. Sizing the decline by price (not quantity) keeps it
  to one reserved unit, so a busy run's held reservations (the payment auth-delay holds them for
  minutes) can't starve it into a stock_unavailable cancel. The decline path only fires while that
  knob is set; with it removed the stub always approves, so declines simply confirm. Declines RELEASE
  their reservation, so they are stock-neutral; only happy orders consume stock (1 unit each).

  Stock is reseeded on every boot, so a fresh `dotnet run` of the AppHost restores full stock.
  This script does NOT poll orders to completion — it fires and moves on, so the saga runs
  asynchronously in the background and the flourish stays visible on the dashboards.

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

.PARAMETER OrdersUrl
  Orders service base URL. Default http://localhost:5103 (the pinned demo port).

.EXAMPLE
  ./demo-traffic.ps1
  12 orders, ~1.5s apart, a decline every 5th.

.EXAMPLE
  ./demo-traffic.ps1 -Continuous
  Stream until Ctrl+C — the "set it running during the demo" mode.

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
    [string]$OrdersUrl = "http://localhost:5103"
)

function ConvertTo-CompactJson($value) { $value | ConvertTo-Json -Compress -Depth 6 }

# High-stock SKUs from the canonical seed (crit-001 / crit-deluxe start at 100 each). crit-rare
# (seeded at 1) is deliberately omitted so a sustained run never stalls on an exhausted SKU.
$catalog = @(
    @{ sku = 'crit-001'; name = 'Cosmic Critter Plush'; price = 24.99 },
    @{ sku = 'crit-deluxe'; name = 'Deluxe Critter'; price = 24.99 }
)

Write-Host "demo-traffic -> $OrdersUrl  (Ctrl+C to stop)" -ForegroundColor Cyan

$i = 0
while ($Continuous -or $i -lt $Count) {
    $i++
    $product = $catalog | Get-Random
    $decline = ($DeclineEvery -gt 0) -and ($i % $DeclineEvery -eq 0)
    # A decline is about the order TOTAL clearing Payment:DeclineOverAmount, NOT the quantity. Always
    # reserve a single unit (so a busy run's held reservations under the AppHost's payment auth-delay
    # can never starve a decline into a stock_unavailable cancel) and, for declines, inflate the
    # snapshotted unit price above the threshold instead. The cart trusts the productSnapshot price
    # (it never reads Catalog), so the frozen order total = price -> payment_declined.
    $price = if ($decline) { $DeclinePrice } else { $product.price }
    $customer = "traffic-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        Invoke-RestMethod "$OrdersUrl/carts/mine/items" -Method Post -ContentType application/json -Headers @{ "X-Customer-Id" = $customer } `
            -Body (ConvertTo-CompactJson @{ sku = $product.sku; quantity = 1; productSnapshot = @{ name = $product.name; price = $price } }) | Out-Null
        # POST /orders resolves identity from the X-Customer-Id HEADER (PR #87) — there is no
        # request body; a missing header is a 400. (The cart POST above already sends the header.)
        $orderId = (Invoke-RestMethod "$OrdersUrl/orders" -Method Post -Headers @{ "X-Customer-Id" = $customer }).orderId
        $kind = if ($decline) { 'DECLINE' } else { 'happy  ' }
        Write-Host ("[{0,4}] {1}  {2}  {3}  price={4:N2}" -f $i, $kind, $orderId.Substring(0, 8), $product.sku, $price)
    }
    catch {
        Write-Host ("[{0,4}] error: {1}" -f $i, $_.Exception.Message) -ForegroundColor Yellow
    }
    if ($Continuous -or $i -lt $Count) { Start-Sleep -Seconds $DelaySeconds }
}

Write-Host "done — placed $i order(s)." -ForegroundColor Cyan
