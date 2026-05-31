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

builder.AddProject<Projects.CritterMart_Catalog>("catalog")
    .WithReference(crittermart)
    .WaitFor(crittermart);

builder.AddProject<Projects.CritterMart_Inventory>("inventory")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq);

// Orders is the second event-sourced service (Cart + Order aggregates, slices 3.1/4.1).
// Slice 4.2 sends its first cross-BC RabbitMQ flow (Reserve stock) to Inventory.
builder.AddProject<Projects.CritterMart_Orders>("orders")
    .WithReference(crittermart)
    .WithReference(rabbitmq)
    .WaitFor(crittermart)
    .WaitFor(rabbitmq);

builder.Build().Run();
