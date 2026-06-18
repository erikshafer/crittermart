# Design: customer-data (slices 5.3 + 5.4)

## Decisions

1. **`CustomerRegistered` moves to `CritterMart.Contracts`** (the shared published-language assembly).
   Previously Identity-internal because nothing consumed it. Slice 5.4 introduces the first consumer
   (Orders), which is exactly when the workshop mandated the graduation. The exchange name changes on
   rename; since the previous exchange had no bindings (no consumer), no messages are in-flight and
   the rename is safe.

2. **`CustomerRegisteredHandler` is a plain Wolverine message handler in `Orders/Customers/`** (not a
   Marten event subscription). `CustomerRegistered` arrives over RabbitMQ as a Wolverine message, not
   from Marten's internal event store — the `marten-event-subscriptions` patterns (daemon, async
   forwarding) don't apply. A `static Handle(CustomerRegistered, IDocumentSession)` handler is all
   that's needed; `AutoApplyTransactions` commits the session.

3. **`LocalCustomerView` is a plain Marten document** (not a projection, not a snapshot). It is
   upserted via `session.Store(...)` when `CustomerRegistered` is handled. `opts.Schema.For<LocalCustomerView>()`
   registers it with Marten's schema management so the table is created at startup alongside the other
   Order documents. Keyed by `customerId` (same value as `Id`). No index needed — all reads are primary-key
   loads (`session.LoadAsync<LocalCustomerView>(customerId)`).

4. **Read-time enrichment via `EnrichedOrderView`** — the `OrderStatusView` projection is NOT changed.
   The endpoint loads `LocalCustomerView` alongside `OrderStatusView` and wraps both in `EnrichedOrderView`,
   a new response DTO. This keeps the projection pure (event-driven, no side dependencies) and the
   enrichment transparent (a second document load at read time). The two loads are independent
   (`LoadAsync<OrderStatusView>` + `LoadAsync<LocalCustomerView>`) and both are primary-key hits, so
   performance is not a concern.

5. **`GET /orders/mine` does one customer load for the whole list** (not N loads for N orders). All
   orders in "my orders" share the same `customerId` — the `X-Customer-Id` header. One
   `LoadAsync<LocalCustomerView>(customerId)` serves all rows. Efficient by construction.

6. **`RegisterCustomer` accepts an optional explicit `Id?`** to support the seeder's deterministic
   `customer-demo` id. All existing callers (identity tests, other code) that supply no `Id` get a
   server-minted UUID as before — the defaulted `null` is the backwards-compatible path.

7. **Seeder wires to Identity via `IDENTITY_URL`**, injected by the AppHost like the existing
   `CATALOG_URL` / `INVENTORY_URL`. The seeder `WaitFor(identity)` so it doesn't call before Identity
   is healthy. The customer seed is idempotent: a `409 CustomerAlreadyRegistered` (duplicate email) →
   skip, mirroring the existing product seed's `409 Conflict` → skip pattern.
