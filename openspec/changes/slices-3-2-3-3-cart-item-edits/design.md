# Design: Slices 3.2 + 3.3 — Cart Item Edits

## Context

The Cart aggregate (slice 3.1) is an event-sourced stream keyed by a generated `cartId`, projected inline into `CartView`, with the customer's single open cart resolved by querying `CartView` on `customerId` behind a partial-unique index. Slices 4.1–4.7 completed the Order lifecycle; the Cart's own edit operations (remove, change quantity) are the last simple Cart-stream slices before 3.4 (abandonment) closes the Orders BC. Slice 3.1 deliberately deferred the cart-line identity question: today the `CartView` fold appends one line per `CartItemAdded`, even for the same SKU (the `CartItemAdded.cs` comment names this "a 3.3 concern").

## Goals / Non-Goals

**Goals:**

- The Customer can remove an item from, and change an item's quantity in, their open cart.
- Cart lines acquire a stable identity (SKU-keyed), making the edit commands unambiguous.
- The `CartEmpty` guard in `PlaceOrder` (shipped defensively in 4.1) becomes reachable and proven.

**Non-Goals:**

- No `CartActivityTimeout` scheduling/refresh (deferred to 3.4, same deferral 3.1 used — Workshop § 8 item 1).
- No cart auto-close on empty; no `CartAbandoned`.
- No changes to the `AddToCart` endpoint contract, the Order side, Inventory, or anything cross-BC.

## Decisions

### Decision 1 — Cart lines are SKU-keyed; the fold merges same-SKU adds

`Apply(CartItemAdded)` now checks for an existing line with the event's SKU: present → that line's quantity increments by the event's quantity; absent → a new line is appended. The first add's snapshotted name/price stays authoritative for the line (consistent with the snapshot-is-authoritative-until-checkout rule). The event stream is untouched — every add is still recorded; only the view merges.

**Alternatives considered:** (a) reject duplicate-SKU adds at the endpoint — changes 3.1's shipped contract retroactively and is worse storefront UX; (b) keep duplicate lines and make `CartItemRemoved` remove all lines for a SKU — surprising semantics ("remove one item" deletes two lines) and leaves `CartItemQuantityChanged` ambiguous. Merge-by-SKU is what the Workshop's SKU-addressed GWTs implicitly assume, and what the 3.1 code comment anticipated.

**Resolved with the user** (collaborative fork, session 012).

### Decision 2 — Guards read the projected `CartView`, not the raw stream

Both new endpoints resolve the open cart by `customerId` (the same indexed query `AddToCart` and `PlaceOrder` use) and validate against `CartView.Lines` — item presence for both commands, positivity for quantity. The inline projection is transactionally consistent with the stream (it folds in the same commit), so a view-based guard is exactly as strong as a stream-based one here, and cheaper to express.

### Decision 3 — Routes follow codebase precedent; first `[WolverineDelete]`

`POST /carts/{customerId}/items/{sku}/quantity` mirrors Catalog's `POST /products/{sku}/price` (slice 1.3) — command-shaped POST to a sub-resource. `DELETE /carts/{customerId}/items/{sku}` introduces the project's first DELETE: removal is the one cart edit where the REST verb and the command's intent coincide exactly. Both routes key on `customerId`, not `cartId` — the Customer edits *their* cart; which stream that is, is the server's business.

### Decision 4 — Failure-path status codes

`NoOpenCart` and `CartItemNotPresent` → 409 Conflict via `Results.Problem` (state conflicts, the established idiom from `PlaceOrder`); non-positive quantity → 400 Bad Request (malformed input, not a state conflict).

### Decision 5 — Removing the last line keeps the cart open

An empty open cart is a legitimate state the Customer can add to again. No auto-close event is appended. The consequence — placing an order from an empty cart must fail — is guarded by `PlaceOrder`'s existing `CartEmpty` 409, which this change covers with a test for the first time.

## Faithfulness notes (Workshop § 6.1 divergences)

1. **Merge-by-SKU is implied, not stated.** The Workshop's 3.2/3.3 GWTs address lines by SKU but never say what two adds of the same SKU produce. This change resolves that gap in the view's favor (one line per SKU); the Workshop slice table needs no amendment, but the post-merge tidy should note the resolution in § 6.1's slice-3.1 amendment trail.
2. **`CartItemNotPresent` on quantity change is an extension.** The Workshop's 3.3 failure path covers only non-positive quantity; rejecting a change for an absent SKU mirrors 3.2's `CartItemNotPresent` guard. Obvious, but not in the Workshop text.
3. **The "refresh `CartActivityTimeout`" writes-to clauses on slice-table rows 3.2/3.3 are deferred to 3.4** — same deferral slice 3.1 recorded (Workshop § 8 item 1, v1.1 amendment).

## Risks / Trade-offs

- **[Fold change applies to already-folded views]** Inline projections fold at append time; existing `CartView` documents were folded with the one-line-per-add logic. → No production data exists (teaching project), and no shipped test pins same-SKU duplicate lines (the 3.1 tests use distinct SKUs). A rebuild is not needed.
- **[Merged lines keep the first snapshot price]** If the same SKU is added twice while the Seller changes the price between adds, the second add's snapshot is ignored. → Consistent with the snapshot-is-authoritative rule; the alternative (last-write-wins) would let a price change sneak into a line the Customer already reviewed.
- **[DELETE with no request body]** First use of `[WolverineDelete]`; route-only parameter binding to be verified against current Wolverine.Http docs before wiring (tasks group 1).

## Migration Plan

Pure addition plus a view-fold refinement; no schema migration, no data migration, no rollback concern beyond reverting the PR.

## Open Questions

(none — both collaborative forks were resolved at session start; remaining unknowns are the two verify-before-wiring facts in tasks group 1)
