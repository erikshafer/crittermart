# Prompt: Implementations 026 — Harden `AddToCart` against a malformed product snapshot

**Kind**: round-two **defect-hardening** slice — closes the round-one pre-frontend audit's only open *defect* (every other audit item is a missing feature). A malformed `AddToCart` (a `null` `productSnapshot`) currently surfaces as a `500` NRE deep in the shared `CartLines.Add` fold (`CartLine.cs:19`); it should be a `400` at the boundary, before any event is appended. **No new event, command, projection, or index — one `Validate` guard on an existing endpoint.**
**Source**: the carry-forward logged across recent retros (023/024/025 "Outstanding") — *"`AddToCart` null-snapshot 500 hardening (the precisely-diagnosed `productSnapshot`/`CartLine.cs:19` gap)."* The owner picked it from the post-#68 open-pick fork (the criticality option) via `AskUserQuestion`. **Cadence**: 1st implementation after the #68 design-return (the #66+#67 pair was the prior impl run; #68 was the interleave) — budget intact.
**Files touched**: this prompt; OpenSpec change **`harden-add-to-cart-snapshot`** (proposal / `shopping-cart` spec-delta / design / tasks — authored at session start, validated `--strict`); **`src/CritterMart.Orders/Features/AddToCart.cs`** (one `Validate(AddToCart)` method on `AddToCartEndpoint` + a `Microsoft.AspNetCore.Mvc` using; `Post` untouched); **`tests/CritterMart.Orders.Tests/AddToCartTests.cs`** (3 added negative tests); **`docs/narratives/004-customer-purchase.md`** (Moment 1 note → v1.9, authored at session start); `docs/{prompts,retrospectives}/README.md` (counts 25 → 26); `docs/retrospectives/implementations/026-harden-add-to-cart-snapshot.md` (forthcoming).
**Mode**: solo. No genuine sub-decision was forked — the guard idiom (`ProblemDetails` `Validate`) is settled by the `wolverine-http-fundamentals` skill + the in-repo `PublishProduct.ValidateAsync` precedent; the snapshot-usability extent (absent / blank name / negative price) is settled in design.md Decision 3.
**Commit subject**: `fix: AddToCart rejects a malformed product snapshot at the boundary (400, not 500)`

## Framing

`AddToCart` (`AddToCart.cs:11`) binds `ProductSnapshot` from the request body with no validation. System.Text.Json does not honor C#'s non-nullable-reference annotation, so a request omitting `productSnapshot` deserializes it to `null`. The handler then wraps that null into a `CartItemAdded` and **appends it** — and the failure surfaces only downstream, when the `CartView`/`Cart` fold runs `new CartLine(..., added.Snapshot.Name, added.Snapshot.Price)` and `added.Snapshot.Name` throws an NRE at `CartLine.cs:19`, returned as a `500`.

The altitude is wrong twice: a malformed *request* should be `400`, not `500`; and — the event-sourcing point — the malformed command was allowed to **become an event** before anything checked it. An appended event can't be un-appended, so the only correct place to stop it is the boundary. The cart never reads the Catalog (`ProductSnapshot.cs`), so the snapshot is a cart line's *only* source of product truth: a command with no usable snapshot has nothing from which to build a line and must be refused at the door.

## Goal

A `Validate(AddToCart)` guard on `AddToCartEndpoint` returns `400 ProblemDetails` when the command's snapshot is **absent**, has a **blank name**, or has a **negative price**, and `WolverineContinue.NoProblems` otherwise. Wolverine short-circuits before `Post`, so no `Cart` stream is started and no event appended on the malformed path. The five existing happy-path `AddToCartTests` stay green; three negative tests prove the `400` (the absent-snapshot one also asserting **no cart was created**). Full Orders suite green (`dotnet test`), `dotnet format` clean.

## Spec delta

The OpenSpec **`shopping-cart`** capability gains **1 ADDED requirement** — *"Reject an add-to-cart command with no usable product snapshot"* (3 scenarios: absent snapshot → `400` + no stream; blank name → `400`; negative price → `400`). **Narrative 004 → v1.9** records the Moment 1 robustness note (a malformed command never becomes cart history). The **workshop § 6.1 slice 3.1 faithfulness note** (the add now has a modeled malformed-input rejection) is a **fenced post-merge tidy**, paired with `openspec archive`. Four-step closure: **this prompt names it → the session executes → the retro confirms → Narrative 004 records it.**

## Orientation

