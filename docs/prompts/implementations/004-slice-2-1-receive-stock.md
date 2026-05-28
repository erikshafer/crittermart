# Prompt: Implementations 004 — Slice 2.1 Receive Stock (Inventory skeleton + first event-sourced slice, one PR)

**Kind**: per-slice, **consolidated** (narrative + proposal + new-service skeleton + implementation in one PR)
**Files touched**: this prompt; `docs/narratives/003-operator-manage-stock.md` (new); `openspec/changes/slice-2-1-receive-stock/{.openspec.yaml, proposal.md, specs/stock-management/spec.md, design.md, tasks.md}` (new); `src/CritterMart.Inventory/**` (new service); `tests/CritterMart.Inventory.Tests/**` (new); `CritterMart.slnx` (+2 projects); `docs/retrospectives/implementations/004-slice-2-1-receive-stock.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; ctx7-verified Marten 9 / Wolverine 6 API
**Commit subject(s)**: `docs: slice 2.1 narrative + receive-stock proposal/design` + `feat: Inventory service skeleton + slice 2.1 receive stock` (one PR)

## Framing

The first **event-sourced** bounded context. Catalog (BC 1) was the "CRUD is fine" document-store example; Inventory is the **textbook event-sourcing case** (Workshop 001 § 2): a `Stock` stream per SKU, an inline `StockLevelView` snapshot. Slice 2.1 (`ReceiveStock`) is its genesis operation. Built under the consolidated one-PR mode (memory `feedback-consolidate-slice-prs`); divergence from ADR 011's session split kept **informal**, recorded in the retro. Inventory is already in the context map + Workshop 001, so **no context-map/workshop change** — just the per-slice triangle + a new service skeleton (the "skeleton + first slice" exception).

Stack: **Wolverine 6.1 / Marten 9.2**. Single-BC — **no RabbitMQ, no Aspire** (that infra bundle comes before Orders).

## Goal

A running `CritterMart.Inventory` service where `ReceiveStock` appends `StockReceived` to a per-SKU stream (creating it on first receipt, appending on later receipts) and an inline `StockLevelView` snapshot reflects the available quantity — proven by tests over both Workshop 001 § 6.1 slice 2.1 happy paths and a real run. `openspec validate slice-2-1-receive-stock --strict` passes.

## Spec delta

New narrative (003, the Operator's stock journey). New openspec capability `stock-management` with a **Receive stock** requirement (Inventory's first). First event-sourced aggregate + inline projection in the codebase. New `CritterMart.Inventory` service.

## Orientation

1. **Workshop 001** § 2 (Inventory: `Stock` stream per SKU, `StockReceived`, inline `StockLevelView`), § 4 (Inventory vocabulary), § 5 (slice 2.1 row), § 6.1 (2.1 GWT — two happy paths: receive new → `available 100`; receive additional onto a stream with a reservation → `available 120, reserved 30`; **no failure path**).
2. **`src/CritterMart.Catalog/`** — the service-skeleton + Program.cs + test-fixture templates (Marten config, schema-per-service, `StreamIdentity.AsString`, `RuntimeCompilation`, Alba + Testcontainers). Mirror, with `inventory` schema.
3. **Skills `wolverine-http-marten-integration` + `marten-projections-single-stream`** — the event-sourced patterns. Note: `[Aggregate]` cannot create a stream (404s if missing), so `ReceiveStock` (create-or-append) uses explicit `FetchForWriting<StockLevelView>(sku)` — which keeps Inventory on `WolverineFx.Marten` only (no `WolverineFx.Http.Marten`).
4. **openspec CLI instructions**.

## Working pattern

1. Author this prompt (done).
2. **Narrative 003** — the Operator's stock-receiving journey (the same one-person operator as the Seller, in their stock-keeping role; slice 2.1 only; note forthcoming reserve/release 2.2/2.3).
3. **OpenSpec change** `slice-2-1-receive-stock`: proposal (new capability `stock-management`) + spec (`## ADDED Requirements`: Receive stock, the two § 6.1 happy scenarios) + design.md + tasks.md. Validate `--strict`.
4. **Inventory skeleton:** `src/CritterMart.Inventory` (Program.cs: Marten event store, `inventory` schema (ADR 002), `StreamIdentity.AsString`, inline `StockLevelViewProjection`, Wolverine.Http, `RuntimeCompilation`); add `WolverineFx.Http`/`WolverineFx.Marten`/`WolverineFx.RuntimeCompilation` refs; add both projects to `CritterMart.slnx`; `docker-compose` Postgres already exists (shared DB).
5. **Implement:** `StockReceived` event; `StockLevelView` doc (`Id`=sku, `Available`, `Reserved`); `partial class StockLevelViewProjection : SingleStreamProjection<StockLevelView, string>` with `Apply(StockReceived, view)`; `ReceiveStock(int Quantity)` command + `POST /stock/{sku}/receipts` (FetchForWriting + AppendOne); `GET /stock/{sku}` ([read] LoadAsync, 404 if none) for demo + test reads.
6. **Test** (`CritterMart.Inventory.Tests`, Alba + Testcontainers): receive new (`crit-001` qty 100 → `StockReceived 100` on stream, `StockLevelView` available 100 / reserved 0); receive additional (+50 → available 150); `GET /stock/{sku}` reflects.
7. **Verify:** build + test green; real docker-compose run (receive, GET); `openspec validate --strict`.
8. **Retro:** spec-delta closure; first-event-sourced-BC notes; one-PR-mode data point 2; the FetchForWriting-vs-[Aggregate] decision.

## Out of scope

- **No reserve/release** (slices 2.2/2.3) — only `StockReceived`. **No cross-BC** (`ReserveStock` from Orders is slice 2.2/4.2). **No RabbitMQ, no Aspire, no OTel.**
- **No `WolverineFx.Http.Marten`** — explicit `FetchForWriting` keeps the package set lean.
- **No `openspec archive`** for 2.1. **No `tidy: docs`** items. **No formalizing the one-PR mode.**
- Stay faithful to Workshop 001 § 6.1 (happy paths only; no failure scenario for 2.1).
