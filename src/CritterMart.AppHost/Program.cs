var builder = DistributedApplication.CreateBuilder(args);

// Group CritterMart's containers under a single "crittermart" row in Docker Desktop's Containers
// view — the way the sibling Critter Stack apps (critterbids, critterwatch) already group. Docker
// Desktop renders any container carrying the com.docker.compose.project label as a collapsible
// "project" parent row; it is a UI convention, not a docker-compose dependency. Aspire's DCP
// launches these containers (not docker-compose), so without the label they float loose/ungrouped.
// Only Postgres + RabbitMQ are containers here — the three services and the seeder are dotnet
// processes and the storefront is a node process, so they never appear in Docker Desktop's
// container list. WithContainerRuntimeArgs passes the --label straight through to the runtime.
// Mirrors CritterBids' AppHost (same Aspire 13.4.3).
const string dockerProject = "crittermart";

// Shared PostgreSQL with one database; each service uses its own schema (ADR 002).
// Naming the database "crittermart" makes WithReference inject ConnectionStrings__crittermart,
// which both services already read via GetConnectionString("crittermart").
var postgres = builder.AddPostgres("postgres")
    .WithContainerRuntimeArgs("--label", $"com.docker.compose.project={dockerProject}");
var crittermart = postgres.AddDatabase("crittermart");

// RabbitMQ for cross-service messaging (ADR 003). The first cross-BC message flows in
// slice 4.2 (Reserve stock): Orders cascades ReserveStock to Inventory and the
// StockReserved / StockReservationFailed reply returns — so both services WithReference it.
// WaitFor lets AutoProvision declare exchanges/queues reliably against a healthy broker.
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithContainerRuntimeArgs("--label", $"com.docker.compose.project={dockerProject}");

// CritterWatch monitoring console (out-of-band trial). It keeps its own event store in a
// dedicated database on the shared Postgres container — separate from the crittermart demo
// database — and receives telemetry from all three services over the existing RabbitMQ broker.
// Services WaitFor it so the dashboard sees their startup events live.
var critterwatchDb = postgres.AddDatabase("critterwatch");

// "critterwatch-console" because the database resource above already owns the name
// "critterwatch" (Aspire resource names share one case-insensitive namespace), and the
// database resource's name is what WithReference injects as the connection-string key.
var critterwatch = builder.AddProject<Projects.CritterMart_CritterWatch>("critterwatch-console")
    .WithReference(critterwatchDb)
    .WithReference(rabbitmq)
    .WaitFor(critterwatchDb)
    .WaitFor(rabbitmq)
    // Production so the console exercises the real JasperFx trial license — in Development
    // it silently substitutes a "Development" tier (expires never) and never reads the key.
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
    .WithExternalHttpEndpoints();

// Catalog gains a RabbitMQ reference solely for CritterWatch telemetry — it still has no
// cross-BC message flows of its own.
var catalog = builder.AddProject<Projects.CritterMart_Catalog>("catalog")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch);

var inventory = builder.AddProject<Projects.CritterMart_Inventory>("inventory")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch);

// Orders is the second event-sourced service (Cart + Order aggregates, slices 3.1/4.1).
// Slice 4.2 sends its first cross-BC RabbitMQ flow (Reserve stock) to Inventory.
var orders = builder.AddProject<Projects.CritterMart_Orders>("orders")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch)
    // ───── DEMO AFFORDANCE — change before the talk / REMOVE after it ──────────────────────────────
    // Makes the payment-DECLINE path (slice 4.6: OrderCancelled{payment_declined} + compensating
    // ReleaseStock back to Inventory) triggerable LIVE: the stubbed payment provider declines any order
    // whose total exceeds this threshold, and approves everything at/under it. So in one running stack,
    // a small order confirms and a large order (total > $100) cancels and releases its reserved stock —
    // no restart, no provider swap. This is the ONLY thing that makes a decline happen at runtime; the
    // whole decline→cancel→release chain itself is real, built, and tested (slice 4.6).
    //   • Change the threshold here to tune which orders decline.
    //   • DELETE this one line to restore round-one "always approve" behavior after the demo.
    //   • Full how-to + the order amounts to use: docs/demo-runbook.md § Step 5 / Payment decline.
    .WithEnvironment("Payment__DeclineOverAmount", "200")
    // Slow the stock_reserved → confirmed transition to demo pace (~20 s). Tune or remove for prod.
    .WithEnvironment("Payment__AuthDelay", "00:00:20");

