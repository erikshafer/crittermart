var builder = DistributedApplication.CreateBuilder(args);

// Group CritterMart's containers under a single "crittermart" row in Docker Desktop's Containers
// view — the way the sibling Critter Stack apps (critterbids, critterwatch) already group. Docker
// Desktop renders any container carrying the com.docker.compose.project label as a collapsible
// "project" parent row; it is a UI convention, not a docker-compose dependency. Aspire's DCP
// launches these containers (not docker-compose), so without the label they float loose/ungrouped.
// Only Postgres + RabbitMQ are containers here — the three services and the seeder are dotnet
// processes and the storefront is a node process, so they never appear in Docker Desktop's
// container list. WithContainerRuntimeArgs passes the --label straight through to the runtime.
// Mirrors CritterBids' AppHost (both on the Aspire 13.4 line).
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
    .WithManagementPlugin()
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
    .WaitFor(critterwatch)
    // ───── DEMO AFFORDANCE — the saga's escalate clock ───────────────────────────────────────────────
    // Inventory__ReplenishTimeout: the Replenishment saga's escalate deadline (slice 2.7). When a refused
    // reservation opens the saga (a BackorderDetected — order a SKU over its stock), the saga schedules a
    // ReplenishTimeout for this duration; if no covering RestockArrived (an Operator stock receipt) lands
    // first, the timeout fires and the saga escalates (ReplenishmentEscalated → the "Operator alert" log +
    // bus signal). The default (when unset) is 2 minutes (ReplenishDeadline.Default); 25s makes the escalate
    // beat demoable at speaking pace instead of two minutes of dead air. The service already binds this key
    // (Inventory:ReplenishTimeout via the __→: convention, src/CritterMart.Inventory/Program.cs) — this just
    // supplies the demo value. DELETE after the demo to restore the production-faithful 2-min default.
    // Full how-to: docs/demo-runbook.md § 5c.
    .WithEnvironment("Inventory__ReplenishTimeout", "00:00:25");

// Orders is the second event-sourced service (Cart + Order aggregates, slices 3.1/4.1).
// Slice 4.2 sends its first cross-BC RabbitMQ flow (Reserve stock) to Inventory.
var orders = builder.AddProject<Projects.CritterMart_Orders>("orders")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch)
    // ───── DEMO AFFORDANCES — all three knobs in one place ─────────────────────────────────────────
    // Payment__DeclineOverAmount: the stubbed provider declines any order whose total exceeds this
    // threshold and approves everything at/under it — makes the slice-4.6 decline→cancel→release
    // path triggerable live without swapping providers or restarting. DELETE after the demo to restore
    // "always approve." Full how-to: docs/demo-runbook.md § Step 5 / Payment decline.
    .WithEnvironment("Payment__DeclineOverAmount", "200")
    // Payment__AuthDelay: artificial pause inside the stub before it returns a decision, so the
    // stock_reserved → payment_authorized → confirmed steps are visible at speaking pace.
    // Default (when unset): TimeSpan.Zero (instant). Tune or remove for prod. See PaymentProvider.cs.
    .WithEnvironment("Payment__AuthDelay", "00:03:00")
    // Orders__PaymentTimeout: how long a placed order may sit non-terminal before the scheduled
    // OrderPaymentTimeout self-message fires and cancels it (Bruun temporal automation, slice 4.7).
    // Default (when unset): 10 minutes (PaymentDeadline.Default in OrderPaymentTimeout.cs).
    .WithEnvironment("Orders__PaymentTimeout", "00:07:00");

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
// + their stock) to those services' HTTP endpoints, verifies all products are queryable, then exits.
// The storefront WaitForCompletion(seeder) so the browser never opens on an empty catalog. The two
// service base URLs are injected exactly the way the SPA gets its VITE_*_URL values (ADR 018).
// The seed is idempotent (duplicate SKU → 409 → skip). Set SEEDING_ENABLED=false to disable.
var seeder = builder.AddProject<Projects.CritterMart_Seeding>("seeder")
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
    .WaitFor(orders)
    // Hold the Vite dev server until the seeder has exited — products are in the catalog before
    // any browser tab can open. WaitForCompletion waits for process exit (any exit code), so a
    // seed hiccup still shows red on the dashboard but the storefront starts regardless.
    .WaitForCompletion(seeder);

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
