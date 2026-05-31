## 1. Order aggregate (slice 4.1)

- [x] 1.1 Events: `OrderPlaced(string OrderId, string CustomerId, IReadOnlyList<OrderLine> Items, decimal Total)`; `OrderLine(string Sku, int Quantity, string Name, decimal Price)` (`Order/OrderPlaced.cs`)
- [x] 1.2 `OrderStatusView` document (`Id`=orderId, `CustomerId`, `string Status`, `List<OrderLine> Lines`, `decimal Total`) + `OrderStatus.AwaitingConfirmation` constant + `partial class OrderStatusViewProjection : SingleStreamProjection<OrderStatusView, string>` — `Apply(OrderPlaced)` sets customer/status/lines/total (pure fold) (`Order/OrderStatusView.cs`)
- [x] 1.3 Register `OrderStatusViewProjection` inline in `Program.cs` (ADR 008)

## 2. Cart checkout (slice 4.1, shopping-cart delta)

- [x] 2.1 Event: `CartCheckedOut(string OrderId)` (`Cart/CartCheckedOut.cs`)
- [x] 2.2 `Apply(CartCheckedOut, CartView)` on `CartViewProjection` — flip `IsOpen` to false, retain lines; refresh the `IsOpen` comment on `CartView`

## 3. PlaceOrder feature

- [x] 3.1 `PlaceOrder(string CustomerId)` command + `PlaceOrderResponse(string OrderId)` + `POST /orders`: resolve the customer's open `CartView`; none ⇒ `409 NoOpenCart`; lineless ⇒ `409 CartEmpty`; else `StartStream<OrderStatusView>(orderId, OrderPlaced)` + `FetchForWriting<CartView>(cart.Id).AppendOne(CartCheckedOut)` (one transaction). Return `201` + `Location: /orders/{orderId}` + `{ orderId }`
- [x] 3.2 `GET /orders/{orderId}` → `OrderStatusView` (`LoadAsync`, `404` if none)
- [x] 3.3 `dotnet build` succeeds

## 4. Tests

- [x] 4.1 **Unit (pure fold, untagged → CI unit job)**: `OrderStatusViewProjectionTests` — `Apply(OrderPlaced)` yields customer, `awaiting_confirmation`, the two lines, and total `103.98`
- [x] 4.2 **Unit (pure fold)**: add to `CartViewProjectionTests` — `Apply(CartCheckedOut)` flips `IsOpen` false and retains lines
- [x] 4.3 **Integration (GWT happy path)**: add two lines, `PlaceOrder` → new Order stream `OrderPlaced`; `OrderStatusView` shows 2 lines, total `103.98`, `awaiting_confirmation`; Cart stream appends `CartCheckedOut`; `CartView.IsOpen` false
- [x] 4.4 **Integration (GWT failure — no open cart)**: `PlaceOrder` for a customer with no cart → `409`
- [x] 4.5 **Integration (GWT failure — already checked out)**: `PlaceOrder` twice → second `409`; exactly one `OrderStatusView` for the customer
- [x] 4.6 **Integration**: `GET /orders/{orderId}` → `200` `OrderStatusView`

## 5. Verify + close

- [x] 5.1 `dotnet build` + `dotnet test` green (5 unit, 7 integration in the Orders suite)
- [x] 5.2 `openspec validate slice-4-1-place-order --strict` passes
- [x] 5.3 Narrative 004 → v1.1 (Moment 2 — placing the order; `slices` frontmatter `[3.1, 4.1]`; Document History row)
- [x] 5.4 Author `docs/retrospectives/implementations/007-slice-4-1-place-order.md`; `openspec archive slice-4-1-place-order` deferred to a post-merge `tidy:` step
