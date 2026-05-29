## 1. Orders service skeleton (blueprint architecture — skeleton + first slice in one PR)

- [x] 1.1 Create `src/CritterMart.Orders/CritterMart.Orders.csproj` (refs `WolverineFx.Http`, `WolverineFx.Marten`, `WolverineFx.RuntimeCompilation`, `Swashbuckle.AspNetCore`; project ref `CritterMart.ServiceDefaults`)
- [x] 1.2 `Program.cs`: Marten event store, `orders` schema (ADR 002), `StreamIdentity.AsString`, register `CartViewProjection` inline (ADR 008), `IntegrateWithWolverine` + `AutoApplyTransactions` + `ApplyAllDatabaseChangesOnStartup`, Wolverine.Http, Swagger; `appsettings.json`
- [x] 1.3 `Properties/launchSettings.json` with `applicationUrl` on `:5103` (distinct port — the `:5000`-collision lesson)
- [x] 1.4 Wire into `src/CritterMart.AppHost/Program.cs` (`AddProject<Projects.CritterMart_Orders>("orders").WithReference(crittermart).WaitFor(crittermart)`) — RabbitMQ not referenced this slice
- [x] 1.5 Add `src/CritterMart.Orders` + `tests/CritterMart.Orders.Tests` to `CritterMart.slnx`; `dotnet build` succeeds

## 2. Cart event sourcing (slice 3.1)

- [x] 2.1 Events: `CartCreated(string CartId, string CustomerId)`, `CartItemAdded(string Sku, int Quantity, ProductSnapshot Snapshot)`; `ProductSnapshot(string Name, decimal Price)`
- [x] 2.2 `CartView` document (`Id`=cartId, `CustomerId`, `bool IsOpen`, `List<CartLine> Lines`; `CartLine(Sku, Quantity, Name, Price)`) + `partial class CartViewProjection : SingleStreamProjection<CartView, string>` — `Apply(CartCreated)` sets `Id`/`CustomerId`/`IsOpen=true`, `Apply(CartItemAdded)` appends a line (pure fold)
- [x] 2.3 Register on `CartView` in `Program.cs`: `Schema.For<CartView>().Index(x => x.CustomerId, idx => { idx.IsUnique = true; idx.Predicate = "(data ->> 'IsOpen')::boolean = true"; })` — one computed index serving both the resolution query and the partial-unique "one open cart per customer" invariant
- [x] 2.4 `AddToCart(string Sku, int Quantity, ProductSnapshot ProductSnapshot)` command + `POST /carts/{customerId}/items`: query `CartView` for the customer's open cart → none ⇒ `MartenOps.StartStream<CartView>(newCartId, CartCreated, CartItemAdded)`; found ⇒ `FetchForWriting<CartView>(open.Id)` + `AppendOne(CartItemAdded)`. Return `201` + `Location: /carts/{cartId}` + `{ cartId }`
- [x] 2.5 `GET /carts/{cartId}` → `CartView` (`LoadAsync`, `404` if none)
- [x] 2.6 `dotnet build` succeeds

## 3. Tests

- [x] 3.1 `tests/CritterMart.Orders.Tests` project (Alba, Testcontainers.PostgreSql, xunit, Shouldly); fixture boots Orders against a throwaway Postgres, clean Marten data between runs
- [x] 3.2 **Unit (pure fold, untagged → CI unit job)**: applying `CartCreated` + `CartItemAdded` events to a `new CartView` yields the expected lines; a second `CartItemAdded` yields two lines — no DB, no mocks
- [x] 3.3 **Integration (GWT scenario 1)**: `customer-X` has no cart; `AddToCart{crit-001, 1, snapshot{Cosmic Critter Plush, 24.99}}` → new stream `CartCreated` + `CartItemAdded`; `CartView` shows one line `crit-001` qty 1 @ 24.99
- [x] 3.4 **Integration (GWT scenario 2)**: with the open cart from 3.3, `AddToCart{crit-002, 3, snapshot{Nebula Newt, 18.00}}` → same stream appends `CartItemAdded`; no new stream; `CartView` shows two lines (24.99 + 18.00)

## 4. Verify + close

- [x] 4.1 `dotnet build` + `dotnet test` green (unit job now selects ≥1 test; integration scenarios pass)
- [x] 4.2 Real run against a Postgres container (the Alba integration tests boot the actual `Program`, so `ApplyAllDatabaseChangesOnStartup` creates the `orders` schema + the partial unique index): first add creates a cart and returns `cartId`; second add appends to the same cart; `GET /carts/{cartId}` reflects both lines
- [x] 4.3 `openspec validate slice-3-1-add-to-cart --strict` passes
- [x] 4.4 Author `docs/retrospectives/implementations/006-slice-3-1-add-to-cart.md` (incl. the Workshop § 6.1 wording amendment: `cartId` keying + `CartActivityTimeout` deferral)
