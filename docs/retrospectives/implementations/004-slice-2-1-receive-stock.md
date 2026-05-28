---
retrospective: 004
kind: implementations
prompt: docs/prompts/implementations/004-slice-2-1-receive-stock.md
deliverable: docs/narratives/003-operator-manage-stock.md (new); openspec/changes/slice-2-1-receive-stock/{proposal.md, specs/stock-management/spec.md, design.md, tasks.md} (new); src/CritterMart.Inventory/** (new service); tests/CritterMart.Inventory.Tests/** (new); CritterMart.slnx (+2); docs/prompts/implementations/004-slice-2-1-receive-stock.md (new); docs/retrospectives/implementations/004-slice-2-1-receive-stock.md (this file)
date: 2026-05-28
mode: solo, consolidated one-PR slice; ctx7 docs verification
session-runner: Claude (Opus 4.7)
---

# Retrospective — Implementations 004: Slice 2.1 Receive Stock (first event-sourced BC)

## Outcome summary

The **first event-sourced bounded context** shipped: a new `CritterMart.Inventory` service plus slice 2.1 (`ReceiveStock`), in one PR. `ReceiveStock` appends `StockReceived` to a per-SKU `Stock` stream (creating it on first receipt via `FetchForWriting`), and an **inline** `StockLevelView` single-stream projection accrues the available quantity. `GET /stock/{sku}` reads the snapshot. Narrative 003 (the Operator's stock journey) and the `slice-2-1-receive-stock` openspec change (new `stock-management` capability) round it out. Three tests pass; the real run confirmed it (two receipts of 100 + 50 → `{"available":150,"reserved":0}`, unknown SKU → 404); `openspec validate --strict` passes. This is the project's first event-sourced aggregate and first inline projection — the textbook contrast to Catalog's document store.

## What worked

- **`FetchForWriting` was the right create-or-append primitive.** `ReceiveStock` must both create (first receipt) and append (later receipts); `[Aggregate]` 404s on a missing stream and `StartStream` throws on an existing one. `FetchForWriting<StockLevelView>(sku)` handles both, and using it with an injected `IDocumentSession` kept Inventory on `WolverineFx.Marten` only — no `WolverineFx.Http.Marten`. The skill's "manual FetchForWriting is an anti-pattern" caveat is scoped to *when `[Aggregate]` would suffice*; here it wouldn't.
- **ctx7 nailed the Marten 9 projection shape.** The `partial class StockLevelViewProjection : SingleStreamProjection<StockLevelView, string>` with `Apply(StockReceived, view)` is the source-generator-friendly Marten 9 form; Marten auto-creates the empty view for the genesis event and sets `Id` to the stream key. Confirmed before writing.
- **The Catalog skeleton was a reusable template.** `Program.cs`, the Marten schema-per-service config, `StreamIdentity.AsString`, `RuntimeCompilation`, and the Alba + Testcontainers fixture all carried over; standing up the second service was fast. The "blueprint architecture" payoff compounds across BCs, not just slices.
- **One-PR mode held for a bigger slice (data point 2).** A new service + first event sourcing + narrative + proposal in one PR was coherent and faster than the three-PR triangle would have been. The artifacts are all present; only the boundary collapsed.
- **The event-sourcing contrast is now demoable.** Catalog (CRUD document) vs. Inventory (`available: 150` *projected from* `StockReceived` events) is the talk's core thesis, now runnable end-to-end.

## What was harder than expected

- **Namespace hunt: `SingleStreamProjection` is in `Marten.Events.Aggregation`** (not `Marten.Events.Projections`). The build error + a package grep resolved it in one pass. (`ProjectionLifecycle`/`StreamIdentity` are in `JasperFx.Events(.Projections)`, as expected from the v6/v9 moves.)
- **A void Wolverine.HTTP endpoint returns 204, not 200.** The `ReceiveStock` POST returns no body → 204 No Content. The test asserted 200 and failed; fixed the assertion (one iteration). Good to have confirmed for future command endpoints.
- **Workshop § 6.1 scenario 2 assumes a reservation that doesn't exist yet.** The workshop's second 2.1 happy path has a pre-existing `StockReserved { 30 }` (→ `available 120`). Reserve is slice 2.2, so scenario 2 was scoped to receive-accumulation (`available 150`, no reservation), with the reserved variant deferred to 2.2 — recorded in the proposal and design rather than pulling `StockReserved` into 2.1.

## Methodology refinements that emerged

1. **New-BC stand-up is cheap once one service exists.** The Catalog skeleton is effectively a service template; the second service reused its Program.cs/fixture/schema patterns wholesale. Future BCs (Orders) inherit the same.
2. **Forward-compatible read-model shape.** `StockLevelView.Reserved` exists (always `0` in 2.1) so the read model matches the workshop's available/reserved shape now; slice 2.2 fills it. Modeling the full read shape up front avoids a later view migration.
3. **One-PR mode scales to a service-sized slice** (data point 2) — but Orders' cross-BC slices will be larger still; re-evaluate review legibility there.

## Outstanding items / next-session inputs

1. **The infra bundle is now worthwhile.** With **two services** (Catalog + Inventory), Aspire orchestrating both + Postgres, plus OpenTelemetry, is no longer "thin" — and slice 2.2 (Reserve stock) is **cross-BC** (`ReserveStock` from Orders over RabbitMQ), which *needs* RabbitMQ + Aspire. Strong recommendation: **the infra bundle (Aspire + RabbitMQ + OTel + Static codegen) is the natural next focus**, before the cross-BC Inventory/Orders slices. It also makes the demo legible (the Aspire dashboard + distributed traces).
2. **`openspec archive`** — slices 1.3 and 2.1 are now complete-but-unarchived (`product-catalog` and `stock-management`). Batch-archive when convenient.
3. **Encode one-capability-per-BC** — Catalog's evidence is complete; `stock-management` as Inventory's single capability reinforces it. A `tidy:` could lift the convention into CLAUDE.md / structural-constraints.
4. **`tidy: docs` debt** (growing): README *Current population* lines now also lag `implementations/` (004), `narratives/` (003), the `specs/` count, and the new `stock-management`; plus the `docs/specs/` path drift in the narratives README. One sweep.
5. **One-PR mode** still informal per the user; revisit formalizing if it persists (esp. before the larger Orders slices).
6. **docker-compose volume** holds Catalog + Inventory test rows across schemas; `docker compose down -v` wipes.

## Spec-delta — landed?

**Yes.** Narrative 003 created; the `stock-management` capability + Receive stock requirement are satisfied by code (first event-sourced aggregate + inline projection); both § 6.1 happy paths proven by tests + real run; `openspec validate --strict` passes. Scenario 2's reservation variant was deliberately deferred to 2.2 (recorded), not dropped.

## Process notes

- **One PR**: `docs:` (narrative + proposal + design + tasks + prompt + retro) and `feat:` (Inventory skeleton + slice 2.1). Branch `feat/slice-2-1-receive-stock`. New service + first slice under the "skeleton + first slice" exception.
- One-PR-mode divergence kept informal per the user; ctx7-verified the Marten 9 projection + FetchForWriting API; `SingleStreamProjection` namespace corrected to `Marten.Events.Aggregation`.
