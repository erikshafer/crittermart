# Proposal: Harden AddToCart — reject a malformed product snapshot at the boundary

## Why

`AddToCart` binds its `ProductSnapshot` straight from the request body (`AddToCart.cs:11`) with no validation. A request that omits `productSnapshot` deserializes it to `null`, the handler wraps it into a `CartItemAdded` event and appends it, and the failure surfaces only **downstream** in the shared `CartLines.Add` fold — `added.Snapshot.Name` throws a `NullReferenceException` at `CartLine.cs:19`, returned to the caller as a `500`. This was the round-one pre-frontend audit's only open *defect* (every other audit item was a missing feature, not a bug).

The altitude is wrong twice over. First, a malformed *request* should be a `400` (the caller sent bad input), not a `500` (the server failed). Second — and more important for an event-sourced system — the malformed command was allowed to **become an event** before anything checked it. An appended event can't be un-appended; the only correct place to stop a malformed command is the boundary, before it is written. The cart never reads the Catalog (`ProductSnapshot.cs`), so the snapshot is a cart line's *only* source of product truth — a command with no usable snapshot has nothing from which to build a line, and must be refused at the door.

## What Changes

- A `Validate(AddToCart)` guard is added to `AddToCartEndpoint` (Orders). It returns a `ProblemDetails` (`400`) when the command carries no usable product snapshot — the snapshot is **absent**, its **name is blank**, or its **price is negative** — and `WolverineContinue.NoProblems` otherwise. Wolverine runs it before `Post` and short-circuits on a populated `ProblemDetails`, so no `Cart` stream is started and no event is appended.
- The guard is the idiomatic Wolverine.HTTP shape (the `ProblemDetails` `Validate` method, per the `wolverine-http-fundamentals` skill), mirroring Catalog's existing `PublishProduct.ValidateAsync` (`PublishProduct.cs:25`). It is **synchronous** because a snapshot-shape check needs no I/O. The `Post` method's tuple return and scheduling logic are untouched.
- The `400 / application/problem+json` response is auto-reflected into the endpoint's OpenAPI metadata by Wolverine (no manual `[ProducesResponseType]`).

## Capabilities

### New Capabilities

*(none — the delta extends the existing `shopping-cart` capability, per the one-capability-per-aggregate shape)*

### Modified Capabilities

- `shopping-cart`: 1 ADDED requirement (*Reject an add-to-cart command with no usable product snapshot* — a malformed-input guard on the add path; no requirement text changes for existing behaviors).

## Impact

- **Code**: `src/CritterMart.Orders/Features/AddToCart.cs` (one new `Validate` method + a `Microsoft.AspNetCore.Mvc` using). No change to `CartItemAdded`, `CartLine`/`CartLines`, the `Cart` aggregate, `CartView`, or `Program.cs`.
- **Tests**: `tests/CritterMart.Orders.Tests/AddToCartTests.cs` (3 added — absent snapshot → `400` **and no cart created**, blank name → `400`, negative price → `400`). The five existing happy-path tests are unchanged and still green.
- **Docs**: `docs/narratives/004-customer-purchase.md` Moment 1 gains a short note that a malformed command is refused before it becomes cart history (the snapshot is the cart's only product truth); `## Document History` → v1.9.
- **No cross-BC impact**, no contract changes, no broker topology changes, no new packages. The guard stays inside Orders.
- **Out of band (post-merge tidy)**: `openspec archive harden-add-to-cart-snapshot` syncs the ADDED requirement into `openspec/specs/shopping-cart/spec.md`; an optional workshop § 6.1 slice 3.1 faithfulness note records the malformed-input guard (the workshop modeled only happy paths for the add).
- **Deliberately out of scope** (not the named defect; would be opportunistic): validating `Quantity > 0` and a non-blank `Sku` on `AddToCart`. Neither causes the NRE; both are recorded as a deferred-awareness item in the retro rather than fixed here.
