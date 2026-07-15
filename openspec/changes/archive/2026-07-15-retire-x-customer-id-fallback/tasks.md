# Tasks — retire the X-Customer-Id fallback

## 1. Production cut (Orders)

- [x] 1.1 Collapse `CustomerIdentity` to a `ClaimsPrincipal.CustomerId()` extension (delete cases 2–4 of `TryResolve`; case 2's 401 now belongs to JwtBearer middleware)
- [x] 1.2 Add `[Authorize]` to the six customer-keyed endpoints and drop the `[FromHeader("X-Customer-Id")]` parameter + `HttpContext` from their signatures (AddToCart, ViewMyCart, RemoveCartItem, ChangeCartItemQuantity, PlaceOrder, ListMyOrders)
- [x] 1.3 Update `Program.cs` authorization comments (endpoints are now blanket-`[Authorize]`'d; anonymous reads stay anonymous)
- [x] 1.4 Sweep remaining stale `X-Customer-Id` comment references in production code (LocalCustomerView, Identity's Customer + RegisterCustomer, Seeding)

## 2. Test migration

- [x] 2.1 Create `tests/CritterMart.TestSupport` with `JwtTestTokens` (MintToken lifted from TokenAuthTests + `Bearer(id)` convenience); wire into the solution and both test csprojs
- [x] 2.2 Swap `.WithRequestHeader("X-Customer-Id", id)` → `.WithRequestHeader("Authorization", JwtTestTokens.Bearer(id))` across the Orders + CrossBc suites
- [x] 2.3 Invert the layered-cutover fallback test: no token → 401; header-only → 401 with no cart created; convert the five "no identity → 400" tests to 401
- [x] 2.4 Full backend suite green (Orders 104, CrossBc 3, Catalog 9, Inventory 31, Identity 26)

## 3. Demo tooling

- [x] 3.1 `demo-traffic.ps1`: add `$IdentityUrl` + `New-DemoShopper` (register → login → Bearer); migrate the happy loop and backorder mode; fix the stale PR#87 comment
- [x] 3.2 `demo-runbook.md`: migrate Step 4, 5a, 5b, § 5c manual blocks and the bash quick reference to mint-a-shopper → Bearer; update the request-shapes table
- [x] 3.3 Seeder: correct the stale "matches the SPA's X-Customer-Id stub" comment (customer-demo stays passwordless by design)

## 4. Verification

- [x] 4.1 Live-verify on the real Aspire stack: demo-traffic run (register→login→order confirmed), header-only call rejected 401, SPA journey unaffected
- [x] 4.2 Confirm no `X-Customer-Id` remains in production code (grep)

## 5. Close-out

- [x] 5.1 Validate and archive this change; author the session retrospective; open the PR (this step — the archive itself is the act that completes it)
