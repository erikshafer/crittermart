---
narrative: 007
title: Customer Data Flows into Orders
actor: Customer
status: draft
version: v1.0
slices: [5.3, 5.4]
references:
  - docs/workshops/002-identity-event-model.md (§ 3 timeline, § 5 slices 5.3/5.4, § 6 GWT scenarios)
  - docs/context-map/README.md (Identity → Orders: Open-Host Service + Published Language)
  - docs/narratives/006-customer-registration.md (the prior chapter — registration itself)
  - docs/decisions/001-separate-services-topology.md (no synchronous service-to-service HTTP)
  - docs/decisions/009-polecat-deferred-for-round-one.md (X-Customer-Id seam stays)
  - openspec/changes/customer-data/ (the machine-readable sibling)
---

# Narrative 007 — Customer Data Flows into Orders

Narrative 006 followed the Customer as they registered on the storefront and the Identity service
recorded their row. At the close of that journey, a `CustomerRegistered` event left the Identity
service — through the EF-Core transactional outbox, onto RabbitMQ — and floated there unconsumed.
No other service knew the customer by name. The `X-Customer-Id: customer-demo` header on every SPA
request identified *who* was making a request; nothing in the Orders service could say *what their
name was*.

This narrative picks up that drifting message.

Two things happen in these two slices, in strict build order:

**Slice 5.4** is when Orders starts listening. The Orders service subscribes to `CustomerRegistered`
(now a shared `CritterMart.Contracts` Published-Language type) and upserts a consumer-LOCAL customer
read model — a plain Marten document, not an event stream. Identity does not know. No synchronous
call is made (ADR 001). The local model is eventually consistent: the first order for a new customer
may arrive before `CustomerRegistered` does, which is why the enrichment in slice 5.3 degrades
gracefully when the local model is absent.

**Slice 5.3** is when the Customer sees the result. The order endpoints (`GET /orders/{orderId}` and
`GET /orders/mine`) now load the local customer model alongside the `OrderStatusView` and return an
enriched response carrying `customerName`. If the local model is present, the name appears; if it is
not yet known, the field is `null`. The `OrderStatusView` projection is untouched — the enrichment
is a read-time wrapper, not a new projection.

