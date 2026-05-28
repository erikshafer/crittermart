# Prompt: Implementations 003 — Slice 1.3 Change Product Price (consolidated one-PR slice)

**Kind**: per-slice, **consolidated** (narrative extension + OpenSpec proposal + implementation, all in one PR)
**Files touched**: `docs/prompts/implementations/003-slice-1-3-change-price.md` (new, this file); `docs/narratives/001-seller-manage-catalog.md` (edit → v1.1, Moment 3); `openspec/changes/slice-1-3-change-price/{.openspec.yaml, proposal.md, specs/product-catalog/spec.md, design.md, tasks.md}` (new); `src/CritterMart.Catalog/Products/ProductPriceChanged.cs` (new); `src/CritterMart.Catalog/Features/ChangeProductPrice.cs` (new); `tests/CritterMart.Catalog.Tests/ChangeProductPriceTests.cs` (new); `docs/retrospectives/implementations/003-slice-1-3-change-price.md` (forthcoming, session close)
**Mode**: solo, consolidated slice; current-docs verification via `ctx7` before framework code
**Commit subject(s)**: `docs: slice 1.3 narrative v1.1 + change-price proposal/design` + `feat: implement slice 1.3 change product price` (one PR)

## Framing

This is the **first slice authored under the one-PR mode** (memory `feedback-consolidate-slice-prs`): the whole vertical slice — narrative, OpenSpec proposal, and implementation — ships in **one PR**, not the three slices 1.1/1.2 used. Per the user's decision, this divergence from CLAUDE.md's one-prompt-one-session-one-PR discipline and ADR 011's session split is **kept informal** — no ADR, no rule-file change — and is **recorded in this slice's retrospective** (non-silent). It may be revisited if the mode sticks.

