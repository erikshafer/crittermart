# Tasks: customer-data (slices 5.3 + 5.4)

## Implementation #034 — branch `feat/identity-slices-5-3-5-4`

### Slice 5.4 — Consume `CustomerRegistered` (build first)

- [ ] Add `src/CritterMart.Contracts/CustomerRegistered.cs` (namespace `CritterMart.Contracts`, same fields `CustomerId`, `Email`, `DisplayName`)
- [ ] Add `ProjectReference` to `CritterMart.Contracts` in `src/CritterMart.Identity/CritterMart.Identity.csproj`
- [ ] Remove `src/CritterMart.Identity/Customers/CustomerRegistered.cs` (the type now lives in Contracts)
- [ ] Update `src/CritterMart.Identity/Features/RegisterCustomer.cs` to `using CritterMart.Contracts;`
- [ ] Add `src/CritterMart.Orders/Customers/LocalCustomerView.cs`
- [ ] Add `src/CritterMart.Orders/Customers/CustomerRegisteredHandler.cs`
- [ ] Register `opts.Schema.For<LocalCustomerView>()` in `src/CritterMart.Orders/Program.cs`

### Slice 5.3 — Resolve identity at read time (after 5.4)

- [ ] Add `src/CritterMart.Orders/Ordering/EnrichedOrderView.cs`
- [ ] Update `GET /orders/{orderId}` in `src/CritterMart.Orders/Features/PlaceOrder.cs` to return `EnrichedOrderView`
- [ ] Update `GET /orders/mine` in `src/CritterMart.Orders/Features/ListMyOrders.cs` to return `EnrichedOrderView`

### Seeder + Identity explicit-id support

- [ ] Add `string? Id = null` to `RegisterCustomer` record in `src/CritterMart.Identity/Features/RegisterCustomer.cs`
- [ ] Use `command.Id ?? Guid.NewGuid().ToString()` in `RegisterCustomerEndpoint.Post`
- [ ] Capture `identity` as a variable in `src/CritterMart.AppHost/Program.cs`
- [ ] Inject `IDENTITY_URL` + `WaitFor(identity)` on the seeder in AppHost
- [ ] Add customer seed block in `src/CritterMart.Seeding/Program.cs`

### Tests

- [ ] `tests/CritterMart.Orders.Tests/CustomerRegisteredHandlerTests.cs` (upsert, idempotency, enrichment present/absent)
- [ ] Update `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` with explicit-id scenario

### Artifacts

- [ ] `docs/narratives/007-customer-data-in-orders.md` (v1.0)
- [ ] `docs/prompts/implementations/034-slices-5-3-5-4-customer-data.md`
- [ ] `docs/retrospectives/implementations/034-slices-5-3-5-4-customer-data.md`
- [ ] `openspec archive customer-data -y` (post-merge tidy PR)
