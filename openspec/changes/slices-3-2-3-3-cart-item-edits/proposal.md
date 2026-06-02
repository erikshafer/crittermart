# Proposal: Slices 3.2 + 3.3 — Cart Item Edits (remove item, change quantity)

## Why

The Customer can put items in their cart (slice 3.1) and take the cart through checkout to a finished order (slices 4.1–4.7), but cannot change their mind before checkout: there is no way to remove an item or adjust a quantity. These are the last two simple Cart-stream slices of the Orders bounded context (Workshop 001 § 5 rows 3.2/3.3), and they force the cart-line identity question slice 3.1 explicitly deferred ("quantity-merge by SKU is a 3.3 concern").

## What Changes

- The Customer can **remove an item** from their open cart: `RemoveCartItem` → `CartItemRemoved { sku }` on the Cart stream; the `CartView` line disappears. Removing a SKU not in the cart is rejected (`CartItemNotPresent`, no event).
- The Customer can **change an item's quantity**: `ChangeCartItemQuantity` → `CartItemQuantityChanged { sku, quantity }` on the Cart stream; the `CartView` line shows the new quantity. Non-positive quantities are rejected (use remove for zero); a SKU not in the cart is rejected (`CartItemNotPresent`).
- The `CartView` fold gains **merge-by-SKU semantics** on `CartItemAdded`: adding a SKU already in the cart increments that line's quantity instead of creating a duplicate line. Cart lines are SKU-keyed from now on (the resolution of 3.1's deferred decision). The event stream still records every add; only the view merges.
- Removing the last line leaves the cart **open and empty** — a legitimate state. Placing an order from an empty cart is rejected (`CartEmpty` — the guard shipped defensively in slice 4.1 becomes reachable, and gains its proving test).
- The Cart-activity timeout refresh named in the workshop slice table for 3.2/3.3 is **deferred to slice 3.4** (same deferral slice 3.1 used).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `shopping-cart`: gains two requirements — *Remove an item from the cart* (3.2) and *Change a cart item's quantity* (3.3). Both edit the open cart resolved by customer, guard against the projected `CartView`, and append exactly one event on success.

## Impact

- **Code**: `src/CritterMart.Orders/Cart/` (two new events, fold updates in `CartView.cs`, comment resolution in `CartItemAdded.cs`); `src/CritterMart.Orders/Features/` (two new endpoints — the project's first `[WolverineDelete]` and a change-quantity POST mirroring Catalog's change-price route shape).
- **API**: `DELETE /carts/{customerId}/items/{sku}` (new), `POST /carts/{customerId}/items/{sku}/quantity` (new). No changes to existing routes.
- **Tests**: new fold unit tests (merge-by-SKU, remove, quantity-change), two new Alba integration test classes, one new `PlaceOrderTests` case (CartEmpty).
- **No cross-BC impact**: both slices are pure Cart-stream, in-process HTTP. No Inventory, no Contracts, no broker involvement.
- **Docs**: Narrative 004 → v1.6 (new Moment: editing the cart); prompt/retro pair `implementations/012`.
