# ADR 014: Published-Language Cross-BC Contracts in a Shared `CritterMart.Contracts` Project

**Status**: Accepted

## Context

Slice 4.2 introduced CritterMart's first cross-bounded-context message flow: Orders asks Inventory to reserve a placed order's stock, and Inventory replies with the outcome. Three wire records carry that conversation — `ReserveStock` (with `ReserveStockLine`), `StockReserved`, and `StockReservationFailed`. Both the Orders service (which sends `ReserveStock` and handles the replies) and the Inventory service (which handles `ReserveStock` and sends the replies) must agree on these shapes byte-for-byte for serialization across RabbitMQ to round-trip.

The Orders↔Inventory relationship is **Customer-Supplier** (context map): Orders is the customer, Inventory the supplier whose capacity gates fulfillment. The question this ADR settles is *where the contract types physically live*, given a hard round-one structural constraint — "services do not reference each other's projects" (ADR 001, structural-constraints § Cross-service messaging). If neither service may reference the other, the wire shapes need a home that is neither service.

This decision was resolved with the user during the 4.2 implementation session and parked in that change's `design.md` (decision 4) pending a durable cross-change record. It clears the ADR bar on all three counts: it spans two bounded contexts, the trade-off (shared coupling vs. duplicated lockstep) is non-obvious, and a later contributor adding a second cross-BC pair would otherwise re-derive it.

## Decision

The cross-BC message records live in a new `CritterMart.Contracts` project that **both** Orders and Inventory reference via `ProjectReference`. This project is the explicit **published language** of the Customer-Supplier relationship — a single source of truth for the wire shapes — and carries no Wolverine or Marten dependency (it is plain records).

This does **not** breach "services do not reference each other's projects." `Contracts` is not a service: it holds no handlers, no `Program`, no persistence. Both services already share `CritterMart.ServiceDefaults`, so a shared non-service project is an established pattern; `Contracts` extends that pattern to domain contracts rather than cross-cutting infrastructure.

`Contracts` owns **only the wire messages**. Neither context's stream events leak into it. This matters because the same conceptual fact — a granted reservation — is persisted three times, each owned by its context: `Inventory.Stock.StockReserved` (the per-SKU Stock-stream event), `Contracts.StockReserved` (the order-level wire message), and `Orders.Order.StockReserved` (the Order-stream Klefter event). The shared project draws the line at the wire; inbound handlers on each side map the message to their own stream event. The repeated name across three namespaces is intentional — the workshop calls them the same fact — and kept un-aliased.

## Consequences

The wire shapes have one definition, so a change to `ReserveStock` is a single edit that both services see at compile time; a mismatch becomes a build error rather than a runtime serialization failure. The published language is named and locatable, which is the teaching point the talk wants from a Customer-Supplier boundary.

The accepted cost is a **shared-kernel coupling** on the contract types: both services are bound to recompile against a single shared definition, and an independent-evolution scenario (Inventory accepting an older `ReserveStock` shape while Orders sends a newer one) is not modeled in round one. That is acceptable for a single-repository teaching reference; it would warrant revisiting (e.g., versioned contracts, or schema-registry-style decoupling) if the contexts ever deployed and versioned independently.

Rejected alternatives. **Per-service duplicated records** with aligned `[MessageIdentity]` would decouple the two services entirely, but trades the coupling for a lockstep-maintenance burden — two definitions that must be kept identical by discipline, with drift surfacing only at runtime. For a single repo, a single source of truth is clearer and safer than enforced duplication. **Folding contracts into `ServiceDefaults`** would avoid a new project but conflate domain contracts (what the two contexts say to each other) with cross-cutting infrastructure (telemetry, health checks, host defaults) — two different axes of change in one project. A dedicated `Contracts` project keeps the published language a first-class, separately-evolving artifact.

This ADR is paired with a note in [`docs/rules/structural-constraints.md`](../rules/structural-constraints.md) § Cross-service messaging (same PR, per that file's header convention). The originating rationale is [slice-4-2-reserve-stock `design.md`](../../openspec/changes/archive/2026-05-31-slice-4-2-reserve-stock/design.md) decisions 4 and 5; this ADR is the durable cross-change record of it.
