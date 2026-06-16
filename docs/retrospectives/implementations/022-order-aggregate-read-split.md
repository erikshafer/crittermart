---
retrospective: 022
kind: implementations
prompt: docs/prompts/implementations/022-order-aggregate-read-split.md
deliverable: src/CritterMart.Orders/Ordering/** (Order.cs + OrderStatus.cs new; OrderStatusView.cs converted; folder renamed from Order/), src/CritterMart.Orders/Features/PlaceOrder.cs, src/CritterMart.Orders/Program.cs, tests/CritterMart.Orders.Tests/OrderProjectionTests.cs (renamed + rewritten), docs/decisions/020-domain-write-models-read-views.md, docs/rules/structural-constraints.md (v1.5)
date: 2026-06-16
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 022: Order read/write split (ADR 020/021 rollout to the Order aggregate)

## Outcome summary

Rolled the **ADR 020 (domain write-models vs. `*View` read models) + ADR 021 (verb feature folders)** pattern out to **Order** — the second pilot after Cart (#59), the **design-return** owed after three consecutive frontend implementations (#60/#61/#62). `OrderStatusView` stopped playing three roles and split into a domain-named write aggregate and a dedicated read projection; no behavior, contract, or frontend changed.

- **`Order/` → `Ordering/`** (ADR 021 verb folder; namespace `CritterMart.Orders.Order` → `…Ordering` across ~14 moved files + the cross-namespace callers). The aggregate keeps its canonical noun `Order` — a verb namespace never collides with the noun type.
- **`Ordering/Order.cs`** (new) — the `sealed record` write aggregate *and* the PMvH process-manager state: `Status`/`Total`/`Lines` (+ `Id`/`CustomerId`), self-aggregating `Snapshot<Order>(SnapshotLifecycle.Inline)`, static `Create(OrderPlaced)` + the five status `Apply`s. The `FetchForWriting`/`StartStream` target on `PlaceOrder` and the three cross-BC handler families; never served over HTTP.
- **`Ordering/OrderStatus.cs`** (new) — the five status constants, extracted from `OrderStatusView.cs` (now shared by the aggregate, the view, and the handlers' guards).
- **`Ordering/OrderStatusView.cs`** — converted from a `class` + `OrderStatusViewProjection : SingleStreamProjection` to a `sealed record` self-aggregating `Snapshot<OrderStatusView>(SnapshotLifecycle.Inline)` with the same five folds, **wire shape `{ id, customerId, status, lines, total }` byte-for-byte preserved**.
- **Repointed**: `PlaceOrder` → `StartStream<Order>`; `StockReservedHandler`/`StockReservationFailedHandler`/`PaymentDecisionHandler`/`PaymentTimeoutHandler` → `FetchForWriting<Order>` (reading `.Status`/`.Total`/`.Lines`); `OrderEndpoint.Get` keeps `LoadAsync<OrderStatusView>` (the read path). Program.cs swaps `Add<OrderStatusViewProjection>` for `Snapshot<Order>` + `Snapshot<OrderStatusView>`.

**Tests**: full solution green — **101 backend tests, 0 failures** (Catalog 9, Inventory 16, Orders 73, CrossBc 3); `dotnet format` clean. The former `OrderStatusViewProjectionTests` (5 tests of the deleted projection class) became `OrderProjectionTests` (7: five `Order` aggregate folds + two `OrderStatusView` read-model consistency cases), mirroring `CartProjectionTests`. The Alba integration tests (`LoadAsync`/`Query`/`ReadAsJson<OrderStatusView>` over real Postgres) and the CrossBc smoke tests (`PlaceOrder → ReserveStock → StockReserved → confirm`, plus the cancel/timeout branches) prove the wire shape and the cross-BC flow survived the split.

**Spec movement**: **no new ADR, no OpenSpec/workshop/narrative change** — a contract-preserving model refactor. ADR 020 gained an **"Applied — the Order pilot"** subsection and its **Revisit note ticks Order → done** (Stock remains the last rollout); `docs/rules/structural-constraints.md` → **v1.5** (the naming-rollout status updated). The `OrderStatusView` wire shape is unchanged, so the W3 frontend (PR #62) and the deferred W4 are untouched.

## What worked

- **The Cart pilot was a clean template, and the mechanical parts were genuinely mechanical.** `git mv` the folder + two `sed`s (namespace; `StartStream`/`FetchForWriting<OrderStatusView>` → `<Order>`) did the bulk; the hand-authored parts were just `Order.cs`, the `OrderStatusView` conversion, and the Program.cs registration. The production project compiled on the first build after the repoint.
- **No fork to surface — and that was the right call.** ADR 020 prescribes the pattern (sealed-record aggregate + dedicated `*View` snapshot) and names this rollout in its "Revisit" line; the Cart pilot is the worked example. Asking the owner to re-pick the projection form would have re-litigated a settled convention. The one genuine choice (Order-first vs. Stock-first vs. a tidy) was put to the owner up front; the implementation followed the ADR.
- **The integration suite is what makes "contract-preserving" a verified claim, not a hope.** The record conversion changes how `OrderStatusView` is *authored* (mutable class + projection class → immutable record + static folds) and *stored*; only the Alba tests deserializing the view from the live HTTP response and the CrossBc tests driving the full saga across real services prove the wire shape and the `FetchForWriting<Order>` decision path actually behave. 101 green is the proof.
- **Extracting `OrderStatus` to its own file resolved the shared-constant question cleanly.** It was hosted on `OrderStatusView` (the type that split); pulling it to `Ordering/OrderStatus.cs` lets the aggregate, the view, and the handlers' guards all reference one source without either split type owning it.
- **Plain-event folds (no `IEvent<T>` wrapper) are simpler than Cart's.** The order tracks no activity timestamp, so `Create(OrderPlaced)` / `Apply(StockReserved, order)` need no `IEvent<T>` — a small but real simplification over the cart's timestamp-folding `At(...)` helper, and the `OrderProjectionTests` are correspondingly cleaner.

## What was harder / notable

- **`OrderStatusView` was a *sharper* conflation than `CartView` — three roles, not two.** Beyond write-target + served-read, it was the **PMvH decision state**: every cross-BC handler `FetchForWriting<OrderStatusView>` and branched on `.Status` for idempotency. So the split's payoff is larger here — the idempotent stream-state guards now sit on a protected `Order` write model that can grow decision-only fields (retry counts, gate flags) without ever leaking them to the W3/W4 wire — but the repoint touched more call sites (4 `FetchForWriting` + 1 `StartStream`) than Cart's.
- **The projection-class → record conversion is a real authoring shift, not a rename.** The old `OrderStatusViewProjection` mutated a shared `new OrderStatusView()` instance (`view.Status = …`); the record folds return `view with { Status = … }`, and the genesis moved from an `Apply(OrderPlaced, view)` (relying on `SingleStreamProjection` to default-construct + set the Id from the stream key) to a `Create(OrderPlaced)` that returns the full record incl. `Id = e.OrderId`. The unit test that did `new OrderStatusView { Status = … }` couldn't survive — hence the file rewrite, not an edit.
- **`StartStream<OrderStatusView>` lingered in the test setup, not just production.** Three test files seeded streams as `StartStream<OrderStatusView>`; left unfixed they'd type the stream to the read model. A second `sed` over the test project caught them — a reminder that a write-target rename has a test-side blast radius beyond the obvious production handlers.

## Methodology refinements

- **"ADR + pilot one aggregate, then roll out per-PR" held its promise.** The Cart pilot de-risked Order: same `git mv` + `sed` + Snapshot-registration shape, same wire-preservation constraint, same integration-test safety net. The template made a multi-file refactor low-drama. Stock will be the third and smallest application.
- **A design-return can be a code-primary refactor, and it still belongs in `implementations/`.** This PR authored no decision (it applied ADR 020/021 and ticked their status), so it is `implementations/022`, not a `decisions/` session — the cart pilot was `decisions/002` only because it *authored* the ADRs. The cadence rule's "design-tidy PR" is satisfied by a refactor that realizes a standing decision; the kind follows the deliverable (code), not the cadence role.
- **Re-running the no-fork judgment against the ADR before asking saved a round-trip.** The instinct was to surface "self-aggregating snapshot vs. keep the projection class" as a fork; reading ADR 020 showed it prescribes the snapshot form and the Cart pilot realizes it, so it wasn't a genuine choice. Checking the authority before drafting an `AskUserQuestion` is the discipline.

## Outstanding / next-session inputs

- **Stock pilot is the last ADR 020/021 rollout.** `StockLevelView` → a `StockLevel` write aggregate + a dedicated read model, in Inventory — **no folder rename** (`StockLevel` ≠ `…Stock`, so no collision). Smaller than Order (no PMvH state). Reuses this same `git mv`-free / Snapshot-registration template. Its own PR.
- **W4 order-tracking** remains the open frontend follow-on (reuses `client/src/orders/`; the disabled `[ Track this order ]` on W3 becomes a live `<Link>`). Either Stock or W4 is a fine next slice; after this design-return the implementation budget is replenished.
- **Cadence**: this PR *was* the design-return (it self-interleaves ADR 020 + the rules-file update). The next 2–3 implementations (W4, the harmonization tidy, etc.) run before the next interleave is due.
- **Carry-forward (unchanged, non-blocking)**: the cart identity-transport harmonization tidy (4 commands); the `AddToCart` null-snapshot 500 (latent backend gap); no frontend CI job; `client/README.md` Layout block stale (omits the feature folders); the workshop § 5.1 W3 bullet still says `{ orderId, status }`; the overdue `tidy: encode-ceremony-rule`; the NU1507 two-nuget-source warning; CritterWatch trial expires **2026-07-10**.

## Spec-delta — landed?

**Named delta landed.** The prompt named **ADR 020 gains an "Applied — the Order pilot" note + its Revisit note ticks Order → done**, with no OpenSpec/workshop/narrative change (contract-preserving). That landed: ADR 020 carries the Order-pilot subsection and the updated Revisit bullet (Stock the last rollout); `docs/rules/structural-constraints.md` is v1.5 with the rollout-status update. The forward-confirmed "no behavioral spec delta" holds — the `OrderStatusView` wire shape is byte-for-byte unchanged (the Alba/CrossBc tests prove it), so no spec the SPA or another service binds moved. This restructured *how the Order model is expressed* and *where it lives*, nothing more.
