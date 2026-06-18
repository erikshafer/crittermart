# Proposal: Customer data in Orders — slices 5.3 (resolve identity) + 5.4 (consume Published Language)

## Why

Slices 5.1/5.2 landed the Identity customer registry on `main` (PRs #83 + #84): `POST /customers`
registers a customer, `GET /customers/{id}` resolves one, and `CustomerRegistered` publishes over
RabbitMQ unconsumed. The Published-Language edge is declared in the context map but never trafficked.
That gap is the entry point for this change.

Two things follow naturally:

1. **Slice 5.4 — complete the PL edge.** The Orders service (the likeliest consumer — it already
   carries a `customerId` on every `OrderStatusView`) subscribes to `CustomerRegistered` and upserts
   a **consumer-LOCAL** customer read model. This is the moment `CustomerRegistered` graduates from
   an Identity-internal type to a shared `CritterMart.Contracts` Published-Language type. The local
   model is never shared with Identity (no sync call back — ADR 001 forbids it); it is Orders' own
   projection of the customer it has seen. The PL event is eventually consistent: the first order for
   a customer may arrive before the subscription delivers, which is why the local model is "upsert if
   known" rather than a hard dependency.

2. **Slice 5.3 — resolve identity at read time.** With the local model populated, the order endpoints
   (`GET /orders/{orderId}` and `GET /orders/mine`) can enrich their response with the customer's
   display name. Resolution is read-time, against the local model — a second Marten document load
   that costs one index scan — and degrades gracefully to `null` when the customer is not yet known
   (eventually-consistent gap). The `OrderStatusView` projection is untouched; the enrichment is an
   additive wrapper DTO (`EnrichedOrderView`) returned by the endpoint.

A third piece makes the demo work end-to-end: the **seeder needs a deterministic customer id** so the
demo's `X-Customer-Id: customer-demo` header resolves to a real local model entry. The cleanest fix
is to allow `RegisterCustomer` to accept an optional explicit `Id` — the seeder passes
`"customer-demo"`, the server uses it verbatim. All existing callers that omit `Id` get a server-minted
UUID as before.

## What Changes

- **`CritterMart.Contracts/CustomerRegistered.cs`** (new): `CustomerRegistered { CustomerId, Email, DisplayName }` moves from the Identity-internal `CritterMart.Identity.Customers` namespace to the shared contracts assembly. The existing `CritterMart.Identity.Customers.CustomerRegistered` is replaced by a `using` alias.
- **`CritterMart.Identity`**: gains a `ProjectReference` to `CritterMart.Contracts`; `RegisterCustomer` gains `string? Id = null` (server mints UUID when omitted). `CustomerRegistered.cs` in Identity is removed (the type moves to Contracts).
- **`CritterMart.Orders/Customers/LocalCustomerView.cs`** (new): a plain Marten document `{ Id, DisplayName }` keyed by `CustomerId`. The consumer-local projection of the customer.
- **`CritterMart.Orders/Customers/CustomerRegisteredHandler.cs`** (new): a Wolverine handler that handles `CustomerRegistered` arriving from RabbitMQ and upserts `LocalCustomerView` via `session.Store(...)`.
- **`CritterMart.Orders/Ordering/EnrichedOrderView.cs`** (new): an additive response DTO wrapping `OrderStatusView` fields plus `CustomerName?`. Preserves the existing wire shape exactly — `CustomerName` is appended and nullable, so existing deserializers are unaffected.
- **`CritterMart.Orders/Features/PlaceOrder.cs`** (modified): `GET /orders/{orderId}` loads `LocalCustomerView` alongside `OrderStatusView` and returns `EnrichedOrderView`.
- **`CritterMart.Orders/Features/ListMyOrders.cs`** (modified): `GET /orders/mine` loads `LocalCustomerView` (one load, since all orders in "my list" share the requesting customer's id) and wraps results as `EnrichedOrderView`.
- **`CritterMart.Orders/Program.cs`** (modified): `opts.Schema.For<LocalCustomerView>()` registers the document type for Marten schema management.
- **`CritterMart.Seeding/Program.cs`** (modified): extends the seeder to `POST /customers { email, displayName, id: "customer-demo" }` after product/stock seeding; idempotent (409 → skip, mirroring the existing product seed pattern).
- **`CritterMart.AppHost/Program.cs`** (modified): captures `identity` as a variable, injects `IDENTITY_URL` into the seeder, adds `WaitFor(identity)` on the seeder.

### Build order (why 5.4 before 5.3)

The local read model (`LocalCustomerView`) must exist before the enrichment logic can read it. Slice 5.4 creates the model; slice 5.3 reads it. In code terms: `CustomerRegisteredHandler` writes the document; `GET /orders/{orderId}` reads it. Both land in the same PR.

## Capabilities

### New Capabilities

*(none — the delta extends the existing `customer-registry` and `order-lifecycle` capabilities)*

### Modified Capabilities

- **`customer-registry`**: **1 MODIFIED requirement** (*Register a customer*). `RegisterCustomer` gains an optional `Id?` field: when provided the server uses it verbatim; when absent the server mints a UUID. The rest of the requirement (email uniqueness, outbox publication, 409 on duplicate) is unchanged.
- **`order-lifecycle`**: **2 ADDED requirements** (*Consume `CustomerRegistered` and maintain a local customer read model*; *Resolve customer identity at read time*). The Order aggregate, its events, and the `OrderStatusView` projection are unchanged; the new requirements add a subscription handler and a read-time enrichment layer on top.

## Impact

- **Code**: `src/CritterMart.Contracts/CustomerRegistered.cs` (new); `src/CritterMart.Identity/` (csproj ref + RegisterCustomer optional Id + remove local CustomerRegistered); `src/CritterMart.Orders/Customers/` (LocalCustomerView, CustomerRegisteredHandler); `src/CritterMart.Orders/Ordering/EnrichedOrderView.cs`; `src/CritterMart.Orders/Features/` (both order endpoints updated); `src/CritterMart.Orders/Program.cs`; `src/CritterMart.Seeding/Program.cs`; `src/CritterMart.AppHost/Program.cs`.
- **Tests**: `tests/CritterMart.Orders.Tests/CustomerRegisteredHandlerTests.cs` (upsert; idempotency; enrichment present and absent); `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` (optional id acceptance).
- **No frontend changes**: `CustomerName?` is a nullable additive field — existing zod schemas ignore unknown fields; displaying the name is a follow-up frontend slice.
- **No new packages**: `CritterMart.Orders` already references `CritterMart.Contracts` and `WolverineFx.Marten`; `CritterMart.Identity` adds only the Contracts project ref (no new NuGet packages).
- **Broker topology**: renaming `CustomerRegistered`'s namespace changes the RabbitMQ exchange name (from `crittermart.identity.customers:customerregistered` to `crittermart.contracts:customerregistered`). Since the previous exchange had no consumer bindings, no messages are in-flight and the rename is safe. Orders' conventional routing auto-provisions the new exchange and binds its listener queue on startup.
- **Out of band (post-merge tidy)**: `openspec archive customer-data` (syncs the MODIFIED `customer-registry` req + the 2 ADDED `order-lifecycle` reqs into their main specs).
