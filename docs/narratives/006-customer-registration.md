---
narrative: 006
title: The Customer Joins the Storefront
actor: Customer
status: draft
version: v1.0
slices: [5.1, 5.2]
references:
  - docs/workshops/002-identity-event-model.md (§ 2 Identity BC, § 4 event vocabulary, § 5 slices 5.1/5.2, § 6 GWT scenarios, § 7 read models)
  - docs/context-map/README.md (Identity → Open-Host Service + Published Language)
  - docs/vision.md (single-seller framing)
  - docs/decisions/009-polecat-deferred-for-round-one.md (Identity is a data store, not auth)
  - docs/decisions/001-separate-services-topology.md (no synchronous service-to-service HTTP)
  - openspec/changes/customer-registry/ (the machine-readable sibling)
---

# Narrative 006 — The Customer Joins the Storefront

A Customer in CritterMart is the person on the other side of the single-seller counter: they browse the catalog, fill a cart, and place orders. Before any of that is tied to a name, the storefront needs a place to record *who they are* — a row in the Identity registry. This narrative is that row's beginning: a customer joining the storefront, and the storefront later looking them up.

Workshop 002 refers to the trigger at the GWT level as *the storefront / signup*. In this narrative the actor is the **Customer**, because that is whose intent the registration captures; the storefront's signup form is the surface that issues `RegisterCustomer` on the customer's behalf. The two are interchangeable here — there is one actor and one intent.

A word on what Identity *is*, because it shapes every Moment below. Identity is CritterMart's **one non-event-sourced bounded context**. Catalog stores documents; Inventory and Orders store event streams; Identity stores a plain **row**. There is no stream to fold, no projection to rebuild — the `Customer` row *is* the read model. Its single `CustomerRegistered` "event" is not the source of truth and not a stream entry; it is an outbound notification, enrolled in the EF-Core **transactional outbox** alongside the row insert and published only after that insert commits. That is the teaching contrast this journey carries: *when current-state CRUD is the right shape, you store the row and publish a notification — you do not manufacture an event stream.*

## Journey scope

The Customer's registration journey threads two Workshop 002 slices, consolidated into one PR:

- **Slice 5.1 — Register a customer.** Moments 1 and 2 below: the happy-path signup, and the duplicate-email rejection (case-insensitive).
- **Slice 5.2 — Resolve a customer.** Moment 3 below: the storefront resolving a registered customer by id (the Open-Host Service read), and the unknown-id miss.

The future OHS/PL integration slices — **5.3** (resolving the `X-Customer-Id` header against a consumer-local model) and **5.4** (a cross-BC consumer subscribing to `CustomerRegistered`) — are modeled in Workshop 002 but not built here; they are named under *What the Customer does not yet see* and land when a consumer genuinely needs customer data.

## Moment 1 — Registering for the first time

**Context.** A new visitor decides to create an account on the CritterMart storefront. They are `Ada Lovelace`, and they sign up with the email `ada@example.com`. No customer has been registered for that email before.

**Interaction.** The storefront's signup surface collects the email `ada@example.com` and the display name `Ada Lovelace` and issues `RegisterCustomer { email: "ada@example.com", displayName: "Ada Lovelace" }` to the Identity service at `POST /customers`.

**System response.** Identity normalizes the email (trimmed and lowercased), confirms no customer already holds it, mints an opaque customer id and a `registeredAt` timestamp, and writes a `Customer` row into the `identity` schema. In the **same transaction**, it cascades `CustomerRegistered { customerId, email, displayName }` into the EF-Core outbox; once the insert commits, that message is published to RabbitMQ. The response is `201 Created` with `Location: /customers/{id}` — the address the storefront can resolve the customer at (Moment 3).

Two details are the teaching payoff. First, the handler never calls `SaveChanges` by hand: Wolverine's transactional middleware owns the commit, exactly as the Marten endpoints never commit by hand — the same outbox guarantee, a different store. Second, **nothing consumes `CustomerRegistered` yet.** It publishes to its own RabbitMQ exchange with no bindings — the **Published-Language** relationship is *declared* in the context map but carries no traffic. That is correct, not a loose end: until a bounded context genuinely needs customer data (slice 5.4), keeping the event Identity-local avoids a premature shared contract. The message is on the wire so the outbox beat is real; it simply has no listener.

## Moment 2 — Catching a duplicate email

**Context.** Some time later, a registration arrives for an email that is already on file — perhaps Ada returning to sign up a second time, perhaps a typo-driven double submit. This time the email is typed `Ada@Example.com`: same address, different casing.

**Interaction.** The storefront issues `RegisterCustomer { email: "Ada@Example.com", displayName: "Ada L." }` to `POST /customers`.