Slice 1.3 (Change a product's price) is the Seller's third catalog operation. It extends **Narrative 001** (the Seller's journey, which already forward-looked Moment 3) to v1.1 — it is **not** a new narrative. The `Product` document stays the source of truth; `ProductPriceChanged` is appended as the **second** event on the per-product audit stream (after `ProductPublished` from slice 1.1), carrying the old and new price — the workshop's "even CRUD wants events for audit" point, now exercised with a genuinely growing stream.

This is also the **last capability-granularity test** flagged by the slice 1.1/1.2 retros: adding a price-change operation to `product-catalog`. It is almost certainly another `## ADDED Requirements` (a new operation), **not** `MODIFIED` (it does not change publish's or browse's stated behavior — browse already returns "current price", so it reflects the new price with no spec edit). If it ADDs cleanly, the one-capability-per-bounded-context convention is confirmed across three operations.

Stack is **Wolverine 6.1.0 / Marten 9.2.0** — verify v6/v9 API via `ctx7` before writing.

## Goal

A `ChangeProductPrice` operation that updates a published product's price and records a `ProductPriceChanged` audit event (old + new price) on the product's stream, with the catalog listing reflecting the new price — proven by Alba + Testcontainers tests and a real run. Narrative 001 is at v1.1; the `slice-1-3-change-price` openspec change is complete (four artifacts) and passes `--strict`.

## Spec delta

Narrative 001 gains Moment 3 and bumps to v1.1 (`slices: [1.1, 1.3]`). The `product-catalog` capability gains a **Change a product's price** requirement (its third, after publish + SKU-uniqueness + browse) via a new `slice-1-3-change-price` change. Code satisfies it: `ChangeProductPrice` → `ProductPriceChanged`. Confirms (or refutes) the one-capability-per-BC convention. The archived main spec (`openspec/specs/product-catalog/spec.md`) is **not** edited here — archiving slice 1.3 is a later step.

## Orientation

1. **Memory `feedback-consolidate-slice-prs`** — the one-PR mode and what it overrides.
2. **`docs/workshops/001-crittermart-event-model.md`** § 5 (slice 1.3 row), § 6.1 (1.3 GWT — **happy path only**: `crit-001` `24.99` → `19.99`, `ProductPriceChanged` with old+new, view reflects `19.99`), § 4 (`ProductPriceChanged` vocabulary).
3. **`docs/narratives/001-seller-manage-catalog.md`** — the existing "Forthcoming Moments → Moment 3" forward-look (the content to author) and the price-snapshot non-event (already there; keep it). Extend, do not rewrite.
4. **`openspec/specs/product-catalog/spec.md`** — the live main spec the 1.3 delta ADDs to (publish, uniqueness, browse).
5. **`src/CritterMart.Catalog/Features/PublishProduct.cs`** + **`Products/Product.cs`** + **`Products/ProductPublished.cs`** — the command/endpoint/event/document templates. Reuse `Product`, `SellerIdentity`, the `StartStream`/`Store` + transactional pattern (here it's `Events.Append` to an existing stream + document update).
6. **openspec CLI instructions** (`openspec instructions proposal|specs|design|tasks --change slice-1-3-change-price`).

## Working pattern

1. Author this prompt (done).
2. **Narrative 001 → v1.1:** author Moment 3 (Adjusting a product's price), set `slices: [1.1, 1.3]`, bump version, append Document History.
3. **OpenSpec proposal:** `openspec new change slice-1-3-change-price`; author `proposal.md` (`product-catalog` Modified Capability) + `specs/product-catalog/spec.md` (`## ADDED Requirements`: Change a product's price, with the § 6.1 happy-path scenario carrying old+new price). Quote-identical anchor: `crit-001` `24.99` → `19.99`.
4. **design.md + tasks.md** (ADR 011 grain). Decisions to record: load the `Product` by SKU (prefer Wolverine `[Entity]` for auto-`404` on unknown SKU; else explicit `LoadAsync` + `ProblemDetails`); append `ProductPriceChanged(sku, oldPrice, newPrice, changedBy, at)` to the SKU-keyed stream and update `Product.Price` in one transaction; endpoint route/verb.
5. **Verify API** with `ctx7` (Wolverine 6.1 `[Entity]`/route binding/verb attributes; Marten 9.2 `Events.Append` to an existing stream).
6. **Implement:** `Products/ProductPriceChanged.cs` (event) + `Features/ChangeProductPrice.cs` (command + endpoint).
7. **Test:** `ChangeProductPriceTests.cs` (reuse `CatalogAppFixture`): happy path (publish `crit-001` @ `24.99`, change to `19.99` → one `ProductPriceChanged` with old `24.99`/new `19.99` appended after `ProductPublished`; document price `19.99`; `GET /products` reflects `19.99`) **and** not-found (change price for an unknown SKU → `404`).
8. **Verify:** `dotnet build` + `dotnet test` green; real docker-compose run (change a price, then `GET /products`); `openspec validate slice-1-3-change-price --strict`.
9. **Retro:** spec-delta closure; the one-PR-mode divergence (kept informal); the capability-granularity result; the workshop-is-happy-only note (the not-found guard is a defensive code+test addition, not a spec scenario).

## Out of scope

- **No new narrative** — extend Narrative 001 (Seller's journey); slice 1.3 is not the Customer's.
- **No new capability** — ADD to `product-catalog`.
- **No `openspec archive`** for slice 1.3 — a later step.
- **No spec scenario for the not-found path** unless you also add it to the narrative and keep all three (workshop/narrative/proposal) aligned — the workshop's 1.3 GWT is happy-only; the not-found `404` is a **defensive code + test** addition (cleanly free via `[Entity]`), recorded in the retro rather than back-filled into the workshop. Do not silently amend Workshop 001.
- **No change to slice 1.1/1.2 code** beyond what's strictly required (browse needs none — it already returns current price).
- **No infra** (Aspire/OTel/Static codegen), **no `tidy: docs`** (README population lines, `docs/specs/` path drift) — separate, still-deferred work.
- **No formalizing the one-PR mode** (ADR/rule update) — the user chose to keep it informal; just note it in the retro.
