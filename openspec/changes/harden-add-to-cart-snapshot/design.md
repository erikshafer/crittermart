# Design: Harden AddToCart against a malformed product snapshot

## Context

This change closes the round-one pre-frontend audit's only open *defect*. `AddToCart` (`AddToCart.cs:11`) binds `ProductSnapshot` from the request body with no null check; a request omitting `productSnapshot` deserializes it to `null`, the handler appends a `CartItemAdded` carrying that null snapshot, and the failure surfaces only at the read-model fold — `new CartLine(..., added.Snapshot.Name, added.Snapshot.Price)` throws an NRE at `CartLine.cs:19`, returned as a `500`.

The fix is a validation boundary: reject the malformed command at the HTTP edge, before any event is written. This is not a new slice — it hardens slice 3.1 (`AddToCart`).

## Goals / Non-Goals

**Goals:**

- A malformed `AddToCart` returns `400` (bad request), not `500` (server error).
- No malformed command ever becomes an event: the guard short-circuits before the `Cart` stream is resolved or created.
- The snapshot is validated for *usability*, not mere presence — an absent snapshot, a blank name, or a negative price are all refused, because the cart line is built entirely from the snapshot.
- Match the repo's idiomatic Wolverine.HTTP guard shape (`ProblemDetails` `Validate`), keeping the `Post` handler unchanged.

**Non-Goals:**

- No change to the event (`CartItemAdded`), the shared fold (`CartLines`), the `Cart` aggregate, or `CartView`. The fold is *correct* given a valid event; the fix is to stop invalid events at the source, not to null-guard the fold (which would only convert a `500` into a silently mis-built line).
- No validation of `Quantity` or `Sku` on `AddToCart` — neither causes the NRE, and adding them is out of the named defect's scope (recorded as deferred-awareness in the retro).
- No FluentValidation wiring — the project has no FluentValidation today, and a one-method synchronous guard does not warrant introducing it.

## Decisions

### Decision 1 — A `ProblemDetails` `Validate` method, not an inline `Results.Problem`

The guard is a `public static ProblemDetails Validate(AddToCart command)` on `AddToCartEndpoint`, returning a populated `ProblemDetails` (status `400`) on a malformed snapshot or `WolverineContinue.NoProblems` to continue.

**Why over an inline `Results.Problem` at the top of `Post`:** the `wolverine-http-fundamentals` skill's decision table is explicit — "endpoint with validation guards → Wolverine.HTTP with `ProblemDetails` `Validate` method." Wolverine generates a short-circuit that never calls the handler, which is exactly the guarantee this fix needs (no event append on the malformed path), and the `400 / application/problem+json` is auto-added to OpenAPI. This also matches Catalog's existing `PublishProduct.ValidateAsync` (`PublishProduct.cs:25`). The Orders BC does have an inline-`Results.Problem` precedent (`ChangeCartItemQuantity.cs:22`), but that endpoint pre-dates this idiom and mixes input and state checks in one body; for a net-new guard the skill steers to the `Validate` method, and it keeps `AddToCart`'s tuple-returning `Post` (HTTP response + scheduled `CartActivityTimeout`) untouched.

### Decision 2 — Synchronous `Validate` (no `IDocumentSession`)

The check is a pure shape test on the command, so `Validate` is synchronous and takes only `AddToCart` — no session, unlike `PublishProduct.ValidateAsync` (which queries for a duplicate SKU). The skill's canonical example is exactly this synchronous form.

### Decision 3 — Validate the snapshot's *usability*, scoped to the snapshot

The guard rejects three unusable-snapshot cases: absent (`null`), blank name, negative price. Only the absent case caused the `500` (`decimal` can't be null, and a null `Name` would store rather than throw), but a guard that admitted `ProductSnapshot("", -5)` would be a boundary in name only — it would let a nonsensical cart line through. All three checks are the *same field* (the snapshot) in the *same method*, so this is a coherent "the snapshot must be usable" guard, not scope creep. `Quantity`/`Sku` are a different concern and are left alone (Non-Goals).

## Faithfulness notes (workshop divergences, for the post-merge tidy)

1. **A malformed-input guard was added to slice 3.1.** Workshop § 6.1 modeled only the add's happy paths (create, append, merge); it did not model a malformed-command rejection. The guard is recorded here as a faithfulness note for an optional § 6.1 amendment.

## Risks / Trade-offs

- **[The guard duplicates a shape the type system "should" express]** `ProductSnapshot` is a non-nullable positional record param, yet the wire can still deliver `null`. → Accepted: System.Text.Json deserialization does not honor C# nullable-reference annotations, so the boundary check is the real enforcement point. This is the canonical reason a validation boundary exists.
- **[Inconsistent guard idiom within Orders]** Orders now has both an inline `Results.Problem` guard (`ChangeCartItemQuantity`) and a `Validate`/`ProblemDetails` guard (this change). → Accepted as the better-of-two going forward; harmonizing `ChangeCartItemQuantity` onto the `Validate` shape is a separate, non-blocking tidy, not in this defect's scope.

## Open Questions

*(none — the scope was fixed by the named defect; the snapshot-usability extent was settled in Decision 3)*
