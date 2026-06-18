# Prompt: Implementations 034 — Slices 5.3 + 5.4 Customer Data in Orders (consume CustomerRegistered + enrich order responses + seeder customers)

**Kind**: per-slice implementation (Identity→Orders PL integration, consolidated per [[feedback-consolidate-slice-prs]])
**Files touched**: `docs/prompts/implementations/034-slices-5-3-5-4-customer-data.md` (new, this file); `openspec/changes/customer-data/{proposal.md,design.md,tasks.md,specs/customer-registry/spec.md,specs/order-lifecycle/spec.md}` (authored this session); `docs/narratives/007-customer-data-in-orders.md` (new) + `docs/narratives/README.md` (count 6→7); `src/CritterMart.Contracts/CustomerRegistered.cs` (new); `src/CritterMart.Identity/{CritterMart.Identity.csproj,Customers/CustomerRegistered.cs,Features/RegisterCustomer.cs}` (modified); `src/CritterMart.Orders/{Customers/LocalCustomerView.cs,Customers/CustomerRegisteredHandler.cs,Ordering/EnrichedOrderView.cs,Features/PlaceOrder.cs,Features/ListMyOrders.cs,Program.cs}` (new/modified); `src/CritterMart.AppHost/Program.cs` (capture identity var, wire seeder); `src/CritterMart.Seeding/Program.cs` (customer seed block); `tests/CritterMart.Orders.Tests/CustomerRegisteredHandlerTests.cs` (new); `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` (updated); `docs/retrospectives/implementations/034-slices-5-3-5-4-customer-data.md` (forthcoming)
**Mode**: solo implementation; OpenSpec change `customer-data` authored before code (proposal → design → tasks → `openspec validate --strict` green)
**Commit subject**: `feat: consume CustomerRegistered in Orders, enrich order responses with CustomerName, extend seeder — slices 5.3 + 5.4`

## Framing

Slices 5.1/5.2 landed Identity on `main`: `POST /customers` registers a customer row, `GET /customers/{id}` resolves it, and `CustomerRegistered` publishes over RabbitMQ unconsumed. The Published-Language edge was declared in the context map but never trafficked. The `X-Customer-Id: customer-demo` header the SPA sends on every request identified *who* was making a request; nothing in Orders could say *what their name was*.

This session completes the PL edge. Build order is 5.4 before 5.3 — the local read model must exist before the enrichment logic can read it.

