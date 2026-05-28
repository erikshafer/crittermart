---
retrospective: 002
kind: implementations
prompt: docs/prompts/implementations/002-slice-1-2-browse-products.md
deliverable: src/CritterMart.Catalog/Features/BrowseProducts.cs (new); tests/CritterMart.Catalog.Tests/BrowseProductsTests.cs (new); openspec/changes/slice-1-2-browse-products/{design.md, tasks.md} (new); docs/prompts/implementations/002-slice-1-2-browse-products.md (new); docs/retrospectives/implementations/002-slice-1-2-browse-products.md (this file)
date: 2026-05-28
mode: solo implementation with ctx7 docs verification
session-runner: Claude (Opus 4.7)
---

# Retrospective — Implementations 002: Slice 1.2 Browse Products (GET /products)

## Outcome summary

The **third and final edge of the slice 1.2 triangle** landed, completing the slice. The session added a single read-only endpoint — `GET /products` (`src/CritterMart.Catalog/Features/BrowseProducts.cs`) — that queries the `Product` document store and projects each document to `ProductCatalogView`, satisfying the `slice-1-2-browse-products` proposal's browse requirement. It also authored the change's deferred `design.md` (short) + `tasks.md`, completing it to a four-artifact change. Two tests cover the behavior (`tests/CritterMart.Catalog.Tests/BrowseProductsTests.cs`): the two-product browse scenario from Workshop 001 § 6.1, and an empty-catalog → empty-list case (validating `design.md` Decision 3). All four Catalog tests pass; `openspec validate slice-1-2-browse-products --strict` passes; and the upgraded service was run on Kestrel and served `GET /products` over real HTTP, returning `sku` (not the raw document `id`) — proving the projection.

There was **no skeleton work**: the Catalog service, Marten config, and `ProductCatalogView` already existed from slice 1.1. This was a purely additive query slice.

## What worked

- **The slice 1.1 skeleton investment amortized.** Because the service, the document store, and the `ProductCatalogView` record already existed, slice 1.2 was one endpoint plus tests — a genuinely small PR. This is the "blueprint architecture" payoff: build one slice by hand, and the next slice in the same BC is cheap.
- **ctx7 confirmed the v6 pattern before writing.** The Wolverine 6 docs showed exactly `[WolverineGet("/x")] public static Task<IReadOnlyList<T>> Get(IQuerySession s) => s.Query<T>().ToListAsync();`. Zero iteration, and it surfaced the alternative (`StreamMany<T>`) so the materialize-vs-stream decision was made knowingly, not by default.
- **Materialize-and-project honored the contract — and the real run proved it.** `StreamMany<Product>` would have streamed the raw document (with `Id`), but the proposal's response contract uses `sku`. Projecting to `ProductCatalogView` (which renames `Id` → `Sku`) was the right call, and the real-HTTP `GET /products` returned `"sku"` fields, confirming it. This closes slice 1.1 `design.md` Decision 1 ("`ProductCatalogView` is a query, not a projection") — the query was finally written, exactly as that decision specified.
- **Reused the test fixture and patterns.** `CatalogAppFixture` and the `Scenario` + data-clean patterns carried straight over; the only new wrinkle was reading the JSON response (`result.ReadAsJson<List<ProductCatalogView>>()`), which round-tripped cleanly through the app's STJ serializer.

## What was harder than expected

- **The `StreamMany` temptation.** The idiomatic, efficient Marten + Wolverine.Http path for "return many documents" is `StreamMany<T>`, which streams straight to JSON. It was the obvious reach — but it serializes the raw document shape. The general lesson (worth carrying to future read endpoints): **when the read model renames or reshapes relative to the stored document, raw-document streaming is off the table; you must materialize and project.** Recorded as `design.md` Decision 1.
- **The ADR 011 design-grain judgment: author-light, not skip.** Slice 1.1 exercised the "design.md earns a full treatment" end. Slice 1.2 was a candidate for the "skip design.md as trivial" end — but it was *not* truly zero-decision: the materialize-vs-stream choice genuinely affects the response contract. So this session authored a **short** design.md (closing the query-realization loop + recording the stream-vs-project and empty-list decisions) rather than skipping it. The grain spectrum now has two data points (1.1 full, 1.2 short); the pure-skip case still awaits a genuinely zero-decision slice.

## Methodology refinements that emerged

1. **Capability granularity is now satisfied by *code* for two requirements on one capability.** Both publish (1.1) and browse (1.2) are implemented against the single `product-catalog` capability. The one-capability-per-BC decision has held through proposal *and* implementation for a second requirement. Slice 1.3 (`ChangeProductPrice`) — which may genuinely MODIFY existing behavior rather than ADD — remains the last test before encoding it as a convention.
2. **An additive query slice is a small implementation PR.** Calibration point: after the skeleton exists, a read-only slice is ~one endpoint + tests. Implementation effort tracks slice shape, and the per-slice loop gets cheaper within a BC.
3. **"Raw-document streaming vs. materialize-and-project" is a reusable read-endpoint decision.** Stream when the wire shape *is* the document; project when the read model reshapes it. Future read slices (e.g., Orders' `OrderStatusView`) will face the same fork.

## Outstanding items / next-session inputs

1. **`openspec archive` is now doubly due.** Both `slice-1-1-publish-product` and `slice-1-2-browse-products` are complete-but-unarchived changes on `product-catalog`. Archiving both (1.1 then 1.2) would build `openspec/specs/product-catalog/spec.md` carrying publish + SKU-uniqueness + browse. Strongly recommend a dedicated `chore`/`tidy` step for this soon — it has been deferred across three sessions now.
2. **Slice 1.2 is complete.** Next options: **slice 1.3** (`ChangeProductPrice` — extends Narrative 001 to v1.1, adds a `ProductPriceChanged` event to the per-product audit stream, and is the capability-granularity MODIFIED test), or a **design-return / the infra bundle** (Aspire + OpenTelemetry + Static codegen).
3. **Design-return cadence is healthy.** This is the 2nd Catalog *implementation* PR overall (after slice 1.1), but it is the *first since the last design interleave* — PRs #9 (chore), #10 (narrative), #11 (proposal) all sat between the two implementation PRs. Consecutive-implementation count is effectively 1; the cadence is comfortably satisfied.
4. **Standing `tidy: docs` debt** (unchanged): the `docs/specs/` → `openspec/changes/` path drift in `docs/narratives/README.md`, and the `prompts/`/`retrospectives/` README *Current population* lines lagging the `narratives/`/`specs/`/`implementations/`/`chore/` kinds. Batch into one sweep.
5. **docker-compose volume accumulation.** Verification rows now include `crit-001`, `crit-002`, `crit-099`; `docker compose down -v` wipes them for a clean local DB.
6. **No new ADR or skill triggered.**

## Spec-delta — landed?

**Yes (forward-confirmed).** The prompt named a satisfaction delta:

1. The `product-catalog` browse requirement gains its satisfying implementation under `src/` — **landed**; proven by tests and a real run.
2. The `slice-1-2-browse-products` change gains `design.md` + `tasks.md`, completing it to four artifacts — **landed**; `openspec validate --strict` passes.
3. First exercise of ADR 011's light-design judgment — **landed** (author-short, not skip; see above).

No requirement or scenario added; the proposal is unchanged; Narrative 002 and Workshop 001 untouched.

## Process notes

- One PR bundles `feat:` (the endpoint + tests) and `docs:` (prompt, design, tasks, retro). No skeleton (it existed).
- **Prompt-first** this session; ctx7-verified the framework API before writing.
- Branch: `feat/slice-1-2-browse-products`.
