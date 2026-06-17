# Tasks: Harden AddToCart against a malformed product snapshot

## 1. Verify before wiring (skill, numbered facts)

- [x] 1.1 `wolverine-http-fundamentals` skill — the idiomatic guard for an HTTP endpoint is a `Validate`/`ValidateAsync` method returning `ProblemDetails`; a populated `ProblemDetails` (status `400`) short-circuits the handler, and the `400 / application/problem+json` is auto-added to OpenAPI. Synchronous `Validate(command)` is the canonical shape when the check needs no I/O. Confirmed against the in-repo exemplar `PublishProduct.ValidateAsync` (`PublishProduct.cs:25`).

## 2. The guard

- [x] 2.1 `Features/AddToCart.cs` — add `public static ProblemDetails Validate(AddToCart command)` to `AddToCartEndpoint`: `400` when `ProductSnapshot is null` (`MissingProductSnapshot`), name blank (`MissingProductName`), or price `< 0` (`NegativeProductPrice`); else `WolverineContinue.NoProblems`. Add the `Microsoft.AspNetCore.Mvc` using. `Post` untouched.

## 3. Integration proof

- [x] 3.1 `tests/CritterMart.Orders.Tests/AddToCartTests.cs` — 3 added Alba tests:
  - absent snapshot (`new AddToCart("crit-001", 1, null!)`) → `400` **and** `CartView` query for the customer is empty (no stream created, no event appended);
  - blank snapshot name → `400`;
  - negative snapshot price → `400`.
- [x] 3.2 Full Orders suite green (`dotnet test` — 80 passed); the five existing happy-path `AddToCartTests` unchanged.

## 4. Sibling artifacts

- [x] 4.1 `docs/narratives/004-customer-purchase.md` — Moment 1 note (a command with no usable snapshot is refused before it becomes cart history; the snapshot is the cart's only product truth) + `## Document History` v1.9 row.
- [x] 4.2 `docs/prompts/implementations/026-harden-add-to-cart-snapshot.md` + `docs/retrospectives/implementations/026-harden-add-to-cart-snapshot.md` — outcome, refinements, spec-delta confirmation, deferred-awareness (`Quantity`/`Sku` validation; `ChangeCartItemQuantity` guard harmonization).
- [x] 4.3 `openspec validate harden-add-to-cart-snapshot --strict` green; consolidated PR opened.

## 5. Deferred (out of this change)

- [x] 5.1 `openspec archive harden-add-to-cart-snapshot` (post-merge tidy — syncs the ADDED requirement into `openspec/specs/shopping-cart/spec.md`).
- [x] 5.2 Optional workshop § 6.1 slice 3.1 faithfulness note (malformed-input guard added beyond the modeled happy paths).
