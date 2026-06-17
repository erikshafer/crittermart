---
retrospective: 026
kind: implementations
prompt: docs/prompts/implementations/026-harden-add-to-cart-snapshot.md
deliverable: openspec/changes/harden-add-to-cart-snapshot/ (proposal + shopping-cart spec delta + design + tasks), src/CritterMart.Orders/Features/AddToCart.cs (+Validate guard), tests/CritterMart.Orders.Tests/AddToCartTests.cs (+3 negative tests), docs/narratives/004-customer-purchase.md (v1.9)
date: 2026-06-17
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 026: Harden `AddToCart` against a malformed product snapshot

## Outcome summary

Closed the round-one pre-frontend audit's only open *defect*: a malformed `AddToCart` (a `null` `productSnapshot`) that previously surfaced as a `500` NRE deep in the shared `CartLines.Add` fold (`CartLine.cs:19`) is now refused at the HTTP boundary with a `400`, before any event is appended. The fix is a single validation-boundary guard — the smallest possible surface for a real correctness fix.

- **`Features/AddToCart.cs`** — added `public static ProblemDetails Validate(AddToCart command)` to `AddToCartEndpoint`: `400` when the snapshot is absent (`MissingProductSnapshot`), its name is blank (`MissingProductName`), or its price is negative (`NegativeProductPrice`); `WolverineContinue.NoProblems` otherwise. Wolverine runs it before `Post` and short-circuits on a populated `ProblemDetails`, so the malformed command never starts a `Cart` stream and never appends a `CartItemAdded`. Synchronous (no `IDocumentSession`) because the check is pure command shape. `Post`, `CartEndpoint`, `CartItemAdded`, `CartLine(s)`, the `Cart` aggregate, `CartView`, and `Program.cs` are all **untouched**.
- **`AddToCartTests.cs`** — 3 added Alba negative tests: absent snapshot (`new AddToCart("crit-001", 1, null!)`) → `400` **and** a `CartView` query for the customer is empty (proving no stream was created, no event appended); blank name → `400`; negative price → `400`. The five existing happy-path tests are unchanged.

**Tests**: full Orders suite green — **80 tests, 0 failures** (+3 from the new negative cases). `dotnet format` clean. The host started without a codegen error, confirming Wolverine discovered the synchronous `Validate(AddToCart)` as a before-middleware on `AddToCartEndpoint`; the three `400` assertions confirm the `ProblemDetails` short-circuit produced a real HTTP `400`; `carts.ShouldBeEmpty()` confirms the no-event guarantee.

**Spec movement**: OpenSpec **`shopping-cart`** gained **1 ADDED requirement** (*Reject an add-to-cart command with no usable product snapshot*, 3 scenarios; validated `--strict`). **Narrative 004 → v1.9** records the Moment 1 robustness note (a malformed command never becomes cart history). The workshop **§ 6.1 slice 3.1 faithfulness note is a fenced post-merge tidy**, paired with `openspec archive`.

## What worked

- **The skill + the in-repo precedent agreed, so the idiom was settled before any code.** `wolverine-http-fundamentals`'s decision table ("validation guard → `ProblemDetails` `Validate` method") and Catalog's existing `PublishProduct.ValidateAsync` pointed at the same shape. The guard is the synchronous twin of `PublishProduct`'s — no session, because the check is pure command shape — so there was no design search, only application.
- **The `Validate` short-circuit gave the exact event-sourcing guarantee for free.** The whole point of a boundary guard in an event-sourced system is that no malformed command becomes an event (an appended event can't be un-appended). Because Wolverine generates a short-circuit that never calls `Post`, the `carts.ShouldBeEmpty()` assertion passed without any defensive code in the handler — the guarantee is structural, not hand-rolled.
- **Validating the snapshot's *usability*, not just its presence, kept the boundary honest.** Only the `null` case caused the `500` (a `decimal` can't be null; a null `Name` would store rather than throw), but a guard that admitted `ProductSnapshot("", -5)` would be a boundary in name only. Folding blank-name and negative-price into the same method — same field, same guard — closed the whole "unusable snapshot" class without expanding scope to other command fields.
- **Tight scope made the consolidated PR trivially coherent.** One method, three tests, four doc artifacts — the OpenSpec change, the narrative note, the prompt, and the retro all describe the same one-method change. No churn to existing consumers (the five happy-path tests never moved).

