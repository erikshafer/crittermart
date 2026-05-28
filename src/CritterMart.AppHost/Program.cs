var builder = DistributedApplication.CreateBuilder(args);

// Shared PostgreSQL with one database; each service uses its own schema (ADR 002).
// Naming the database "crittermart" makes WithReference inject ConnectionStrings__crittermart,
// which both services already read via GetConnectionString("crittermart").
var postgres = builder.AddPostgres("postgres");
var crittermart = postgres.AddDatabase("crittermart");

// RabbitMQ for cross-service messaging (ADR 003). Provisioned now so it's in the
// dashboard and ready, but NOT yet referenced by a service — the first cross-BC
// message flows in slice 2.2 (Reserve stock), which will WithReference it then.
// (Gating a service's startup on RabbitMQ health before it consumes RabbitMQ only
// delays boot.)
builder.AddRabbitMQ("rabbitmq");

builder.AddProject<Projects.CritterMart_Catalog>("catalog")
    .WithReference(crittermart)
    .WaitFor(crittermart);

builder.AddProject<Projects.CritterMart_Inventory>("inventory")
    .WithReference(crittermart)
    .WaitFor(crittermart);

builder.Build().Run();
