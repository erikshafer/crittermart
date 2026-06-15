var builder = DistributedApplication.CreateBuilder(args);

// Shared PostgreSQL with one database; each service uses its own schema (ADR 002).
// Naming the database "crittermart" makes WithReference inject ConnectionStrings__crittermart,
// which both services already read via GetConnectionString("crittermart").
var postgres = builder.AddPostgres("postgres");
var crittermart = postgres.AddDatabase("crittermart");

// RabbitMQ for cross-service messaging (ADR 003). The first cross-BC message flows in
// slice 4.2 (Reserve stock): Orders cascades ReserveStock to Inventory and the
// StockReserved / StockReservationFailed reply returns — so both services WithReference it.
// WaitFor lets AutoProvision declare exchanges/queues reliably against a healthy broker.
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

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
    .WaitFor(critterwatch);

// The round-two customer storefront SPA (ADR 015) — a Vite + React app launched as part of the Aspire
// orchestration so one `dotnet run` boots the full stack with the frontend visible in the dashboard
// (ADR 004). It is a flat app at client/ (the single round-one SPA). The dev-server port is pinned to
// 5173 so its origin is deterministic — that exact origin is injected into each service's CORS
// allowlist just below, and it is the value ServiceDefaults.AddFrontendCors already falls back to.
//
// Each service's base URL is injected as a VITE_-prefixed env var (ADR 018 — no BFF, no proxy): Vite
// exposes VITE_* on import.meta.env, which src/config.ts reads and Zod-validates. There is deliberately
// no Vite proxy — the SPA issues genuine cross-origin requests in dev exactly as in the demo, so the
// cross-network OpenTelemetry trace boundary is exercised continuously.
var storefront = builder.AddViteApp("storefront", "../../client")
    .WithHttpEndpoint(port: 5173, name: "http")
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
// pinned 5173 port means the origin is known regardless of resolution order.
var storefrontOrigin = storefront.GetEndpoint("http");
catalog.WithEnvironment("Cors__AllowedOrigins__0", storefrontOrigin);
inventory.WithEnvironment("Cors__AllowedOrigins__0", storefrontOrigin);
orders.WithEnvironment("Cors__AllowedOrigins__0", storefrontOrigin);

builder.Build().Run();
