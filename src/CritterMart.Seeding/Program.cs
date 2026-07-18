using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

// CritterMart demo seeder — closes demo-runbook Known Gap #1.
//
// Aspire's Postgres is ephemeral (no data volume), so the DB is empty on every boot. This
// one-shot console runs as an Aspire resource: once Catalog + Inventory are healthy it POSTs
// the canonical demo seed (products + stock) to their real HTTP endpoints, then exits. One
// `dotnet run` on the AppHost now yields a demo-ready stack — no manual runbook Step 3.
//
// It drives the SAME endpoints a client would (POST /products, POST /stock/{sku}/receipts),
// so it exercises the real PublishProduct / ReceiveStock handlers and appends real events. It
// takes no project reference on any service — only two base URLs, injected by the AppHost the
// same way the SPA gets VITE_*_URL (ADR 018) — which keeps services decoupled.

// --- Configuration (env-driven; localhost fallbacks let it also run standalone vs. a manual boot) ---
const int maxAttempts = 8;                              // readiness margin on top of Aspire's WaitFor
var retryDelay = TimeSpan.FromSeconds(1.5);
var requestTimeout = TimeSpan.FromSeconds(10);

var enabled = Environment.GetEnvironmentVariable("SEEDING_ENABLED") ?? "true";
if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
{
    Log("SEEDING_ENABLED is not 'true' — skipping auto-seed (manual runbook Step 3 applies).");
    return 0;
}

var catalogUrl = Environment.GetEnvironmentVariable("CATALOG_URL") ?? "http://localhost:5101";
var inventoryUrl = Environment.GetEnvironmentVariable("INVENTORY_URL") ?? "http://localhost:5102";
var identityUrl = Environment.GetEnvironmentVariable("IDENTITY_URL") ?? "http://localhost:5105";
var ordersUrl = Environment.GetEnvironmentVariable("ORDERS_URL") ?? "http://localhost:5103";
Log($"Seeding — Catalog={catalogUrl}, Inventory={inventoryUrl}, Identity={identityUrl}, Orders={ordersUrl}");

// The canonical demo set. These three SKUs back the three runbook order routes:
//   crit-001    happy path                (Step 4)
//   crit-rare   insufficient-stock cancel (Step 5a — only 3 in stock; order 4+ to trigger)
//   crit-deluxe payment-decline cancel    (Step 5b — 9 × $24.99 = $224.91 > the $200 demo threshold)
//
// Stock quantities (the last field) are deliberately split by role:
//   - crit-001 / crit-deluxe seed HIGH (1000) so a sustained demo-traffic.ps1 run paints continuous
//     message flow onto CritterWatch without draining the pool. Happy orders permanently consume one
//     unit each and (under the 3-min Payment__AuthDelay) hold it for the auth window before committing,
//     so a busy run burns through stock; 1000/SKU buys ~tens of minutes of continuous default-rate
//     traffic before the pool can flip to stock_unavailable cancels. (Mitigates demo-runbook G5.)
//   - crit-rare seeds LOW (3) on purpose: it IS the insufficient-stock route. Ordering >3 must refuse,
//     so this number must stay small. Do NOT raise it to match the others.
SeedItem[] seed =
[
    new("crit-001",    "Cosmic Critter Plush", "A plush from the cosmos.", 24.99m, 1000),
    new("crit-rare",   "Rare Critter",         "Limited.",                 49.99m, 3),
    new("crit-deluxe", "Deluxe Critter",       "Premium.",                 24.99m, 1000),
];

using var catalog = new HttpClient { BaseAddress = new Uri(catalogUrl), Timeout = requestTimeout };
using var inventory = new HttpClient { BaseAddress = new Uri(inventoryUrl), Timeout = requestTimeout };
using var identity = new HttpClient { BaseAddress = new Uri(identityUrl), Timeout = requestTimeout };
using var orders = new HttpClient { BaseAddress = new Uri(ordersUrl), Timeout = requestTimeout };

