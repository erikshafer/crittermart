---
retrospective: 003
kind: implementations
prompt: docs/prompts/implementations/003-slice-1-3-change-price.md
deliverable: docs/narratives/001-seller-manage-catalog.md (edit → v1.1, Moment 3); openspec/changes/slice-1-3-change-price/{proposal.md, specs/product-catalog/spec.md, design.md, tasks.md} (new); src/CritterMart.Catalog/Products/ProductPriceChanged.cs (new); src/CritterMart.Catalog/Features/ChangeProductPrice.cs (new); tests/CritterMart.Catalog.Tests/ChangeProductPriceTests.cs (new); docs/prompts/implementations/003-slice-1-3-change-price.md (new); docs/retrospectives/implementations/003-slice-1-3-change-price.md (this file)
date: 2026-05-28
mode: solo, consolidated one-PR slice; ctx7 docs verification
session-runner: Claude (Opus 4.7)
---

# Retrospective — Implementations 003: Slice 1.3 Change Product Price

## Outcome summary

Slice 1.3 (`ChangeProductPrice`) shipped as CritterMart's **first consolidated one-PR slice** — narrative extension + OpenSpec proposal + implementation in a single PR, per the user's move-faster change (memory `feedback-consolidate-slice-prs`). The deliverables: Narrative 001 extended to **v1.1** (Moment 3 — adjusting a price); the `slice-1-3-change-price` openspec change (proposal + spec + design + tasks, passing `--strict`); a `ProductPriceChanged` audit event; a `POST /products/{sku}/price` endpoint that loads the product, updates its price, and appends `ProductPriceChanged` (old + new) to the product's stream in one transaction; and tests. Seven Catalog tests pass (2 publish + 2 browse + 3 change-price); the real-HTTP run confirmed it (`crit-777` `50.00` → `39.99`, browse reflects it, unknown SKU → `404`). The per-product audit stream now genuinely **grows** — `ProductPublished` → `ProductPriceChanged` — the first stream in the project with more than one event.

**Catalog round one is now complete:** slices 1.1 (publish), 1.2 (browse), 1.3 (change price) are all shipped.

## What worked

- **The one-PR slice mode is faster, and lost nothing.** Authoring narrative + proposal + implementation in one session eliminated the merge-wait latency between the three triangle edges (slices 1.1/1.2 each spanned three PRs and three review cycles). All the same artifacts were produced — narrative, proposal, spec, design, tasks, prompt, retro, code, tests — only the PR/session boundary collapsed. The spec-delta-closure and retro disciplines held inside the one PR.
- **Capability granularity is confirmed — 3 for 3.** `product-catalog` now carries three `## ADDED Requirements` (publish + SKU-uniqueness, browse, change-price), and slice 1.3 ADDed cleanly with no `MODIFIED` needed (publish and browse behavior are untouched; browse reflects the new price purely because it queries the document). The slice 1.1/1.2 retros flagged "revisit one-capability-per-BC at 1.3" — it holds across all three Catalog operations. Ready to encode as a convention.
- **The audit stream finally grows.** `ProductPriceChanged` as the second moment on `crit-001`'s stream is the first concrete demonstration of "even a CRUD service keeps an event-sourced audit log" with an actual multi-event history. The test asserts the ordering (`ProductPublished` then `ProductPriceChanged`) and the old/new prices.
- **ctx7 caught a package boundary before a wrong turn.** The idiomatic `[Entity]` auto-404 declarative load resolves Marten docs from route params — but that HTTP+Marten integration lives in `WolverineFx.Http.Marten`, which the project doesn't reference. The explicit `LoadAsync` + `Results.Problem(404)` path needs no new package; `design.md` had already named it as the fallback, so the implementation followed the design.
- **Reuse kept the slice small.** `Product`, `SellerIdentity`, `CatalogAppFixture`, and the Scenario/clean test patterns all carried over; despite touching three artifact layers, the net new code is one event record + one endpoint + one test file.

## What was harder than expected

