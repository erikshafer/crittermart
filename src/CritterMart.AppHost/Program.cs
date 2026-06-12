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
builder.AddProject<Projects.CritterMart_Catalog>("catalog")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch);

builder.AddProject<Projects.CritterMart_Inventory>("inventory")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch);

// Orders is the second event-sourced service (Cart + Order aggregates, slices 3.1/4.1).
// Slice 4.2 sends its first cross-BC RabbitMQ flow (Reserve stock) to Inventory.
builder.AddProject<Projects.CritterMart_Orders>("orders")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq)
    .WaitFor(critterwatch);

builder.Build().Run();
