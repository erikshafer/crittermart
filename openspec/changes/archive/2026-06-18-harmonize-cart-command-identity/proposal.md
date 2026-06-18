# Proposal: Harmonize cart command identity onto the X-Customer-Id header

## Why

The shopping cart's identity transport is inconsistent. The cart READ (`GET /carts/mine`, slice 3.5) resolves the customer from the `X-Customer-Id` header — the ADR 009 `useCurrentCustomer` seam, the round-one stand-in for an authenticated claim. But the three cart COMMANDS still key identity off the route: `POST /carts/{customerId}/items` (add), `POST /carts/{customerId}/items/{sku}/quantity` (change-qty), `DELETE /carts/{customerId}/items/{sku}` (remove). The divergence was logged as a deliberate, deferred "future harmonization tidy" in slices 3.1–3.3 (frontend SKILL Convention 4) — this change is that tidy.

Route-keyed identity also lets any client act as any customer by editing the path, whereas the header is the seam that becomes the authenticated claim under Polecat. Harmonizing the commands onto the header makes identity uniform across the cart surface, and makes the Polecat promotion a localized header→claim swap with call sites unchanged — for the commands, not just the read.

## What Changes

- The three cart commands move to header-keyed routes that mirror the read:
  - `POST /carts/{customerId}/items` → **`POST /carts/mine/items`**
  - `POST /carts/{customerId}/items/{sku}/quantity` → **`POST /carts/mine/items/{sku}/quantity`** (the `{sku}` stays on the route)
  - `DELETE /carts/{customerId}/items/{sku}` → **`DELETE /carts/mine/items/{sku}`**
- Each command resolves the customer from the `X-Customer-Id` header (the same `[FromHeader]` binding + the same blank-header → `400` guard `ViewMyCart` already uses). Open-cart resolution and every domain behavior are unchanged — this is a transport change, not a behavior change.
- The storefront's three cart mutation hooks drop the path interpolation; the `X-Customer-Id` header the shared client already sets on every request becomes authoritative.
- **Place-order (`POST /orders { customerId }`, order-lifecycle) is deliberately out of scope** — it is a fourth, body-keyed transport in a different capability, flagged as a fast-follow.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `shopping-cart`: the three command requirements (*Add an item to the cart*, *Remove an item from the cart*, *Change a cart item's quantity*) gain the explicit identity-transport rule already stated on *Read the Customer's open cart* — identity arrives via the `X-Customer-Id` header, not the route or body, and a missing/blank header is rejected with `400`.

## Impact

- **Code**: `src/CritterMart.Orders/Features/{AddToCart,ChangeCartItemQuantity,RemoveCartItem}.cs` — route attribute + `[FromHeader]` param + blank-header 400 guard. No event, projection, index, or domain-logic change.
- **API** (breaking route change): `/carts/{customerId}/items*` → `/carts/mine/items*`; identity now in the `X-Customer-Id` header.
- **Tests**: every cart-add/edit call-site across the Orders + CrossBc suites repointed to `/carts/mine/*` + header; one new missing-header → `400` test per command.
- **Frontend**: `client/src/cart/cartMutations.ts` (3 hooks), `cartMutations.test.tsx`, frontend SKILL Convention 4 (divergence → harmonized).
- **No cross-BC impact**: pure Cart-stream, in-process HTTP. No Inventory, Contracts, or broker change.
- **Docs**: Narrative 005 (transport moment); the demo runbook, `demo-traffic.ps1`, and the OTel walkthrough route refs; prompt/retro `implementations/032`.