**System response.** Identity normalizes the email to `ada@example.com` and finds a customer already registered for it. The command is rejected with `CustomerAlreadyRegistered` (`409 Conflict`). No new `Customer` row is written; no `CustomerRegistered` event is published; the existing row is untouched. The failure is idempotent — a retry, intentional or accidental, never produces a second customer for the same address.

The rejection is enforced **twice over**, and the redundancy is deliberate. A railway-style `ValidateAsync` guard runs before the handler and returns the friendly `409` for the common case (mirroring how Catalog rejects a duplicate SKU with `ProductAlreadyPublished`). Behind it, a **unique database index** on the normalized email column is the true backstop: even two registrations racing in at the same instant — both passing the application check before either commits — cannot both land, because the index rejects the second insert. Catalog never needs this extra index, because a product's SKU *is* its Marten document id and the primary key enforces uniqueness for free; Identity keys uniqueness on email, which is **not** the primary key, so the index is what makes Identity's guarantee as strong as Catalog's. Normalizing the email is what makes the guard honest in the first place — a uniqueness check that `Ada@` could slip past `ada@` would not be a uniqueness check at all.

## Moment 3 — The storefront looks the customer up

**Context.** With Ada registered, the storefront needs to render her details — her display name beside her cart, say. It holds the customer id minted in Moment 1.

**Interaction.** The storefront requests `GET /customers/{id}` against the Identity service.

**System response.** Identity performs a straight primary-key lookup against the `customers` row — no projection, no read model to consult, because the row *is* the read model — and returns `200 OK` with `{ id, email, displayName, registeredAt }`. If the id is unknown (`GET /customers/nope`), it returns `404 Not Found`.

This read is Identity's **Open-Host Service**: a stable, published read API for the **storefront**, exactly as the storefront reads Catalog's products over HTTP. It is emphatically **not** how other *services* resolve a customer. Per [ADR 001](../decisions/001-separate-services-topology.md), CritterMart forbids synchronous service-to-service HTTP; a backend that needs customer data subscribes to the `CustomerRegistered` Published-Language event and keeps its own local model (slice 5.4), then reads that model (slice 5.3) — it never calls into Identity. The clean split is **OHS-for-the-frontend, Published-Language-for-the-backends**, and it is the load-bearing reconciliation of Identity's OHS+PL relationship with the no-sync-HTTP rule.

## What the Customer does *not* yet see

Three non-events are named here because each is a deliberate round-one boundary, not an oversight:

- **Registering creates no login.** Identity is a *data store, not an auth provider* ([ADR 009](../decisions/009-polecat-deferred-for-round-one.md)). `RegisterCustomer` writes a registry row; it mints no credentials, no session, no claims. There is no Polecat. The customer is *recorded*, not *authenticated*.
- **The `X-Customer-Id` seam is not yet wired to the registry.** Round-one identity still arrives ambiently as a hardcoded id on the `X-Customer-Id` header. Resolving that header against this registry — turning the ambient id into a real customer — is the future slice 5.3, and it will read a consumer-local model, not call Identity synchronously.
- **`CustomerRegistered` reaches no consumer.** The event publishes to RabbitMQ unconsumed (the Published-Language edge is declared, not trafficked). The first consumer — likely Orders, enriching `OrderStatusView` with a customer's display name — is the future slice 5.4, and that is the moment `CustomerRegistered` graduates from an Identity-local record to a `CritterMart.Contracts` published type.

## Forthcoming Moments

When the OHS/PL integration slices are authored, this journey (or a consumer BC's journey) grows further Moments:

- **A backend resolves a customer (slice 5.3 / 5.4).** A consuming BC subscribes to `CustomerRegistered`, upserts a local customer read model, and resolves `X-Customer-Id` against that model — never a sync call into Identity. When that lands, this narrative's `slices` frontmatter grows, the version bumps, and `## Document History` records the amendment.
- **Customer lifecycle beyond registration.** Profile edits (`CustomerProfileUpdated`), deactivation, and merge are unmodeled (Workshop 002 § 8). A registry that only inserts is round-two-minimal.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                                                                                                  |
| ------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| v1.0    | 2026-06-18 | Initial commit. Covers Workshop 002 slice 5.1 (Register — happy path + duplicate-email rejection, case-insensitive normalized) and slice 5.2 (Resolve — Open-Host Service read + 404). Names the teaching contrast (the one non-event-sourced BC; the row is the read model; `CustomerRegistered` is an outbox notification, not a stream event) and the OHS-for-frontend / PL-for-backends split. Authored as the human-readable sibling of the `customer-registry` OpenSpec change, consolidating both slices into one PR. The spike on `spike/efcore-identity` @ `0ffe42e` is the reference implementation. |