var failures = 0;
foreach (var item in seed)
{
    // Publish the product. A duplicate SKU is a modeled 409 (PublishProduct stores nothing on
    // conflict), so the whole seed is idempotent: gate the stock receipt on a fresh 201, and a
    // re-run against an already-seeded DB is a no-op.
    using var publish = await PostJsonAsync(catalog, "/products",
        new { sku = item.Sku, name = item.Name, description = item.Description, price = item.Price });

    switch (publish.StatusCode)
    {
        case HttpStatusCode.Created:
            Log($"published {item.Sku} \"{item.Name}\" (${item.Price})");
            using (var receipt = await PostJsonAsync(inventory, $"/stock/{item.Sku}/receipts",
                       new { quantity = item.Quantity }))
            {
                if (receipt.IsSuccessStatusCode)
                {
                    Log($"  received {item.Quantity} units of {item.Sku}");
                }
                else
                {
                    Log($"  FAILED stock receipt for {item.Sku} -> HTTP {(int)receipt.StatusCode}");
                    failures++;
                }
            }
            break;

        case HttpStatusCode.Conflict:
            Log($"{item.Sku} already seeded — skipping (idempotent).");
            break;

        default:
            Log($"FAILED to publish {item.Sku} -> HTTP {(int)publish.StatusCode}");
            failures++;
            break;
    }
}

// Register the demo customer with a deterministic, human-readable id ("customer-demo" — originally
// matching the SPA's now-retired X-Customer-Id stub, kept because a stable id makes demo data easy
// to talk about). Passing the explicit id lets Identity use it verbatim (RegisterCustomer.Id? field,
// slice 5.4 — the seeder's id becomes the LocalCustomerView key in Orders once CustomerRegistered is
// delivered via RabbitMQ). This is the PASSWORDLESS admin path (POST /customers), so customer-demo
// has no login credentials — a browsing SPA visitor registers their own shopper via /register, and
// demo-traffic.ps1 does the same per run (ADR 023: Bearer is the only identity transport). Idempotent:
// a duplicate email → 409 CustomerAlreadyRegistered → skip, mirroring the product seed pattern.
DemoCustomer[] customers =
[
    new("customer-demo", "demo@crittermart.com", "Demo Customer"),
];

foreach (var c in customers)
{
    using var register = await PostJsonAsync(identity, "/customers",
        new { id = c.Id, email = c.Email, displayName = c.DisplayName });

    switch (register.StatusCode)
    {
        case HttpStatusCode.Created:
            Log($"registered customer {c.Id} \"{c.DisplayName}\" ({c.Email})");
            break;
        case HttpStatusCode.Conflict:
            Log($"customer {c.Id} already registered — skipping (idempotent).");
            break;
        default:
            Log($"FAILED to register customer {c.Id} -> HTTP {(int)register.StatusCode}");
            failures++;
            break;
    }
}

// The demo coupon set (Workshop 003 slice 6.1, configuration-as-events). Defined via Orders' POST /coupons
// — the same real-HTTP, no-project-reference pattern as the products above (ADR 024). Two coupons by role:
//   FLASH20    the RACE coupon (cap 3): small enough to drive to breach by hand and demonstrate the DCB —
//              order 4 gets 409 CouponExhausted; cancel one and the slot returns. Do NOT raise the cap.
//   WELCOME10  everyday discount (cap 100000): a high cap so sustained demo-traffic can apply it without
//              exhausting it — the "normal discounted order" path, distinct from the flash-sale race.
//   FIRSTORDER the PER-CUSTOMER coupon (slice 6.5, oneRedemptionPerCustomer): a high global cap but each
//              customer may redeem it at most once — the composite (coupon × customer) DCB. Demo: a customer
//              redeems it, their SECOND attempt gets 409 CouponAlreadyRedeemedByCustomer, another customer
//              still succeeds. The second DCB (ADR 024 §38) made visible.
DemoCoupon[] coupons =
[
    new("FLASH20", 20, 3),
    new("WELCOME10", 10, 100000),
    new("FIRSTORDER", 15, 100000, OneRedemptionPerCustomer: true),
];

