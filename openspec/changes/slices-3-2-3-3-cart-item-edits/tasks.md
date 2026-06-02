# Tasks: Slices 3.2 + 3.3 — Cart Item Edits

## 1. Verify before wiring (ctx7 doc checks)

- [x] 1.1 Verify `[WolverineDelete]` exists in Wolverine.Http 6.x and binds route-only parameters (no request body) the same way `[WolverineGet]` does (ctx7 `/jasperfx/wolverine`)
- [x] 1.2 Verify request-body + route-param binding on `[WolverinePost]` matches the shape `AddToCart` already relies on, for the change-quantity endpoint (`{customerId}`/`{sku}` from route, `newQuantity` from body)

## 2. Cart events + fold

- [x] 2.1 Add `Cart/CartItemRemoved.cs` — `record CartItemRemoved(string Sku)` with the workshop-reference comment
- [x] 2.2 Add `Cart/CartItemQuantityChanged.cs` — `record CartItemQuantityChanged(string Sku, int Quantity)` with the workshop-reference comment
- [x] 2.3 Change `Apply(CartItemAdded)` in `Cart/CartView.cs` to merge-by-SKU (design.md Decision 1)
- [x] 2.4 Add `Apply(CartItemRemoved)` and `Apply(CartItemQuantityChanged)` to `CartViewProjection`
- [x] 2.5 Update the `Cart/CartItemAdded.cs` comment — the "quantity-merge by SKU is a 3.3 concern" deferral is resolved

## 3. Endpoints

- [x] 3.1 Add `Features/RemoveCartItem.cs` — `[WolverineDelete("/carts/{customerId}/items/{sku}")]`: resolve open cart (`NoOpenCart` 409), guard SKU presence (`CartItemNotPresent` 409), append `CartItemRemoved`
- [x] 3.2 Add `Features/ChangeCartItemQuantity.cs` — `[WolverinePost("/carts/{customerId}/items/{sku}/quantity")]`: guard positive quantity (400), resolve open cart (`NoOpenCart` 409), guard SKU presence (`CartItemNotPresent` 409), append `CartItemQuantityChanged`

## 4. Tests

- [x] 4.1 Extend `CartViewProjectionTests` — merge-by-SKU fold (same SKU added twice → one line, summed quantity, first snapshot's price), `CartItemRemoved` fold (line disappears), `CartItemQuantityChanged` fold (quantity updates, price unchanged)
- [x] 4.2 Add `RemoveCartItemTests` (Alba integration) — remove happy path (spec scenario 1), `CartItemNotPresent` 409 (scenario 2), `NoOpenCart` 409, remove-last-item leaves open empty cart (scenario 3 first half)
- [x] 4.3 Add `ChangeCartItemQuantityTests` (Alba integration) — change happy path (spec scenario 1), non-positive 400 (scenario 2), `CartItemNotPresent` 409 (scenario 3), `NoOpenCart` 409
- [x] 4.4 Add the `CartEmpty` case to `PlaceOrderTests` — add item, remove it, place order → 409 `CartEmpty`, no Order stream (spec scenario 3 second half)
- [x] 4.5 Full solution green: `dotnet test` (expect 51 existing + new tests, all passing)

## 5. Narrative + validation

- [x] 5.1 Narrative 004 → v1.6: new Moment (editing the cart), `slices` frontmatter adds 3.2 + 3.3, Forthcoming Moments updated (only 3.4 remains), Document History row
- [x] 5.2 `openspec validate slices-3-2-3-3-cart-item-edits --strict` passes; `openspec validate --all --strict` stays green