1. **CLAUDE.md** — one-prompt-one-PR, no-opportunistic-edits, `{type}/{slug}` branch (`fix/addtocart-snapshot-guard`, created). [[feedback-consolidate-slice-prs]]: the OpenSpec change, the narrative bump, the guard, and the tests land in **one** PR.
2. **The skill that settles the idiom**: `wolverine-http-fundamentals` — decision table: "endpoint with validation guards → Wolverine.HTTP with `ProblemDetails` `Validate` method." A populated `ProblemDetails` short-circuits the handler; `400 / application/problem+json` is auto-added to OpenAPI; a synchronous `Validate(command)` is the canonical shape when the check needs no I/O.
3. **The exact in-repo template**: `src/CritterMart.Catalog/Features/PublishProduct.cs:25` — `ValidateAsync` returning `ProblemDetails` (status set) or `WolverineContinue.NoProblems`. This change mirrors it, synchronous (no session) because the check is pure command shape.
4. **The defect site**: `src/CritterMart.Orders/Shopping/CartLine.cs:19` (`new CartLine(..., added.Snapshot.Name, ...)` — the NRE) and `src/CritterMart.Orders/Shopping/ProductSnapshot.cs` (the snapshot is the cart's only product truth). The fix stops the bad event upstream; the fold is *correct* given a valid event and is **not** touched.
5. **The test surface**: `tests/CritterMart.Orders.Tests/AddToCartTests.cs` — the `_fixture.Host.Scenario` + `ResetOrdersAsync` pattern; the absent-snapshot test posts `new AddToCart("crit-001", 1, null!)` (serializes `productSnapshot: null`), asserts `400`, then queries `CartView` for the customer → empty.
6. **Skills**: `wolverine-http-fundamentals` (the guard shape — authoritative), `marten-integration-testing` + `wolverine-testing-alba` (the Alba negative tests + `DeleteAll*` reset). **Not** ctx7 — the in-repo `PublishProduct` guard is the authoritative pattern.

## Working pattern

**Guard first**: add `public static ProblemDetails Validate(AddToCart command)` to `AddToCartEndpoint` (the class with the `[WolverinePost("/carts/{customerId}/items")]`), three ordered checks (`null` snapshot → name blank → price `< 0`), each a `400 ProblemDetails`; `using Microsoft.AspNetCore.Mvc;`. `Post` and `CartEndpoint` untouched. → `AddToCartTests`: 3 added negative tests (absent → `400` + empty `CartView`; blank name → `400`; negative price → `400`). → `dotnet build` + `dotnet test tests/CritterMart.Orders.Tests` (full Orders suite) + `dotnet format`. OpenSpec + Narrative v1.9 authored at session start; README counts + retro 026 close it. One PR on `fix/addtocart-snapshot-guard`; **the owner merges**.

## Deliverable plan

- **`Features/AddToCart.cs`** — one `Validate(AddToCart)` on `AddToCartEndpoint`: `MissingProductSnapshot` / `MissingProductName` / `NegativeProductPrice`, all `400`, else `WolverineContinue.NoProblems`; `Microsoft.AspNetCore.Mvc` using. No edit to `Post`, `CartItemAdded`, `CartLine(s)`, `Cart`, `CartView`, or `Program.cs`.
- **`AddToCartTests.cs`** — `adding_an_item_without_a_product_snapshot_is_rejected_and_creates_no_cart` (`400` + empty `CartView` query), `adding_an_item_with_a_blank_product_name_is_rejected` (`400`), `adding_an_item_with_a_negative_price_is_rejected` (`400`). The five happy-path tests unchanged.
- **OpenSpec `harden-add-to-cart-snapshot`** — proposal / `shopping-cart` spec-delta (1 ADDED requirement, 3 scenarios) / design (Decision 1 `Validate`-not-inline; Decision 2 synchronous; Decision 3 snapshot-usability extent) / tasks. Validated `--strict`.
- **Docs** — Narrative 004 → v1.9 (Moment 1 note, done at session start); `docs/{prompts,retrospectives}/README.md` counts 25 → 26; retro 026.

## Out of scope

- **No null-guard on the fold** — `CartLines.Add` / `CartLine` are correct given a valid event; converting the `500` into a silently mis-built line would be worse. Stop the bad event at the boundary, not the read model.
- **No `Quantity`/`Sku` validation on `AddToCart`** — neither causes the NRE; adding them is beyond the named defect and would be opportunistic (design.md Non-Goals; logged as deferred-awareness in the retro).
- **No FluentValidation wiring** — the project has none today; a one-method synchronous guard does not warrant introducing it.
- **No harmonization of `ChangeCartItemQuantity`'s inline `Results.Problem` guard** onto the `Validate` shape — a separate, non-blocking tidy.
- **No workshop edit in this PR** — the § 6.1 slice 3.1 faithfulness note is a **fenced post-merge tidy**, paired with `openspec archive`. Named here so it is not silently dropped.
