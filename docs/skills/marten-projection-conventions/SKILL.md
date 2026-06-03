---
name: marten-projection-conventions
description: "CritterMart's Marten projection conventions: the load-bearing partial keyword, instance-registered projections carrying config, IEvent<T> metadata folds, ShouldDelete conditional deletes, and Identity<IEvent<T>> multi-stream routing. Use when writing, reviewing, or testing any Marten projection in this codebase."
cluster: core
tags: [marten, projections, event-sourcing, conventions, testing]
---

# CritterMart Marten Projection Conventions

The conventions this codebase's Marten projections follow, with their in-repo precedents. Each convention here has shipped at least three times; the file references are the authority — when this skill and the code disagree, the code wins and this skill gets a DEBT row.

**Defer-to-upstream discipline:** the upstream JasperFx ai-skills library (`marten-projections-single-stream`, `marten-projections-multi-stream`) documents the Marten projection *APIs*. This skill documents only what CritterMart layers on top: which API options this project chose, how they're wired here, and the traps that cost real sessions. Do not consult this skill for Marten API mechanics; do not consult upstream for CritterMart wiring.

## When to apply this skill

- Writing a new projection (single-stream or multi-stream) in any CritterMart service.
- Registering a projection in a service's `Program.cs`.
- Unit-testing a projection's fold methods.
- Reviewing projection code in a PR.
- Diagnosing a host that refuses to boot with `InvalidProjectionException`.

## Convention 1 — `partial` is load-bearing (read this first)

Every convention-method projection class **must be declared `partial`**:

```csharp
public partial class CartViewProjection : SingleStreamProjection<CartView, string>
```

Marten 9 dispatches conventional `Apply` / `ShouldDelete` methods via a **compile-time source generator** that extends the partial class. There is **no runtime fallback**: a projection missing `partial` fails at host startup with `InvalidProjectionException: No source-generated dispatcher found`, taking every integration test down with it.

This was slice 3.4's only failure (retro implementations/013): the single-stream projections all had `partial` copied from precedent, but the first multi-stream projection was written fresh and missed it. The registration comments in `Program.cs` now state the requirement; this skill leads with it for the same reason.

**Precedents:** every projection class in `src/CritterMart.Orders/` and `src/CritterMart.Inventory/`.

## Convention 2 — Instance registration for config-carrying projections

A projection whose fold needs a **configured value** takes it through its constructor and is registered as an **instance**, not generically:

```csharp
// Program.cs — instance registration: the projection carries the configured timeout
opts.Projections.Add(new OrdersAwaitingPaymentProjection(paymentTimeout), ProjectionLifecycle.Inline);
opts.Projections.Add(new CartsAwaitingActivityProjection(cartActivityTimeout), ProjectionLifecycle.Inline);
```

versus the generic registration used when no config is needed:

```csharp
opts.Projections.Add<CartViewProjection>(ProjectionLifecycle.Inline);
```

**Why CritterMart does this:** the todo-list projections (`OrdersAwaitingPayment`, `CartsAwaitingActivity`) display a *deadline* — placement/activity time plus the configured timeout. The same configured `TimeSpan` feeds both the Wolverine schedule and the projection's visible deadline, so the row a customer-support person reads matches what the automation will actually do. One config value, two consumers, no drift.

**Precedents:** `src/CritterMart.Orders/Order/OrdersAwaitingPayment.cs` (constructor + `Program.cs` registration), `src/CritterMart.Orders/Cart/CartsAwaitingActivity.cs`.

## Convention 3 — `IEvent<T>` metadata folds

When a fold needs the event's **append timestamp** (or any other event metadata), the `Apply` method takes the `IEvent<T>` wrapper instead of the bare event:

```csharp
// The wrapper exposes e.Timestamp (append time) alongside e.Data (the event itself)
public void Apply(IEvent<CartCreated> e, CartView view)
{
    view.CustomerId = e.Data.CustomerId;
    view.LastActivityAt = e.Timestamp;
}
```

The convention's discipline: **only events whose timestamps are domain-meaningful take the wrapper.** Terminal events (e.g. `CartCheckedOut`, `CartAbandoned`) deliberately use the plain-event signature — they end the entity rather than shape its activity clock, and the signature difference makes that intent readable.

**Why CritterMart does this:** the activity-clock pattern. The Cart's abandonment decision and the Order's payment deadline both derive from *when events were appended*, not from any field stored on the events. The stream's own timestamps are the single source of truth (the "stream decides, not the todo-list" principle from slices 4.7/3.4).

**Precedents:** `src/CritterMart.Orders/Cart/CartView.cs` (`LastActivityAt`), `Order/OrdersAwaitingPayment.cs` (`Deadline`), `Cart/CartsAwaitingActivity.cs` (`Deadline`).