**Slice 5.4 (build first)** is a standard Wolverine message handler in Orders. `CustomerRegistered` arrives via RabbitMQ (NOT from Marten's event store — no subscriptions, no daemon, no forwarding). `CustomerRegisteredHandler.Handle(CustomerRegistered, IDocumentSession)` upserts a `LocalCustomerView` document via `session.Store(...)`. `AutoApplyTransactions` commits. This is also when `CustomerRegistered` graduates from `CritterMart.Identity.Customers` to `CritterMart.Contracts` (the shared PL assembly both Identity and Orders reference). The RabbitMQ exchange name changes on rename; since the old exchange had no consumer bindings, no in-flight messages are lost.

**Slice 5.3** is read-time enrichment. Both `GET /orders/{orderId}` and `GET /orders/mine` load `LocalCustomerView` alongside `OrderStatusView` (primary-key hit, no join) and return `EnrichedOrderView` — the existing `{ id, customerId, status, lines, total, placedAt, cancelReason }` fields plus `customerName?`. When the local model is absent (eventually-consistent gap), `customerName` is `null` — graceful degradation, no error. The `OrderStatusView` projection is untouched.

**Seeder.** `RegisterCustomer` gains `string? Id = null`; the seeder passes `id: "customer-demo"` to produce a deterministic customer id that matches the frontend's `useCurrentCustomer` stub. The AppHost injects `IDENTITY_URL` + `WaitFor(identity)` on the seeder.

## Goal

- `CritterMart.Contracts/CustomerRegistered.cs` exists (the PL type); Identity references Contracts and no longer defines its own `CustomerRegistered`.
- `CustomerRegisteredHandler` in Orders upserts `LocalCustomerView` when `CustomerRegistered` arrives.
- `GET /orders/{orderId}` and `GET /orders/mine` return `EnrichedOrderView` with `customerName?` populated from the local model (or `null` if absent).
- `RegisterCustomer` accepts an optional `Id?`; the seeder registers `customer-demo`; AppHost wires the Identity URL to the seeder.
- All Orders tests green (≥ `94` passing — 91 existing + ≥ 3 new); all Identity tests green (≥ `7` — 6 existing + ≥ 1 new); `dotnet build` zero errors.

## Spec delta

This session lands **two new requirements** in the `order-lifecycle` capability: *Consume `CustomerRegistered` and maintain a local customer read model* (slice 5.4) and *Resolve customer identity at read time* (slice 5.3). It also lands **one modified requirement** in `customer-registry` (the optional-explicit-id addition). Workshop 002 § 6 carries the GWT scenarios for 5.3/5.4 (modeled-not-built); this session satisfies them. The `customer-data` OpenSpec change is the machine-readable contract; Narrative 007 is the human-readable companion.

## Orientation files

1. **`docs/workshops/002-identity-event-model.md` § 5 (slice table) + § 6 (GWT 5.3/5.4)** — the source of truth; the scenarios here are what the code must satisfy.
2. **`docs/context-map/README.md`** — Identity → Orders: OHS for the frontend, PL for backends; ADR 001 forbids sync service-to-service HTTP.
3. **`openspec/changes/customer-data/proposal.md` + `design.md`** — the 7 numbered design decisions; reason 2 is the most important (handler, not subscription).
4. **`src/CritterMart.Contracts/`** — the existing shared assembly (ReserveStock, StockReserved, etc.); `CustomerRegistered` follows this pattern.
5. **`src/CritterMart.Identity/Features/RegisterCustomer.cs`** — the shape to modify for the optional `Id?`.
6. **`src/CritterMart.Orders/Program.cs`** — where `opts.Schema.For<LocalCustomerView>()` is registered; pattern: every document type registered near its sibling types.
7. **`src/CritterMart.Orders/Features/PlaceOrder.cs`** — the `OrderEndpoint.Get` method to update; return type changes from `IResult` wrapping `OrderStatusView` to `IResult` wrapping `EnrichedOrderView`.
8. **`src/CritterMart.Orders/Features/ListMyOrders.cs`** — the `GET /orders/mine` handler to update; one `LoadAsync<LocalCustomerView>` call before building the response list.
9. **`src/CritterMart.Seeding/Program.cs`** — the existing product+stock seed loop; the customer seed block mirrors it (idempotent 409 → skip).
10. **`src/CritterMart.AppHost/Program.cs`** — capture identity as `var identity = ...`, inject `IDENTITY_URL`, add `WaitFor(identity)` on seeder.

## Working pattern

1. **Create feature branch** `feat/identity-slices-5-3-5-4`.
2. **Slice 5.4 first** — contracts, Identity changes, Orders handler + LocalCustomerView + Marten registration.
3. **Slice 5.3 second** — EnrichedOrderView DTO, update both order endpoints.
4. **Seeder + AppHost** — optional Id in RegisterCustomer, AppHost wiring, seeder customer block.
5. **Tests** — CustomerRegisteredHandler tests (upsert, idempotency, enrichment present/absent), Identity explicit-id test.
6. **`dotnet build` / `dotnet test`** — zero errors, all green.
7. **Retro** — author at close; update prompts README count 33→34.

## Deliverable plan (in order)

| File | Status |
|---|---|
| `src/CritterMart.Contracts/CustomerRegistered.cs` | new |
| `src/CritterMart.Identity/CritterMart.Identity.csproj` | modify (add Contracts ref) |
| `src/CritterMart.Identity/Customers/CustomerRegistered.cs` | delete |
| `src/CritterMart.Identity/Features/RegisterCustomer.cs` | modify (optional Id?) |
| `src/CritterMart.Orders/Customers/LocalCustomerView.cs` | new |
| `src/CritterMart.Orders/Customers/CustomerRegisteredHandler.cs` | new |
| `src/CritterMart.Orders/Ordering/EnrichedOrderView.cs` | new |
| `src/CritterMart.Orders/Program.cs` | modify (Schema.For<LocalCustomerView>) |
| `src/CritterMart.Orders/Features/PlaceOrder.cs` | modify (enrich GET /orders/{orderId}) |
| `src/CritterMart.Orders/Features/ListMyOrders.cs` | modify (enrich GET /orders/mine) |
| `src/CritterMart.AppHost/Program.cs` | modify (capture identity, wire seeder) |
| `src/CritterMart.Seeding/Program.cs` | modify (customer seed) |
| `tests/CritterMart.Orders.Tests/CustomerRegisteredHandlerTests.cs` | new |
| `tests/CritterMart.Identity.Tests/RegisterCustomerTests.cs` | add test |
| `docs/retrospectives/implementations/034-slices-5-3-5-4-customer-data.md` | new (at close) |

## Out of scope

- Frontend schema update (`customerName?` in `OrderStatusViewSchema`) — follow-up frontend slice.
- `GET /customers/{id}` OHS CORS wiring — SPA doesn't call Identity directly yet.
- `customer-registry` spec `## Purpose` TBD placeholder — flagged carry-forward, separate `tidy: docs` session.
- `POST /orders` body-keyed customerId harmonization — flagged carry-forward, separate fast-follow.
- Any ADR not already named in the design decisions.
- Live-stack verification (offered proactively post-PR, per [[feedback-live-verify-after-changes]]).
