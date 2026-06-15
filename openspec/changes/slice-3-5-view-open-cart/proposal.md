# Proposal: Slice 3.5 — View My Open Cart (`GET /carts/mine`)

## Why

Every cart *command* is customer-keyed — the server resolves the Customer's one open cart and the commands never carry a `cartId` (Narrative 004: *"which cart is theirs is the server's business"*). But the only round-one cart *read* is `GET /carts/{cartId}`, and the storefront only learns a `cartId` from an `AddToCart` **response**. So on a **cold load** — the Customer returns in a fresh browser session holding only the stubbed customer id ([ADR 009](../../../docs/decisions/009-polecat-deferred-for-round-one.md)) — the SPA has no `cartId` and cannot render the cart-review screen (wireframe **W2**). This is the pre-frontend endpoint audit's **blocking Gap #1**, and it is the first frontend implementation slice because it is the cleanest: a pure read over an existing projection, no new event.

The data layer already supports the fix. `Program.cs:74` declares a **unique computed index on `CartView.CustomerId` scoped to open carts** — exactly the index a "resolve the Customer's one open cart" query needs. The read model is ready; only the endpoint is missing.

## What Changes

- A new read endpoint `GET /carts/mine` (Orders) resolves the Customer's single open `CartView` by identity and returns it, or `404` when there is no open cart.
- Customer identity arrives in the **`X-Customer-Id` header** (the chosen transport — see `design.md` Decision 1), bound via `[FromHeader]`. This is the round-one stand-in for an authenticated claim behind the ADR 009 `useCurrentCustomer` seam; a missing or blank header is rejected with `400`.
- The endpoint reuses the same open-cart resolution every cart *command* already performs (`session.Query<CartView>().Where(v => v.CustomerId == id && v.IsOpen).FirstOrDefaultAsync()`, `AddToCart.cs:31`), now exposed as a read. **No new event, no new projection, no new index.**
- The deferred `docs/skills/frontend/SKILL.md` is authored — seeded against this first frontend-touching slice (it documents the locked, ADR-backed frontend conventions and how the SPA consumes `GET /carts/mine`).

## Capabilities

### New Capabilities

*(none — the delta extends the existing `shopping-cart` capability, per the one-capability-per-aggregate shape)*

### Modified Capabilities

- `shopping-cart`: 1 ADDED requirement (*Read the Customer's open cart* — the customer-keyed read counterpart to the customer-keyed write side; no requirement text changes for existing behaviors).

## Impact

- **Code**: `src/CritterMart.Orders/Features/ViewMyCart.cs` (new — the `GET /carts/mine` endpoint). No change to `Program.cs` (the index already exists), `CartView`, or any handler.
- **Tests**: `tests/CritterMart.Orders.Tests/ViewMyCartTests.cs` (new — Alba integration: open cart returned, no-open-cart `404`, missing-identity `400`, and a checked-out/abandoned cart resolving to `404`).
- **Docs/skills**: `docs/skills/frontend/SKILL.md` (new — closes the forward pointer named in ADR 015, the audit, Narrative 005).
- **No cross-BC impact**, no contract changes, no broker topology changes, no new packages. The read stays inside Orders.
- **Deferred to a dedicated frontend-bootstrap PR** (NOT this slice): standing up `client/` (Vite app, pinned `package.json`, Aspire `AddViteApp` wiring, TanStack Router, the W2 cart-review screen), the owed Aspire-integration decision (cross-repo-comparison open decision #2), injecting the real frontend origin into each service's `Cors:AllowedOrigins`, and the `npm` block in `.github/dependabot.yml`.
- **Out of band (post-merge tidy)**: workshop § 6 slice 3.5 amendment (record the resolved header transport + the added missing-identity `400` guard); `openspec archive`.
