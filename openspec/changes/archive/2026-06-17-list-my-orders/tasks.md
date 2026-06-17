# Tasks: List my orders — a customer-keyed order list

## 1. Verify before wiring (skill + repo precedent)

- [x] 1.1 `wolverine-http-marten-integration` + `wolverine-testing-alba` skills loaded. The customer-keyed read precedent is in-repo: `GET /carts/mine` (`ViewMyCart.cs`) — `IResult`, inline blank-`X-Customer-Id` → `Results.BadRequest`, `Query<CartView>().Where(v => v.CustomerId == id)`; and the list precedent `GET /products` (`BrowseProducts.cs`). The read-model customer index precedent is `Program.cs:100` (`Schema.For<CartView>().Index(x => x.CustomerId)`).

## 2. Backend — endpoint + index

- [x] 2.1 `src/CritterMart.Orders/Program.cs` — add `opts.Schema.For<OrderStatusView>().Index(x => x.CustomerId);` next to the `CartView` customer index (line 100), with a comment noting it serves the `GET /orders/mine` read.
- [x] 2.2 `src/CritterMart.Orders/Features/ListMyOrders.cs` — new `ListMyOrdersEndpoint` with `[WolverineGet("/orders/mine")]`: `[FromHeader(Name = "X-Customer-Id")] string? customerId` + `IQuerySession`; blank header → `Results.BadRequest`; else `Query<OrderStatusView>().Where(v => v.CustomerId == customerId).OrderByDescending(v => v.PlacedAt).ToListAsync()` → `Results.Ok(orders)`. Mirror `ViewMyCart`'s comments + literal-segment route-precedence note (`/orders/mine` wins over `/orders/{orderId}`).

## 3. Backend — integration proof

- [x] 3.1 `tests/CritterMart.Orders.Tests/ListMyOrdersTests.cs` — Alba tests mirroring `ViewMyCartTests` (`[Collection("orders")]`, `OrdersAppFixture`, reset helper, add-to-cart + place-order helpers):
  - newest-first: two orders for `customer-X` → list of 2, latest first;
  - terminal orders included with `cancelReason` (at minimum a confirmed order present; a cancelled-route assertion if reachable from the HTTP surface, else covered by the projection's own tests);
  - strict scoping: `customer-Y`'s order excluded from `customer-X`'s list;
  - empty: a customer with no orders → `200 []`;
  - missing header → `400`.
- [x] 3.2 Full Orders suite green (`dotnet test`); existing tests unchanged.

## 4. Frontend — My Orders screen

- [x] 4.1 `client/src/orders/orderSchema.ts` — add `OrderListSchema = z.array(OrderStatusViewSchema)` + `type OrderList` (Convention 2; the schema reused wholesale).
- [x] 4.2 `client/src/orders/orderQueries.ts` — add `orderKeys.mine(customerId)` (mirroring `cartKeys.mine`), `fetchMyOrders(ctx)` (`GET /orders/mine` via `fetchParsed`, parsed through `OrderListSchema`), and `myOrdersQueryOptions(ctx)` keyed by `orderKeys.mine(ctx.customerId)`.
- [x] 4.3 `client/src/orders/MyOrdersPage.tsx` — pure list page: loading / error / empty (`200 []` → "You haven't placed any orders yet.") / list states; each row shows the order id, placed-at (reuse `formatPlacedAt`), humanized status (reuse `humanizeStatus` / `humanizeCancelReason`), and the server-computed total, and is a `<Link to="/orders/$orderId">` into the W4 track screen.
- [x] 4.4 `client/src/router.tsx` — add the `/orders` route → `MyOrdersPage`.
- [x] 4.5 `client/src/components/AppShell.tsx` — add a "My Orders" nav `<Link to="/orders">`.
- [x] 4.6 Vitest: `orderSchema.test.ts` (array parse), `orderQueries.test.ts` (`fetchMyOrders` + key), `MyOrdersPage.test.tsx` (states + row link). Full `client` suite green.

## 5. Sibling artifacts (in this PR — design-return content folded into the slice)

- [x] 5.1 `docs/narratives/005-customer-storefront.md` — new **Moment 6 ("Seeing all my orders")**; flip Moment 5's Gap #3 line + the "No 'My Orders' history" bullet to built; update "Forthcoming"; `slices` frontmatter; v1.8 `## Document History` row.
- [x] 5.2 `docs/workshops/001-crittermart-event-model.md` — § 5.1 **v1.12 amendment**: Gap #3 shipped + `/orders/mine` supersedes the `?customerId=` sketch (route-shape faithfulness note).
- [x] 5.3 `docs/prompts/implementations/028-list-my-orders.md` + `docs/retrospectives/implementations/028-list-my-orders.md` — intent, outcome, refinements, spec-delta confirmation, deferred-awareness (pagination; the still-owed OTel/browser visual pass; cart identity-transport candidate).
- [x] 5.4 `openspec validate list-my-orders --strict` green; live boot verification; consolidated PR opened (owner merges).

## 6. Deferred (out of this change)

- [x] 6.1 `openspec archive list-my-orders` (post-merge tidy — syncs the ADDED requirement into `openspec/specs/order-lifecycle/spec.md`, 9 → 10).
- [ ] 6.2 Pagination / filter / sort controls on the list (non-goal; only if the order count grows).
