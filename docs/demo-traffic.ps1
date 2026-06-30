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

.PARAMETER Backorder
  Switch to "drive the Replenishment saga" mode instead of happy traffic: order a LOW-stock SKU above
  its available stock so the reservation refuses and a backorder saga OPENS (slices 2.5–2.7). Ignores
  -Count/-Continuous/-Decline*. See docs/demo-runbook.md § 5c.

.PARAMETER Cover
  Used with -Backorder: after the saga opens, also POST a covering stock receipt so it RESOLVES in one
  command (open→resolve). Without it the script prints the resolve/partial/escalate follow-ups instead.

.PARAMETER BackorderSku
  The low-stock SKU to backorder. Default crit-rare (canonical seed = 3 units; happy traffic avoids it).

.PARAMETER BackorderQuantity
  Quantity to order in -Backorder mode. Must exceed the SKU's available stock to shortfall. Default 10
  (outstanding 7 against the seeded 3 — visible, and leaves room for a partial-then-cover narration).

.PARAMETER OrdersUrl
  Orders service base URL. Default http://localhost:5103 (the pinned demo port).

.PARAMETER InventoryUrl
  Inventory service base URL — used by -Backorder to read stock and (with -Cover) post the receipt.
  Default http://localhost:5102 (the pinned demo port).

.EXAMPLE
  ./demo-traffic.ps1
  12 orders, ~1.5s apart, a decline every 5th.

.EXAMPLE
  ./demo-traffic.ps1 -Continuous
  Stream until Ctrl+C — the "set it running during the demo" mode.

.EXAMPLE
  ./demo-traffic.ps1 -Count 30 -DelaySeconds 0.8 -DeclineEvery 0
  30 dense happy-path orders, no declines.

.EXAMPLE
  ./demo-traffic.ps1 -Backorder
  Open the Replenishment saga for crit-rare and print the resolve/partial/escalate follow-ups.

.EXAMPLE
  ./demo-traffic.ps1 -Backorder -Cover
  Open the saga AND fire the covering receipt — the full open→resolve beat in one command.
#>
param(
    [int]$Count = 12,
    [double]$DelaySeconds = 1.5,
    [switch]$Continuous,
    [int]$DeclineEvery = 5,
    [double]$DeclinePrice = 250.0,
    [switch]$Backorder,
    [switch]$Cover,
    [string]$BackorderSku = "crit-rare",
    [int]$BackorderQuantity = 10,
    [string]$OrdersUrl = "http://localhost:5103",
    [string]$InventoryUrl = "http://localhost:5102"
)

function ConvertTo-CompactJson($value) { $value | ConvertTo-Json -Compress -Depth 6 }