## What was harder / notable

- **System.Text.Json silently defeats the type system at the wire.** `ProductSnapshot` is a non-nullable positional record parameter, so the C# type *says* it can't be null — yet the deserializer happily binds a missing JSON field to `null`, because STJ does not honor nullable-reference annotations. This is the canonical reason a validation boundary exists, and it is worth stating plainly in design.md so the guard doesn't look redundant with the type declaration.
- **Choosing ADDED over MODIFIED for the spec delta avoided transcription risk.** Folding the rejection into the existing "Add an item to the cart" requirement (MODIFIED) would have meant restating its 13-line body and four scenarios verbatim — an invitation to drift. A standalone ADDED requirement (a malformed-*input* guard, distinct from the cart's domain-state rejections) reads cleanly and validated `--strict` without touching the happy-path scenarios.

## Methodology refinements

- **A skill-settled idiom is a no-fork, same as an ADR- or convention-settled one (retros 024/025).** This is the third consecutive slice where checking the authority *before* drafting an `AskUserQuestion` dissolved an apparent decision: 024/025 were settled by in-repo convention, 026 by the `wolverine-http-fundamentals` skill's decision table plus the `PublishProduct` precedent. The owner forked at the genuine fork (the post-#68 open pick); the sub-decisions inside the chosen slice were authority-bound. The heuristic holds: *surface a fork only after confirming no ADR, named convention, worked precedent, or skill directive already settles it.*
- **Name the deliberately-unfixed adjacent guard, don't fix it.** `Quantity > 0` and a non-blank `Sku` are real un-validated inputs on `AddToCart`, but neither causes the named NRE, so fixing them here would be opportunistic scope creep. Recording them as deferred-awareness (below) keeps the no-opportunistic-edits discipline while not pretending the boundary is complete.

## Outstanding / next-session inputs

- **Workshop § 6.1 slice 3.1 faithfulness note — fenced post-merge tidy.** The workshop modeled only the add's happy paths; this slice added a malformed-input rejection. The note should record it, paired with `openspec archive harden-add-to-cart-snapshot` (which syncs the ADDED requirement into `openspec/specs/shopping-cart/spec.md`).
- **Deferred-awareness (logged, not fixed — no opportunistic edits):** (1) `AddToCart` still does not validate `Quantity > 0` or a non-blank `Sku` — neither causes the NRE, but a complete command boundary would cover them; a `tidy:` or a follow-up hardening candidate. (2) `ChangeCartItemQuantity.cs:22` uses an inline `Results.Problem` guard rather than the `Validate`/`ProblemDetails` idiom this slice and `PublishProduct` use — harmonizing it is a separate, non-blocking tidy. (3) The stale `client/src/orders/orderSchema.ts` `Order/` → `Ordering/` path-reference nit (logged in retro 025) — note: retro 025 also lists an Orders path-ref sweep as part of #68; confirm whether it remains before re-logging.
- **Cadence**: this was the **1st implementation** after the #68 design-return. Two more implementations may run before the next **design-return interleave** is due. Since all four round-one BCs are workshopped, that interleave will be a narrative, a `tidy:` (e.g., the § 6.1 flip above, or the `ChangeCartItemQuantity` guard harmonization), not a new BC workshop.
- **Carry-forwards (unchanged, non-blocking):** the **"My Orders" list** (Gap #3); the cart identity-transport harmonization tidy; the **OTel / in-browser visual pass** (still owed — this session ran the Orders test suite, not a live browser boot); no frontend CI job; focus-ring enhancement; Docker container grouping; **CritterWatch trial expires 2026-07-10**.

## Spec-delta — landed?

**Named delta landed.** The prompt named: OpenSpec `shopping-cart` **+1 ADDED requirement** (*Reject an add-to-cart command with no usable product snapshot*, 3 scenarios) and **Narrative 004 → v1.9** recording the Moment 1 robustness note, with the workshop § 6.1 faithfulness note fenced post-merge. That landed: the change validates `--strict` with the new requirement and its three scenarios; Narrative 004 is v1.9 with the Moment 1 "a cart line can't be built from nothing" note and a Document History row. Four-step closure: **prompt named → session executed → this retro confirms → Narrative 004 recorded.** The single deferred edge — the workshop § 6.1 flip — is named above as a post-merge tidy, so the spec record is *closing*, with one honestly-flagged tidy still owed.
