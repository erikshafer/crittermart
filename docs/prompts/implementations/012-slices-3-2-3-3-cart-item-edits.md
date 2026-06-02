# Prompt: Implementations 012 — Slices 3.2 + 3.3 Cart Item Edits (remove item, change quantity)

**Kind**: per-slice implementation, consolidated one PR (narrative bump + OpenSpec change + implementation + prompt/retro), per the consolidate-slice-prs convention. Covers **two workshop slices** (3.2 remove item, 3.3 change quantity) in one PR — the scope fork resolved with the user at session start (the third slice, 3.4 cart abandonment, gets its own PR because it carries the Bruun temporal automation + the async-projection teaser). First of the Cart-side slices that finish the Orders BC.
**Files touched**: this prompt; `openspec/changes/slices-3-2-3-3-cart-item-edits/{proposal.md, design.md, tasks.md, specs/shopping-cart/spec.md}` (new — one capability delta, two ADDED requirements); `docs/narratives/004-customer-purchase.md` (→ v1.6, new Moment — editing the cart); `src/CritterMart.Orders/Cart/CartItemRemoved.cs` (new), `src/CritterMart.Orders/Cart/CartItemQuantityChanged.cs` (new), `src/CritterMart.Orders/Cart/CartItemAdded.cs` (comment update — the "3.3 concern" is resolved), `src/CritterMart.Orders/Cart/CartView.cs` (fold: merge-by-SKU on add + remove + quantity-change Apply methods); `src/CritterMart.Orders/Features/RemoveCartItem.cs` (new endpoint), `src/CritterMart.Orders/Features/ChangeCartItemQuantity.cs` (new endpoint); `tests/CritterMart.Orders.Tests/{CartViewProjectionTests.cs (amend — new fold tests), RemoveCartItemTests.cs (new), ChangeCartItemQuantityTests.cs (new), PlaceOrderTests.cs (amend — the CartEmpty guard finally becomes reachable)}`; `docs/retrospectives/implementations/012-slices-3-2-3-3-cart-item-edits.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; collaborative on genuine forks (present options + recommendation, user decides — memory `feedback-collaborate-on-decisions`, `feedback-options-with-previews`)
**Commit subject**: `feat: slices 3.2+3.3 cart item edits`

## Framing

Slice 3.1 let the Customer put things *in* the cart; slices 4.1–4.7 took the cart through checkout to a finished order. What's still missing is the Customer changing their mind *before* checkout: taking an item back out (3.2) and changing how many they want (3.3). Both are simple Cart-stream slices — a command, a guard against the projected `CartView`, one new event kind, a fold update. Their real significance is structural: they force the cart-line identity question that 3.1 explicitly deferred ("quantity-merge by SKU is a 3.3 concern"), and 3.2 makes a **lineless-but-open cart** reachable for the first time — turning `PlaceOrder`'s defensive `CartEmpty` guard from dead code into live code.

## Goal

The Customer can remove an item from their open cart (`DELETE /carts/{customerId}/items/{sku}` → `CartItemRemoved { sku }`) and change an item's quantity (`POST /carts/{customerId}/items/{sku}/quantity { newQuantity }` → `CartItemQuantityChanged { sku, quantity }`). Both commands resolve the customer's open cart the same way `AddToCart` does, guard against the projected `CartView` (item must be present; quantity must be positive), and append exactly one event on success. The `CartView` fold gains merge-by-SKU semantics on `CartItemAdded` (the deferred 3.1 decision, resolved this session) plus `Apply` methods for the two new events. Removing the last line leaves the cart **open and empty** — and a new `PlaceOrderTests` case proves the `CartEmpty` 409 path. `openspec validate --strict` passes; full solution green.

## Spec delta

A new OpenSpec change `slices-3-2-3-3-cart-item-edits` with **one** capability delta:
- **`shopping-cart`** (two ADDED requirements):
  1. *Remove an item from the cart* — the Customer removes a SKU from their open cart; the Cart stream appends `CartItemRemoved { sku }` and the `CartView` line disappears; removing a SKU not in the cart is rejected (`CartItemNotPresent`, no event); removing the last line leaves the cart open and empty, and placing an order from it is rejected (`CartEmpty`).
  2. *Change a cart item's quantity* — the Customer sets a new quantity for a SKU in their open cart; the Cart stream appends `CartItemQuantityChanged { sku, quantity }` and the `CartView` line shows the new quantity; a non-positive quantity is rejected (use remove for zero; no event); a SKU not in the cart is rejected (`CartItemNotPresent`, no event).

Narrative 004 gains a new Moment (→ v1.6, `slices` adds 3.2 + 3.3). Workshop § 6.1 slices 3.2 + 3.3 GWTs (happy + failure paths) are satisfied. **Faithfulness notes for design.md**: (a) merge-by-SKU on add is *implied* by the workshop's SKU-keyed GWTs but never stated — the resolution of 3.1's deferred decision is recorded here; (b) the workshop's 3.3 failure path covers only non-positive quantity — the SKU-not-present rejection mirrors 3.2's `CartItemNotPresent` (an obvious extension, not in the workshop text); (c) the slice table's "refresh `CartActivityTimeout`" writes-to clauses on 3.2/3.3 are **deferred to 3.4** (same deferral 3.1 used, § 8 item 1).

## Locked decisions (collaborative forks, resolved with the user this session)

1. **PR scope: 3.2 + 3.3 in this PR; 3.4 separate.** The two simple Cart-stream slices consolidate; the temporal automation + async-projection-teaser slice gets its own session/PR (handoff's recommended option, user confirmed).
2. **Merge-by-SKU in the fold.** `Apply(CartItemAdded)` checks for an existing line with the same SKU: present → quantity increments (the first add's snapshot name/price stays authoritative); absent → new line. The event stream still records every add; only the view merges. Cart lines are SKU-keyed from now on, which is what makes `RemoveCartItem { sku }` and `ChangeCartItemQuantity { sku }` unambiguous. No endpoint behavior change for 3.1; existing tests (different SKUs) stay green.
3. **Routes follow the codebase's command-shaped-POST convention, plus the project's first DELETE** *(session-runner decision from precedent, not a fork)*: `POST /carts/{customerId}/items/{sku}/quantity` mirrors Catalog's `POST /products/{sku}/price` (slice 1.3); `DELETE /carts/{customerId}/items/{sku}` is the natural verb for removal — the codebase's first `[WolverineDelete]`. Both key on `customerId` (open-cart resolution) like `AddToCart`, not on `cartId`.
4. **Failure-path status codes follow the established `Results.Problem` 409 pattern**: `NoOpenCart` (no open cart to edit), `CartItemNotPresent` (SKU not in cart) → 409 Conflict; non-positive quantity → 400 Bad Request (malformed input, not a state conflict).
5. **Removing the last line keeps the cart open.** No auto-close, no `CartAbandoned` — an empty open cart is a legitimate state the Customer can add to again. The `CartEmpty` guard in `PlaceOrder` (shipped defensively in 4.1) protects checkout; this PR adds the test that proves it.

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** § 6.1 slices 3.2 + 3.3 (lines ~283–305: both happy paths, `CartItemNotPresent`, non-positive-quantity rejection), § 5 slice table rows 3.2/3.3 (lines ~163–164 — note the deferred "refresh `CartActivityTimeout`" clauses), § 4 event vocabulary (`CartItemRemoved`, `CartItemQuantityChanged`).
2. **`docs/narratives/004-customer-purchase.md`** (v1.5) — Moment 1 (adding items) is what the new Moment continues; "Forthcoming Moments" names cart edits explicitly.
3. **`openspec/specs/shopping-cart/spec.md`** — the durable spec this change extends (2 requirements today: add-item, checkout).
4. **`src/CritterMart.Orders/Features/AddToCart.cs`** — the open-cart resolution + `FetchForWriting` + `AppendOne` shape both new endpoints mirror. **`Features/PlaceOrder.cs`** — the `CartEmpty` guard (lines ~48–57) this PR makes reachable; also the `Results.Problem` 409 idiom.
5. **`src/CritterMart.Orders/Cart/CartView.cs`** — the fold to extend (merge-by-SKU + two new Apply methods). **`Cart/CartItemAdded.cs`** — the "3.3 concern" comment to resolve. **`Cart/CartCreated.cs`, `Cart/CartCheckedOut.cs`, `Cart/ProductSnapshot.cs`** — read-only context.
6. **`tests/CritterMart.Orders.Tests/CartViewProjectionTests.cs`** (pure fold tests to extend), **`AddToCartTests.cs`** (the Alba integration shape + `ResetOrdersAsync` + `AddAsync` helper to mirror), **`PlaceOrderTests.cs`** (gains the CartEmpty case), **`OrdersAppFixture.cs`** (read-only — the shared collection fixture).
7. **ADRs 007 (Cart/Order aggregates), 008 (inline projections only).** `docs/rules/structural-constraints.md`.
8. **Stack reality**: `Directory.Packages.props` (Wolverine 6.1 / Marten 9.2 / .NET 10). No new packages, no broker involvement — both slices are pure Cart-stream, in-process HTTP.
9. **Skills**: `marten-projections-single-stream` (fold conventions), `wolverine-http-fundamentals` ([WolverineDelete], route params, ProblemDetails), `marten-aggregate-handler-workflow` (FetchForWriting), `wolverine-testing-integration-marten`. Use `find-docs` (ctx7 `/jasperfx/wolverine`) to **verify before wiring** (tasks.md group 1): (a) `[WolverineDelete]` signature with route-only parameters (no body) and any quirks vs `[WolverinePost]`; (b) whether a request body on a POST with route params binds the same way `AddToCart`'s does (expected yes — confirm, don't assume).

## Working pattern

Author on branch `feat/slices-3-2-3-3-cart-item-edits`: (1) this frozen prompt [review gate]; (2) OpenSpec change via the CLI artifact workflow (`openspec new change` → `status --json` → `instructions <artifact> --json` per artifact) + `validate --strict`; (3) implementation (events → fold incl. merge-by-SKU → endpoints → tests green, verify-before-wiring facts first); (4) narrative 004 → v1.6; (5) retro. One consolidated PR; the user merges. `openspec archive` is a post-merge `tidy:` step.

## Out of scope

- **No slice 3.4 (cart abandonment)** — no `CartActivityTimeout`, no `CartsAwaitingActivity*`, no `CartAbandoned`, no async `CartAbandonmentReport`. The slice-table "refresh `CartActivityTimeout`" clauses on 3.2/3.3 are explicitly deferred to 3.4 (this PR's design.md records the deferral).
- **No Order-side or Inventory-side edits** — `PlaceOrder` gains a test, not a code change; nothing crosses a BC boundary in these slices.
- **No cart auto-close on empty** — an empty open cart is legal (decision 5).
- **No `AddToCart` endpoint changes** — merge-by-SKU is a fold change only; the 3.1 endpoint contract is untouched.
- **No README/index refresh** and **no `openspec archive`** — post-merge `tidy: docs` concerns (no opportunistic edits).
- **No skill-file authoring** — if the session surfaces a third use of the new Marten patterns, note it in the retro for a future `tidy:` session (per the handoff's watch-out), don't author it here.