# ───── Backorder scenario (-Backorder) — drive the Inventory Replenishment saga (slices 2.5–2.7) ─────
# Random happy traffic only touches the 1000-unit crit-001/crit-deluxe SKUs, so it never shortfalls and the
# saga never fires. This mode deliberately orders a LOW-stock SKU (crit-rare, seeded at 3) ABOVE its available
# stock: the reservation refuses (the order cancels stock_unavailable — the same path as runbook § 5a) AND, on
# the Inventory side, a BackorderDetected opens a Replenishment saga for the SKU (the RequestRestock "supplier
# notified" log fires and a ReplenishTimeout is scheduled). With -Cover the script then fires the covering stock
# receipt (RestockArrived) so open→resolve is one command; without it, it prints the copy-paste follow-ups.
# Letting the saga sit instead escalates when ReplenishTimeout fires (~25s under the demo knob —
# Inventory__ReplenishTimeout). Full beat-by-beat: docs/demo-runbook.md § 5c.
if ($Backorder) {
    Write-Host "backorder -> open the Replenishment saga for '$BackorderSku' (Orders $OrdersUrl, Inventory $InventoryUrl)" -ForegroundColor Cyan

    # Read current available so the outstanding (and the -Cover receipt) are correct regardless of prior runs.
    try {
        $available = [int](Invoke-RestMethod "$InventoryUrl/stock/$BackorderSku").available
    }
    catch {
        # GET /stock/{sku} 404s when the SKU never received stock — Inventory treats that as available 0, which
        # still shortfalls (and still opens the saga). Carry on with 0.
        $available = 0
        Write-Host "  (no stock record for '$BackorderSku' — treating available as 0)" -ForegroundColor DarkYellow
    }

    if ($BackorderQuantity -le $available) {
        Write-Host ("  '$BackorderSku' has {0} available; ordering {1} would NOT shortfall — no saga would open." -f $available, $BackorderQuantity) -ForegroundColor Yellow
        Write-Host ("  Re-run with -BackorderQuantity above {0} (e.g. -BackorderQuantity {1})." -f $available, ($available + 5)) -ForegroundColor Yellow
        return
    }

    $outstanding = $BackorderQuantity - $available
    $customer = "backorder-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

    # Place the over-stock order. The cart trusts productSnapshot (never reads Catalog/Inventory) so the order
    # is accepted; the refusal happens later at the cross-BC reservation gate, which is what opens the saga.
    Invoke-RestMethod "$OrdersUrl/carts/mine/items" -Method Post -ContentType application/json -Headers @{ "X-Customer-Id" = $customer } `
        -Body (ConvertTo-CompactJson @{ sku = $BackorderSku; quantity = $BackorderQuantity; productSnapshot = @{ name = 'Rare Critter'; price = 49.99 } }) | Out-Null
    $orderId = (Invoke-RestMethod "$OrdersUrl/orders" -Method Post -Headers @{ "X-Customer-Id" = $customer }).orderId
    Write-Host ("  placed order {0} for {1} x{2} (available {3}) -> reservation will refuse" -f $orderId.Substring(0, 8), $BackorderSku, $BackorderQuantity, $available)

    # The refusal is fast (no payment step) — poll briefly for the cancel. It confirms the shortfall path ran,
    # which is the SAME outgoing set that emits BackorderDetected to open the saga.
    $o = $null
    for ($i = 0; $i -lt 25; $i++) {
        Start-Sleep -Milliseconds 800
        $o = Invoke-RestMethod "$OrdersUrl/orders/$orderId"
        if ($o.status -in 'confirmed', 'cancelled') { break }
    }
    if ($null -ne $o) {
        $statusColor = if ($o.status -eq 'cancelled') { 'Green' } else { 'Yellow' }
        Write-Host ("  order {0}: status={1} reason={2}" -f $orderId.Substring(0, 8), $o.status, $o.cancelReason) -ForegroundColor $statusColor
    }
    Write-Host ("  Replenishment saga OPEN for '{0}' — outstanding {1} (RequestRestock 'supplier notified' logged; ReplenishTimeout scheduled)." -f $BackorderSku, $outstanding) -ForegroundColor Green

    if ($Cover) {
        # Let the sibling BackorderDetected open the saga before the covering RestockArrived lands — a receipt
        # that beats the saga open would be a NotFound no-op (and lost). Processing is tens of ms; 2s is margin.
        Start-Sleep -Seconds 2
        Invoke-RestMethod "$InventoryUrl/stock/$BackorderSku/receipts" -Method Post -ContentType application/json `
            -Body (ConvertTo-CompactJson @{ quantity = $outstanding }) | Out-Null
        Write-Host ("  -Cover: received {0} x{1} -> RestockArrived covers outstanding -> saga RESOLVES (doc deleted)." -f $BackorderSku, $outstanding) -ForegroundColor Green
    }
    else {
        $resolveBody = '{"quantity":' + $outstanding + '}'
        Write-Host ""
        Write-Host "  Next — pick an ending:" -ForegroundColor Cyan
        Write-Host "    resolve : Invoke-RestMethod $InventoryUrl/stock/$BackorderSku/receipts -Method Post -ContentType application/json -Body '$resolveBody'"
        Write-Host ("    partial : receive < {0} (saga reduces outstanding, stays open; no fresh RequestRestock)" -f $outstanding)
        Write-Host  "    escalate: do nothing — ReplenishTimeout fires (~25s demo knob) -> ReplenishmentEscalated 'operator alert'"
    }

    Write-Host ("done — backorder scenario for '{0}'." -f $BackorderSku) -ForegroundColor Cyan
    return
}

# High-stock SKUs from the canonical seed (crit-001 / crit-deluxe start at 1000 each). crit-rare
# (seeded at 3) is deliberately omitted so a sustained run never stalls on that low-stock SKU.
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
