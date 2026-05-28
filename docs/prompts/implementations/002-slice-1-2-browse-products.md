# Prompt: Implementations 002 — Slice 1.2 Browse Products (GET /products)

**Kind**: per-slice implementation (query slice; no skeleton — the Catalog service already exists)
**Files touched**: `docs/prompts/implementations/002-slice-1-2-browse-products.md` (new, this file); `openspec/changes/slice-1-2-browse-products/design.md` + `tasks.md` (new); `src/CritterMart.Catalog/Features/BrowseProducts.cs` (new); `tests/CritterMart.Catalog.Tests/BrowseProductsTests.cs` (new); `docs/retrospectives/implementations/002-slice-1-2-browse-products.md` (forthcoming, session close)
**Mode**: solo implementation; current-docs verification via `ctx7` before writing framework code
**Commit subject(s)**: `feat: implement slice 1.2 browse products (GET /products)` + `docs: add slice 1.2 implementation prompt, design, tasks, retro` (one PR)

## Framing

This is the **third and final edge of the slice 1.2 triangle** (Narrative 002 → OpenSpec proposal → **implementation prompt**). Narrative 002 (PR #10) and the `slice-1-2-browse-products` proposal (PR #11) are merged; this session satisfies that proposal's single **browse** requirement.

Slice 1.2 is a **read-only query slice** — Workshop 001 § 5 marks it `*(query)*` with no command and no events; the proposal's spec has one scenario and **no failure path**. There is **no skeleton work**: the Catalog service, its Marten configuration, and the `ProductCatalogView` read shape already exist from slice 1.1. This session adds one thing — the `GET /products` endpoint — plus its test.

The central technical fact, inherited from slice 1.1's `design.md` Decision 1: **`ProductCatalogView` is a query over `Product` documents, not a Marten `IProjection`.** This session is where that query is finally written. The endpoint reads the `Product` document store and projects each document to `ProductCatalogView` (which renames the document's `Id` to `Sku` — so the response honors the proposal's `sku` field rather than leaking the raw document `Id`).

The stack is now **Wolverine 6.1.0 / Marten 9.2.0** (PR #9). Use v6/v9 APIs; verify specifics with `ctx7` before writing.

## Goal

A `GET /products` Wolverine.Http endpoint that returns every published product as a `ProductCatalogView` (SKU, name, description, current price), proven by an Alba + Testcontainers test over the proposal's two-product browse scenario. `openspec validate slice-1-2-browse-products --strict` still passes, and the upgraded service is verified to actually serve the listing over real HTTP.

## Spec delta

This session closes the slice 1.2 triangle: the `product-catalog` capability's browse requirement gains its satisfying implementation under `src/`, and the `slice-1-2-browse-products` openspec change gains its deferred `design.md` + `tasks.md` (completing it to a four-artifact change). No requirement or scenario is added — the proposal is unchanged; this session *satisfies* it. New ADR cross-reference: this is the first slice to exercise ADR 011's "design.md is light for a near-trivial slice" judgment from the author side (slice 1.1 exercised the "design.md earns a full treatment" side).

## Orientation

Read in this order:

1. **`openspec/changes/slice-1-2-browse-products/proposal.md`** + **`specs/product-catalog/spec.md`** — the contract: one browse requirement, one scenario (two products, `GET /products`, both returned with SKU/name/description/price), no failure path.
2. **`docs/narratives/002-customer-browse-catalog.md`** — the human sibling; Moment 1 is the scenario; the non-events section bounds scope (no live stock, no recommendations, no real-time price).
3. **`src/CritterMart.Catalog/Products/Product.cs`** — the `Product` document and the existing `ProductCatalogView` record (`FromDocument` maps `Id`→`Sku`). The endpoint reuses this; do not redefine it.
4. **`src/CritterMart.Catalog/Features/PublishProduct.cs`** — the endpoint pattern to mirror (`[Wolverine*]` attribute, method-injected Marten session, static handler).
5. **`tests/CritterMart.Catalog.Tests/`** — `CatalogAppFixture` (Alba + Testcontainers, shared collection) and `PublishProductTests` (the Scenario + data-clean pattern to reuse).
6. **`openspec/changes/slice-1-1-publish-product/design.md`** Decision 1 — `ProductCatalogView` is a query, not a projection. This session realizes that decision.

## Working pattern

1. **Author `design.md`** (short) via `openspec instructions design`. It closes the loop slice 1.1 opened: record *how* the query is realized — `IQuerySession.Query<Product>()`, materialize, project to `ProductCatalogView` (so `Sku` is surfaced, not the raw document `Id`); **materialize-and-project, not raw-document JSON streaming** (streaming `Product` would leak `Id` and break the `sku` contract); empty catalog returns an empty list (not a 404). Keep it light and reference ADRs rather than restating — this is the near-trivial end of the ADR 011 grain spectrum.
2. **Author `tasks.md`** via `openspec instructions tasks` — the live checklist.
3. **Verify current API** with `ctx7` for Wolverine 6.1 (`[WolverineGet]`, returning a collection as JSON) and Marten 9.2 (`IQuerySession.Query<T>().ToListAsync()`), before writing.
4. **Implement** `src/CritterMart.Catalog/Features/BrowseProducts.cs`: a static `[WolverineGet("/products")]` endpoint taking an injected query session, querying `Product`, projecting to `ProductCatalogView`, returning the list.
5. **Test** `tests/CritterMart.Catalog.Tests/BrowseProductsTests.cs` (reuse `CatalogAppFixture`, `[Collection("catalog")]`): publish `crit-001` and `crit-002` (via the existing `PublishProduct` endpoint or by seeding), `GET /products`, assert both returned with SKU/name/description/price. Quote-identical anchor data: `crit-001` "Cosmic Critter Plush" `24.99`, `crit-002` "Nebula Newt" `18.00`.
6. **Verify**: `dotnet build` + `dotnet test` green; **run the service** against docker-compose and `GET /products` over real HTTP; `openspec validate slice-1-2-browse-products --strict` passes.
7. **Author the retrospective**: spec-delta closure; the ADR 011 light-design judgment; whether one capability still holds (now satisfied by code for both publish and browse).

## Deliverable plan

| Deliverable | Path | Notes |
| --- | --- | --- |
| This prompt | `docs/prompts/implementations/002-slice-1-2-browse-products.md` | Frozen at session start |
| Design doc | `openspec/changes/slice-1-2-browse-products/design.md` | Short; closes the query-realization loop |
| Task checklist | `openspec/changes/slice-1-2-browse-products/tasks.md` | Live checkbox list |
| Browse endpoint | `src/CritterMart.Catalog/Features/BrowseProducts.cs` | `GET /products` → `ProductCatalogView[]` |
| Test | `tests/CritterMart.Catalog.Tests/BrowseProductsTests.cs` | Two-product browse scenario |
| Retrospective | `docs/retrospectives/implementations/002-slice-1-2-browse-products.md` | Session close |

## Out of scope

- **No new domain types.** `Product` and `ProductCatalogView` already exist; reuse them. Do not turn `ProductCatalogView` into a Marten projection.
- **No changes to slice 1.1 code** beyond what's strictly needed (ideally none — the browse endpoint is additive).
- **No detail endpoint** (`GET /products/{sku}`) — the proposal/workshop scenario is the list only; a single-product view is not in this slice's contract.
- **No paging, filtering, sorting, or search** — round-one browse is the flat published listing (Narrative 002). Don't add query parameters the contract doesn't require.
- **No live stock, recommendations, or real-time price** — Narrative 002's named non-events; out of scope and out of round one.
- **No `openspec archive`** — archiving slices 1.1 and 1.2 into `openspec/specs/product-catalog/` is a separate deferred step.
- **No `tidy: docs` items** (the `docs/specs/` path drift, README population/count lines) — not named here; they remain a separate sweep.
- **No Aspire / OpenTelemetry / RabbitMQ** — still deferred from the slice 1.1 scope decisions.