- **The one-PR mode diverges from documented disciplines — handled non-silently.** It overrides CLAUDE.md's one-prompt-one-session-one-PR and ADR 011's proposal-vs-implementation session split. Per the user's explicit choice (asked before starting), the divergence is **kept informal** — no ADR, no `structural-constraints.md` change — and recorded here. If the mode persists beyond a slice or two, revisit formalizing it (the repo's own rule pairs a constraint change with a rule-file update).
- **`[Entity]` vs. explicit load — an idiom/packaging trade-off.** The declarative `[Entity]` path is the Critter Stack's preferred "pure function over the loaded entity" idiom and gives the 404 for free, but it would pull in `WolverineFx.Http.Marten`. The explicit `LoadAsync` + null-check is the "imperative shell" style but adds no dependency. Chose explicit for round one; `[Entity]` + `WolverineFx.Http.Marten` is worth adopting if declarative loading is wanted more broadly (e.g., it would also clean up a future `GET /products/{sku}` detail endpoint).
- **Workshop is happy-only for 1.3; the 404 is tested but unspecced.** Workshop 001 § 6.1 gives slice 1.3 only a happy path. The unknown-SKU `404` is a defensive engineering default with a test, **not** a spec scenario — added without amending Workshop 001 (which would need its own workshop-tidy). The code is intentionally more robust than the spec; narrative, proposal, and workshop all stay mutually consistent (all happy-path).

## Methodology refinements that emerged

1. **One-PR slice cadence — data point 1.** It works and is faster; nothing in the discipline set actually required the three-PR split except the documented rule the user chose to override. Watch whether bundling makes review harder as slices get bigger (Catalog slices are small; Orders slices will be larger and cross-BC — re-evaluate the mode there).
2. **Tests may exceed the spec when the workshop is happy-only.** A defensive guard (here, 404) can be implemented and tested without back-filling a GWT into the workshop, provided the divergence is recorded and the human/machine specs stay consistent. Keeps the code robust without silently mutating the source-of-truth workshop.
3. **Capability-granularity convention is ready to encode.** Three Catalog operations all ADD cleanly to one `product-catalog` capability. A `tidy: encode-one-capability-per-bc` could lift "one capability per bounded context" into CLAUDE.md / structural-constraints — the evidence is now complete for Catalog.

## Outstanding items / next-session inputs

1. **Catalog round one is done.** Next is a **different bounded context** — Inventory (2.x, event-sourced `Stock`) or Orders (3.x cart / 4.x Place Order, event-sourced + the cross-BC process manager — the centerpiece material). Either is a natural next focus; both are the harder, more pedagogically central event-sourcing work. The infra bundle (Aspire + OTel + Static codegen) is the other strong candidate, and arguably *should* precede the cross-BC Orders work (RabbitMQ + Aspire orchestration + OTel tracing are what make the Place Order demo legible).
2. **`openspec archive slice-1-3-change-price`** not done — one unarchived change again; archive when convenient (or batch with future ones).
3. **Encode one-capability-per-BC** (tidy) — evidence complete for Catalog.
4. **One-PR mode formalization** — deferred per user (informal); revisit if it sticks.
5. **Standing `tidy: docs` debt** (unchanged): README *Current population* lines lag several kinds; `docs/narratives/README.md` `docs/specs/` path drift.
6. **`[Entity]` + `WolverineFx.Http.Marten`** — consider adopting if declarative entity loading is wanted more widely (Orders aggregate endpoints will likely want `[Aggregate]` from the same package anyway).
7. **docker-compose volume accumulation** (`crit-001`, `crit-002`, `crit-099`, `crit-777`) — `docker compose down -v` wipes.

## Spec-delta — landed?

**Yes.** The prompt named:

1. Narrative 001 → v1.1 with Moment 3, `slices: [1.1, 1.3]` — **landed**.
2. `product-catalog` gains a change-price requirement (third), satisfied by code — **landed**; `openspec validate --strict` passes; tests + real run prove it.
3. Capability-granularity confirmed (ADDED, not MODIFIED) — **landed**; convention now encodable.

No spec-delta items dropped. The not-found path is a tested-but-unspecced defensive addition (recorded above), not a spec scenario.

## Process notes

- **One PR** (first consolidated slice): `docs:` (narrative v1.1 + proposal + design + tasks + prompt + retro) and `feat:` (event + endpoint + tests). Branch `feat/slice-1-3-change-price`.
- One-PR-mode divergence from CLAUDE.md/ADR 011 **kept informal** per the user's explicit choice; recorded here rather than via ADR/rule change.
- ctx7-verified the Wolverine 6 / Marten 9 API before writing; explicit `LoadAsync` chosen over `[Entity]` to avoid adding `WolverineFx.Http.Marten`.
