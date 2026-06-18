using System.Net;
using System.Net.Http.Json;

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
Log($"Seeding — Catalog={catalogUrl}, Inventory={inventoryUrl}, Identity={identityUrl}");

// The canonical demo set. These three SKUs back the three runbook order routes:
//   crit-001    happy path                (Step 4)
//   crit-rare   insufficient-stock cancel (Step 5a — only 1 in stock)
//   crit-deluxe payment-decline cancel    (Step 5b — 5 × $24.99 = $124.95 > the $100 demo threshold)
SeedItem[] seed =
[
    new("crit-001",    "Cosmic Critter Plush", "A plush from the cosmos.", 24.99m, 100),
    new("crit-rare",   "Rare Critter",         "Limited.",                 49.99m, 1),
    new("crit-deluxe", "Deluxe Critter",       "Premium.",                 24.99m, 100),
];

using var catalog = new HttpClient { BaseAddress = new Uri(catalogUrl), Timeout = requestTimeout };
using var inventory = new HttpClient { BaseAddress = new Uri(inventoryUrl), Timeout = requestTimeout };
using var identity = new HttpClient { BaseAddress = new Uri(identityUrl), Timeout = requestTimeout };

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

// Register the demo customer with a deterministic id that matches the SPA's X-Customer-Id stub
// ("customer-demo" in client/src/identity/useCurrentCustomer.tsx). Passing the explicit id lets
// Identity use it verbatim (RegisterCustomer.Id? field, slice 5.4 — the seeder's id becomes the
// LocalCustomerView key in Orders once CustomerRegistered is delivered via RabbitMQ). Idempotent:
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

if (failures == 0)
{
    Log($"Seed complete: {seed.Length} product(s) + {customers.Length} customer(s) ensured.");
    return 0;
}

// Non-zero so the failure shows red on the Aspire dashboard. The storefront does not WaitFor the
// seeder, so a seed hiccup is visible but never blocks the rest of the stack from coming up.
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

// Aspire captures stdout, so the seeder's progress shows in the dashboard's `seeder` console log.
static void Log(string message) => Console.WriteLine($"[seed] {message}");

// One product + its opening stock quantity.
internal sealed record SeedItem(string Sku, string Name, string Description, decimal Price, int Quantity);

// A demo customer with a deterministic id matching the SPA's useCurrentCustomer stub.
internal sealed record DemoCustomer(string Id, string Email, string DisplayName);