## Convention 4 — Conditional deletes via `ShouldDelete`

A projection whose document should **stop existing** when the stream reaches a terminal state declares it with the `ShouldDelete` method convention:

```csharp
// Any terminal event removes the row from the todo-list
public bool ShouldDelete(OrderConfirmed e) => true;
public bool ShouldDelete(OrderCancelled e) => true;
```

**Why CritterMart does this:** the Bruun todo-list pattern (Workshop 001 § 5/§ 7). `OrdersAwaitingPayment` and `CartsAwaitingActivity` are "rows that exist only while work is pending" — the conditional delete is what makes them todo-*lists* rather than status views. Contrast with `OrderStatusView`/`CartView`, which persist after terminal events as readable history.

**Precedents:** `src/CritterMart.Orders/Order/OrdersAwaitingPayment.cs`, `Cart/CartsAwaitingActivity.cs` (both delete on both of their stream's terminal events).

## Convention 5 — `Identity<IEvent<T>>` multi-stream routing

A multi-stream projection that groups documents by **event metadata** (rather than by a field on the event) declares its identity routing with the `IEvent<T>` wrapper form in its constructor:

```csharp
public CartAbandonmentReportProjection()
{
    // Route every CartAbandoned to the report document for its abandonment day (UTC)
    Identity<IEvent<CartAbandoned>>(e => e.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd"));
}
```

This is the multi-stream analog of Convention 3: the metadata that single-stream folds read per-event, multi-stream projections can also *route* by. No custom grouper or database lookup is needed for metadata-derived identity — it is first-class.

**Precedent:** `src/CritterMart.Orders/Cart/CartAbandonmentReport.cs` (the round-one async teaser, ADR 008 — one document per UTC day of abandonment).

## Testing conventions

Projection folds are **pure functions** — unit-test them without a database:

```csharp
// Constructing an IEvent<T> wrapper in a unit test: wrap the event, set the metadata
var placed = new Event<OrderPlaced>(new OrderPlaced("order-1", "customer-X", [Plush], 49.98m))
{
    Timestamp = placedAt
};

var projection = new OrdersAwaitingPaymentProjection(TimeSpan.FromMinutes(10));
var view = new OrderAwaitingPayment();
projection.Apply(placed, view);

view.Deadline.ShouldBe(placedAt.AddMinutes(10));
```

- `Event<T>` (concrete, from `JasperFx.Events`) is the test-side constructor for the `IEvent<T>` wrapper; object-initialize whatever metadata the fold reads.
- Config-carrying projections (Convention 2) are constructed directly with a test value — the instance registration pattern is what makes this possible.
- Async-lifecycle projections are integration-tested via **rebuild-on-demand** (`store.BuildProjectionDaemonAsync()` → `RebuildProjectionAsync<T>()`), which works with no hosted daemon — see `tests/CritterMart.Orders.Tests/CartAbandonmentTests.cs`.

**Precedents:** `tests/CritterMart.Orders.Tests/OrdersAwaitingPaymentProjectionTests.cs`, `CartViewProjectionTests.cs`, `CartsAwaitingActivityProjectionTests.cs`.

## Quick reference: common mistakes to catch

| Mistake | Symptom | Fix |
|---|---|---|
| Missing `partial` on a convention-method projection | Host won't boot: `InvalidProjectionException: No source-generated dispatcher found` | Add `partial` to the class declaration |
| Generic registration of a config-carrying projection | Compile error (no parameterless constructor) or a hardcoded default that drifts from the schedule | Instance-register: `opts.Projections.Add(new X(config), …)` |
| Plain-event signature where the fold needs append time | Timestamp silently unavailable; tempts adding a redundant timestamp field to the event | Take `IEvent<T>`, read `e.Timestamp` |
| `IEvent<T>` wrapper on terminal events "for consistency" | Works, but muddies the activity-clock signal | Terminal events take the plain signature deliberately |
| Status-view projection given `ShouldDelete` | History disappears when streams terminate | Conditional deletes are for todo-lists; status views persist |
| Testing folds through the database | Slow tests, Testcontainers dependency for pure logic | Construct `new Event<T>(data) { … }` and call `Apply` directly |

## See also

- Upstream `marten-projections-single-stream` / `marten-projections-multi-stream` (JasperFx ai-skills) — the API mechanics this skill defers to.
- `docs/decisions/008-inline-projections-async-teaser-no-daemon.md` — why projections are inline and what the one async exception is.
- `docs/skills/DEBT.md` — where gaps in this skill get recorded when code and skill diverge.
- `docs/retrospectives/implementations/011-slice-4-7-cancel-on-payment-timeout.md` and `013-slice-3-4-cart-abandonment.md` — the sessions that surfaced these conventions.