foreach (var c in coupons)
{
    using var define = await PostJsonAsync(orders, "/coupons",
        new { code = c.Code, discountPercent = c.DiscountPercent, cap = c.Cap, oneRedemptionPerCustomer = c.OneRedemptionPerCustomer });

    switch (define.StatusCode)
    {
        case HttpStatusCode.Created:
            Log($"defined coupon {c.Code} ({c.DiscountPercent}% off, cap {c.Cap}{(c.OneRedemptionPerCustomer ? ", 1/customer" : "")})");
            break;
        case HttpStatusCode.Conflict:
            Log($"coupon {c.Code} already defined — skipping (idempotent).");
            break;
        default:
            Log($"FAILED to define coupon {c.Code} -> HTTP {(int)define.StatusCode}");
            failures++;
            break;
    }
}

if (failures == 0)
{
    // Verify all seeded products are actually queryable before exiting. Inline Marten projections
    // are written in the same transaction as the event, so visibility should be immediate — this
    // verification closes the gap between "201 received" and "readable via GET /products", and
    // ensures the storefront (which WaitForCompletion this process) opens on a full catalog.
    Log("Verifying catalog is queryable...");
    if (!await VerifyCatalogAsync(catalog, seed.Select(s => s.Sku).ToHashSet()))
    {
        Log($"Catalog verification timed out after {maxAttempts} attempt(s).");
        return 1;
    }

    Log($"Seed complete: {seed.Length} product(s) + {customers.Length} customer(s) + {coupons.Length} coupon(s) ensured.");
    return 0;
}

// Non-zero so the failure shows red on the Aspire dashboard.
Log($"Seed finished with {failures} failure(s).");
return 1;

// POST a JSON body, retrying transient failures (connection refused / timeout / 5xx). A service can
// report "running" to Aspire's WaitFor a moment before Marten's schema/JIT is ready for the first
// request; the retry covers that gap. 2xx and 4xx (incl. the idempotent 409) are terminal — not retried.
async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object body)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            var response = await client.PostAsJsonAsync(path, body);
            if ((int)response.StatusCode >= 500 && attempt < maxAttempts)
            {
                Log($"  {path} -> HTTP {(int)response.StatusCode}; retry {attempt}/{maxAttempts} in {retryDelay.TotalSeconds:0.#}s");
                response.Dispose();
                await Task.Delay(retryDelay);
                continue;
            }

            return response;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < maxAttempts)
        {
            Log($"  {path} not reachable ({ex.GetType().Name}); retry {attempt}/{maxAttempts} in {retryDelay.TotalSeconds:0.#}s");
            await Task.Delay(retryDelay);
        }
    }
}

// GET /products and confirm every expected SKU is in the response. Retries to account for any
// last-millisecond lag between the 201 acknowledgement and the document being visible to a query.
async Task<bool> VerifyCatalogAsync(HttpClient client, ISet<string> expectedSkus)
{
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var items = await client.GetFromJsonAsync<ProductView[]>("/products", jsonOptions);
            var found = (items ?? []).Select(p => p.Sku).ToHashSet();
            if (expectedSkus.All(found.Contains))
            {
                Log($"  Catalog verified: all {expectedSkus.Count} product(s) queryable.");
                return true;
            }
            Log($"  Catalog: {found.Count}/{expectedSkus.Count} product(s) visible; retry {attempt}/{maxAttempts} in {retryDelay.TotalSeconds:0.#}s");
        }
        catch (Exception ex)
        {
            Log($"  Catalog verify error ({ex.GetType().Name}); retry {attempt}/{maxAttempts} in {retryDelay.TotalSeconds:0.#}s");
        }
        await Task.Delay(retryDelay);
    }
    return false;
}

// Aspire captures stdout, so the seeder's progress shows in the dashboard's `seeder` console log.
static void Log(string message) => Console.WriteLine($"[seed] {message}");

// One product + its opening stock quantity.
internal sealed record SeedItem(string Sku, string Name, string Description, decimal Price, int Quantity);

// A demo customer with a deterministic id matching the SPA's useCurrentCustomer stub.
internal sealed record DemoCustomer(string Id, string Email, string DisplayName);

// A demo coupon (Workshop 003 slice 6.1): code, percentage discount, and the global redemption cap N.
// Slice 6.5 adds OneRedemptionPerCustomer — when true, the coupon is also once-per-customer (composite DCB).
internal sealed record DemoCoupon(string Code, int DiscountPercent, int Cap, bool OneRedemptionPerCustomer = false);

// Minimal shape for deserialising GET /products responses during catalog verification.
internal sealed record ProductView(string Sku);