The seeder ties this together for the demo: it registers a customer with the deterministic id
`"customer-demo"` (the same value the SPA's `useCurrentCustomer` stub sends on every request),
so the local model is populated at boot time, and the first `GET /orders/{orderId}` the demo user
makes sees a name, not a null.

## Journey scope

This narrative threads two Workshop 002 slices:

- **Slice 5.4 — Consume `CustomerRegistered` (Published Language).** The Orders service receives the
  PL event from Identity over RabbitMQ and upserts a `LocalCustomerView` document. This is when
  `CustomerRegistered` graduates from Identity-internal to `CritterMart.Contracts`.
- **Slice 5.3 — Resolve identity from `X-Customer-Id`.** The consumer (Orders) resolves the ambient
  customer id against its local model at read time, enriching order responses with `customerName`.

---

## Moment 1 — The Orders Service Learns About the Customer (Slice 5.4)

**Context.** The Identity service has recorded Ada as a customer (Narrative 006, Moment 1). Her
`CustomerRegistered { customerId: "c-ada", email: "ada@example.com", displayName: "Ada Lovelace" }`
is in the EF-Core outbox, waiting for the post-commit publish. The Orders service is running.
Nothing in Orders knows Ada's name.

The seeder, running at stack startup, called `POST /customers { id: "customer-demo", email:
"demo@crittermart.com", displayName: "Demo Customer" }`. That registration produced its own
`CustomerRegistered { customerId: "customer-demo", displayName: "Demo Customer" }`, published
through the same outbox.

**Interaction (system-triggered).** After the Identity transaction commits, the EF-Core outbox
publishes `CustomerRegistered` to RabbitMQ over conventional routing. Orders' Wolverine runtime,
subscribed to that exchange by virtue of having a `Handle(CustomerRegistered)` handler, receives
the message.

**System response.** `CustomerRegisteredHandler` in Orders handles the message. It writes (upserts)
a `LocalCustomerView { Id: "customer-demo", DisplayName: "Demo Customer" }` document into Orders'
own Marten document store, in the `orders` schema, within one transaction (Wolverine's
`AutoApplyTransactions` owns the commit). No call is made to the Identity service. If the same
message arrives twice (at-least-once delivery), the upsert is idempotent — `session.Store(...)` on
the same id overwrites the previous value, no error.

The `CustomerRegistered` type, previously in `CritterMart.Identity.Customers`, now lives in
`CritterMart.Contracts` — the shared published-language assembly both Identity (publisher) and
Orders (subscriber) reference. This is the moment it became a Published Language contract in the
operational sense, not just the modeled sense.

---

## Moment 2 — The Customer's Name Appears on Their Orders (Slice 5.3)

**Context.** The demo customer has placed an order. Their `OrderStatusView` in Marten carries
`CustomerId: "customer-demo"` — it has since Moment 1 of Narrative 004 — but until now, querying
`GET /orders/{orderId}` returned the view as-is, with no human-readable name attached. The
`LocalCustomerView { Id: "customer-demo", DisplayName: "Demo Customer" }` from Moment 1 now exists
in the Orders store.

**Interaction.** The storefront's W4 tracking screen requests `GET /orders/{orderId}`, or the "My
Orders" list requests `GET /orders/mine` with header `X-Customer-Id: customer-demo`.

**System response.** The endpoint loads the `OrderStatusView` by order id (the primary-key hit it
always made). Then — and this is new — it also loads `LocalCustomerView` for `customer-demo` (a
second primary-key hit, one scan, no join). Both are present. The endpoint wraps them into an
`EnrichedOrderView`: the same `{ id, customerId, status, lines, total, placedAt, cancelReason }`
as before, with `customerName: "Demo Customer"` appended. The `OrderStatusView` projection was not
touched — no events re-processed, no new stream consulted. The enrichment is applied only at the
endpoint, at read time.

For `GET /orders/mine`, the single `LocalCustomerView` load is shared across the whole list: all
orders in "my list" belong to the same customer, so one load serves every row. Each row in the
response list carries the same `customerName`.

**What the Customer sees.** On the W4 tracking screen, the order detail now reads "Demo Customer"
alongside the order's status and line items. The name matches what Identity recorded at registration.

**The degradation case.** If a request arrives for a customer whose `CustomerRegistered` has not yet
been delivered to Orders (a real possibility in the eventually-consistent PL model), `LoadAsync<LocalCustomerView>(...)`
returns `null`. The endpoint still returns `200` — `customerName` is simply `null` in the response.
No error is surfaced to the caller, and no synchronous call into Identity is attempted. The SPA
renders the customer id in place of the name until the message arrives and the local model catches up.

---

## What the Customer Does Not Yet See

- **The storefront does not yet display `customerName` from the response.** The backend now sends it;
  the frontend zod schema does not yet parse it. Displaying the name on W3/W4 is a follow-up frontend
  slice (additive: add `customerName: z.string().nullable().optional()` to `OrderStatusViewSchema`,
  render it).
- **No reverse lookup: Orders still does not call Identity's OHS endpoint.** `GET /customers/{id}` is
  the storefront-facing read (ADR 001's OHS-for-the-frontend / PL-for-the-backends split). The local
  model is how backends resolve customer data; a cross-service HTTP call is never the answer.
- **Customer profile updates are unmodeled.** If a customer changes their display name in Identity,
  a future `CustomerProfileUpdated` Published-Language event would drive an update to the local model.
  That lifecycle is out of scope for round two; the local model is updated only on `CustomerRegistered`
  for now.

---

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-06-18 | Initial version. Threads slices 5.3 and 5.4: the Orders service subscribes to `CustomerRegistered` (PL, now in `CritterMart.Contracts`) and upserts a consumer-local `LocalCustomerView`; order endpoints are enriched with `CustomerName?` at read time. Seeder registers `customer-demo` with a deterministic id to close the demo loop. Authored as the consolidated per-slice chain for implementation #034. |