// Identity — the ONE service that is NOT event-sourced: a deliberately boring EF Core customer
// registry on the shared Postgres, proving Wolverine's handler model is persistence-agnostic
// (same wiring as the Marten services, a DbContext instead of an IDocumentSession). DATA STORE
// per ADR 009, NOT an auth provider (no Polecat). CustomerRegistered now has a consumer in Orders
// (slice 5.4), so the exchange is no longer unconsumed. The seeder calls POST /customers so it
// WaitFor identity and receives IDENTITY_URL — the same injection pattern as CATALOG_URL / INVENTORY_URL.
var identity = builder.AddProject<Projects.CritterMart_Identity>("identity")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch);

// Demo seed automation (closes demo-runbook Known Gap #1). A one-shot console wired as an Aspire
// resource: once Catalog + Inventory are healthy it POSTs the canonical seed (the three demo products
// + their stock) to those services' HTTP endpoints, then exits — so a single `dotnet run` yields a
// demo-ready stack with no manual runbook Step-3 seeding (Aspire's Postgres is ephemeral, wiped each
// boot). It is a LEAF node: nothing WaitFor()s the seeder, so a seed hiccup shows red on the dashboard
// but never blocks the services or the storefront. The two service base URLs are injected exactly the
// way the SPA gets its VITE_*_URL values below (ADR 018 — explicit URLs; no service discovery needed
// for a one-shot tool). The seed is idempotent (duplicate SKU → 409 → skip). Set SEEDING_ENABLED=false
// to disable auto-seed and fall back to the manual runbook Step 3.
builder.AddProject<Projects.CritterMart_Seeding>("seeder")
    .WithEnvironment("CATALOG_URL", catalog.GetEndpoint("http"))
    .WithEnvironment("INVENTORY_URL", inventory.GetEndpoint("http"))
    .WithEnvironment("IDENTITY_URL", identity.GetEndpoint("http"))
    .WaitFor(catalog)
    .WaitFor(inventory)
    .WaitFor(identity);

// The round-two customer storefront SPA (ADR 015) — a Vite + React app launched as part of the Aspire
// orchestration so one `dotnet run` boots the full stack with the frontend visible in the dashboard
// (ADR 004). It is a flat app at client/ (the single round-one SPA). The dev-server port is pinned to
// 5273 so its origin is deterministic — that exact origin is injected into each service's CORS
// allowlist just below, and it is the value ServiceDefaults.AddFrontendCors already falls back to.
// 5273 (not Vite's default 5173) so the storefront coexists with sibling Vite apps that grab 5173
// (e.g. MmoReconnect, CritterBids) — vite.config.ts pins the same value with strictPort:true.
//
// Each service's base URL is injected as a VITE_-prefixed env var (ADR 018 — no BFF, no proxy): Vite
// exposes VITE_* on import.meta.env, which src/config.ts reads and Zod-validates. There is deliberately
// no Vite proxy — the SPA issues genuine cross-origin requests in dev exactly as in the demo, so the
// cross-network OpenTelemetry trace boundary is exercised continuously.
var storefront = builder.AddViteApp("storefront", "../../client")
    .WithHttpEndpoint(port: 5273, name: "http")
    .WithEnvironment("VITE_CATALOG_URL", catalog.GetEndpoint("http"))
    .WithEnvironment("VITE_INVENTORY_URL", inventory.GetEndpoint("http"))
    .WithEnvironment("VITE_ORDERS_URL", orders.GetEndpoint("http"))
    .WaitFor(catalog)
    .WaitFor(inventory)
    .WaitFor(orders);

// CORS-origin injection (ADR 018 — symmetric with the URL injection above; the AppHost is the single
// source of truth for the cross-origin wiring). Each service reads Cors:AllowedOrigins via
// ServiceDefaults.AddFrontendCors; injecting the storefront's endpoint as Cors__AllowedOrigins__0 binds
// to that string[] at index 0. The services are declared before the storefront and never WaitFor it, so
// there is no startup cycle — only a lazily-resolved endpoint reference flowing the other way. The
// pinned 5273 port means the origin is known regardless of resolution order.
var storefrontOrigin = storefront.GetEndpoint("http");
catalog.WithEnvironment("Cors__AllowedOrigins__0", storefrontOrigin);
inventory.WithEnvironment("Cors__AllowedOrigins__0", storefrontOrigin);
orders.WithEnvironment("Cors__AllowedOrigins__0", storefrontOrigin);

builder.Build().Run();
