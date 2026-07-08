---
workshop: 002
title: CritterMart Identity Bounded Context — Event Model (spike promotion)
scope: The Identity bounded context, promoted from a round-one stub to a kept EF-Core customer registry (the spike on `spike/efcore-identity`). Names the Open-Host Service + Published Language relationship to the other BCs. v1.1 adds the `EmailChange` saga (Saga #2) — Identity's first stateful consumer, EF-Core-backed, additive per ADR 022. v1.2 corrects § 8 item 10's CritterWatch-visibility expectation against empirical findings. v1.3 adds the **authentication increment** (slices 5.8–5.11) — real auth via ASP.NET Core Identity issuing a self-validated JWT, per ADR 023, superseding the earlier "NOT authentication (ADR 009)" scope; still non-event-sourced (a relational user store extending the boring-CRUD foil, not an event stream).
status: Design pass for the spike promotion, plus the Saga #2 forward increment (shipped, PR #121) and the v1.3 authentication increment (modeled-not-built). Slices 5.1/5.2 are realized by the spike; the code re-lands on main via a later OpenSpec proposal + narrative + implementation prompt. Slices 5.3/5.4 (OHS/PL integration) are modeled-not-built future increments. Slices 5.5–5.7 (the `EmailChange` saga) are implemented and live-verified. Slices 5.8–5.11 (ASP.NET Core Identity auth) are modeled here (v1.3, ADR 023), not yet implemented.
version: v1.3
date: 2026-07-07
participants: session-runner in solo multi-persona mode (Facilitator, Domain Expert, Architect, Backend Developer, QA, Product Owner)
references:
  - docs/vision.md
  - docs/context-map/README.md
  - docs/workshops/001-crittermart-event-model.md
  - docs/skills/event-modeling/SKILL.md
  - docs/decisions/001-separate-services-topology.md
  - docs/decisions/003-wolverine-rabbitmq-transport.md
  - docs/decisions/009-polecat-deferred-for-round-one.md
  - docs/decisions/022-convention-sagas-additive-to-pmvh.md
  - docs/decisions/023-real-authentication-for-identity.md
  - docs/research/wolverine-saga-feasibility.md
  - spike/efcore-identity @ 0ffe42e (the reference implementation)
  - src/CritterMart.Inventory/Stock/Replenishment.cs (Saga #1 reference implementation)
---

# Workshop 002 — CritterMart Identity Bounded Context (Spike Promotion)

## 1. Scope

**In scope.** The Identity bounded context, promoted from its round-one stub (a hardcoded frontend customer id, [ADR 009](../decisions/009-polecat-deferred-for-round-one.md)) to a kept, deployed service: a deliberately boring EF-Core customer registry. This workshop models that BC's event, command, read model, slices, and GWT scenarios, and names its strategic-design relationship to the other BCs (**Open-Host Service + Published Language**). The reference implementation is the spike on `spike/efcore-identity` (`0ffe42e`), which realizes slices 5.1 and 5.2.

**The spike inverted the pipeline — this workshop re-establishes the trace.** A spike is throwaway, so it legitimately built before any design artifact existed. Promotion re-asserts the discipline: this workshop is the design layer the kept code must trace back to, and the spike code re-lands on `main` later through an OpenSpec proposal → narrative → implementation prompt chain that references this model. This is also the **design-return interleave** the cadence rule had flagged as due.

**Identity stays a DATA STORE, not auth.** Per ADR 009, this promotion does NOT introduce Polecat, authentication, or authorization. Identity is a customer registry — a row per customer — nothing more. The round-one `X-Customer-Id` seam remains the identity transport; a future slice (5.3) resolves that header against this registry's published data. *(v1.3 supersedes the "not auth" half only: [ADR 023](../decisions/023-real-authentication-for-identity.md) adds real authentication via ASP.NET Core Identity — see the v1.3 amendment below and slices 5.8–5.11. The **data-store / non-event-sourced** framing in this paragraph still holds; auth is more boring relational CRUD, not an event stream.)*

**The teaching contrast.** Identity is the ONLY non-event-sourced BC. Where Catalog persists documents (+ lifecycle events) and Inventory/Orders persist event streams, Identity persists current state in a row. Its one "event," `CustomerRegistered`, is NOT a stream event and NOT the source of truth — it is an outbound integration notification published from a state change through the EF-Core transactional outbox. That is the workshop's pedagogical payoff: Event Modeling a CRUD/registry BC where the event is a published-language notification rather than the system of record, proving Wolverine's handler model (command → handler → outbound message, same transactional outbox) is persistence-agnostic.

**Deferred / future increments.** Slices 5.3 (resolve identity from `X-Customer-Id`) and 5.4 (a cross-BC consumer of `CustomerRegistered`) are modeled here but NOT built by the spike — they are the OHS/PL integration roadmap. Profile management beyond registration and a Polecat backing remain long-road parking-lot items. *(v1.3: **authentication is no longer long-road** — it is chosen, mechanism-settled by [ADR 023](../decisions/023-real-authentication-for-identity.md), and modeled as slices 5.8–5.11 below.)*

**v1.1 amendment — the `EmailChange` saga (Saga #2).** [ADR 009's second amendment](../decisions/009-polecat-deferred-for-round-one.md) settled that Identity's "deliberately boring CRUD" stance holds even as it grows a stateful consumer; [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md) is the binding guard this amendment designs against — the saga does relational things the Wolverine way (a `DbSet`-mapped, string-keyed `Saga`-derived entity) and must not drift into re-implementing event sourcing on SQL. This amendment adds slices **5.5–5.7**: a customer requests an email change, has a confirm-or-expire window to confirm it, and the change applies or silently drops. It is the smallest honest multi-step workflow Identity has — a real confirm-or-expire window, unlike a name/address edit's single mutating command (see [`docs/research/wolverine-saga-feasibility.md`](../research/wolverine-saga-feasibility.md) § Candidate #2 — "don't invent a workflow" governs this amendment's scope). **Not yet implemented** — this workshop pass is the design-return that precedes the sibling OpenSpec proposal, narrative, and implementation prompt.

**v1.3 amendment — real authentication (ADR 023).** This amendment **closes the auth stub** the v1.0 scope deferred ("Identity stays a DATA STORE, not auth" above was v1.0's stance; [ADR 023](../decisions/023-real-authentication-for-identity.md) supersedes its *auth* half — the data-store half still holds). Identity gains real authentication via **ASP.NET Core Identity** (`UserManager`/`SignInManager`, EF-Core-backed on the existing `identity` schema) and becomes the system's sole **auth issuer**: on a successful password check it mints a self-validated, asymmetrically-signed **JWT** carrying the customer id in the `sub` claim, which the SPA presents to all four services and Catalog/Inventory/Orders verify **offline** against Identity's config-distributed public key. This adds slices **5.8–5.11**: register with credentials, log in (issue the JWT), verify a token at a resource server, and log out. **Two framings survive unchanged and are load-bearing here:** (1) auth is *still non-event-sourced* — ASP.NET Core Identity is a relational user store, so this **extends** the boring-CRUD foil (ADR 009 amendment / ADR 022), it does not reverse it; there are still **zero stream events** and **no saga** in this increment. (2) The **no-sync-HTTP non-negotiable is honored, not bent** — offline JWT validation against a config public key means no HTTP call into Identity, per-request or at startup (see § 3's new auth architect note). The `X-Customer-Id` seam retires as the trust boundary, replaced by the `sub` claim; the `useCurrentCustomer` frontend seam sources it from the authenticated session. **Not yet implemented** — this pass is the design-return preceding the sibling OpenSpec proposal, narrative, and implementation prompt.

---

## 2. Bounded-Context Summary

### Identity (deployed — EF-Core customer registry; NOT event-sourced)

Identity is a deployed Wolverine service backed by Entity Framework Core (Npgsql) on the shared Postgres, in its own `identity` schema (schema-per-service, ADR 002). It is the sole non-event-sourced BC: the `Customer` row is the source of truth — no stream, no projection, no fold. A customer is created by `RegisterCustomer` and read by id. The service publishes `CustomerRegistered` to RabbitMQ through the EF-Core transactional outbox (the same outbox guarantee as the Marten services, a different store). It is an **Open-Host Service** — it exposes `GET /customers/{id}` for external (storefront) callers — and its `CustomerRegistered` event is a **Published Language** other BCs may subscribe to. It performs NO authentication or authorization (ADR 009); identity arrives ambiently on the `X-Customer-Id` seam. *(v1.3: superseded by the authentication increment — [ADR 023](../decisions/023-real-authentication-for-identity.md) makes Identity the auth issuer; see the **Authentication via ASP.NET Core Identity** subsection below. Round-one code still uses the `X-Customer-Id` seam until slices 5.8–5.11 ship; the `sub` claim replaces it then.)*

**The EmailChange saga (v1.1, not yet implemented).** Slices 5.5–5.7 add an `EmailChange` saga keyed by `CustomerId`: a `RequestEmailChange` opens the saga (holding `PendingEmail`) and schedules an `EmailChangeTimeout`; a `ConfirmEmailChange` within the window applies the change to the `Customer` row; the timeout drops it if no confirmation arrives. This is Identity's **first stateful consumer** — CritterMart's *second* convention `Wolverine.Saga` (contrast Inventory's Marten-backed `Replenishment`, Workshop 001 slices 2.5–2.7) — proving the saga store itself is swappable (EF Core vs. Marten), per [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md). The saga state lives in EF-Core saga storage (a `DbSet<EmailChange>` on `IdentityDbContext`), never on an event stream — Identity's registry framing is unchanged. See § 4's new saga-state subsection and slices 5.5–5.7 below.

**Authentication via ASP.NET Core Identity (v1.3, not yet implemented).** Slices 5.8–5.11 make Identity the system's **auth issuer** ([ADR 023](../decisions/023-real-authentication-for-identity.md)). It layers **ASP.NET Core Identity** onto the same EF-Core/`identity`-schema store — `UserManager` for credential creation, `SignInManager` for password checks — and, on a successful login, mints a standard **JWT** signed with an asymmetric private key only Identity holds, carrying the customer id in the `sub` claim. The SPA presents that JWT as `Authorization: Bearer …` to **all four** services; Catalog, Inventory, and Orders configure `AddJwtBearer` with Identity's **public key distributed as configuration** and validate the token **fully offline** — no HTTP into Identity, per-request or at startup. This is the clean **issuer / resource-server** split: only Identity can *mint*; the others can only *verify*. Crucially for this BC's teaching thesis, auth introduces **no event sourcing and no saga** — ASP.NET Core Identity is a relational user store, so it *extends* the boring-CRUD foil (ADR 009 amendment / ADR 022) rather than reversing it. The relationship pattern the context map now names: auth extends Identity's **Open-Host Service + Published Language** posture (the JWT claim shape + public key are a published contract), with the resource servers **Conformist** to the token shape — but validated offline, so it adds **no runtime integration edge** and no sync HTTP. See § 4's new auth subsection, § 3's auth architect note, and slices 5.8–5.11 below.

---

## 3. Timeline / Flow

Identity has no multi-step journey of its own (contrast the Place Order storyboard in Workshop 001 § 3). Its flow is a single state change with two reader shapes:

```mermaid
graph LR
    UI["Storefront / signup"] -->|"RegisterCustomer (5.1)"| ID["Identity (EF Core)"]
    ID -->|writes| ROW["Customer row<br/>(identity.customers)"]
    ID -.->|"CustomerRegistered (Published Language)<br/>via EF outbox → RabbitMQ"| BUS["RabbitMQ"]
    BUS -.->|"(5.4, future) subscribe"| CONS["Consuming BC<br/>local customer read model"]
    ROW -->|"GET /customers/{id} (OHS, 5.2)"| SPA["Storefront read"]
    SPA2["Orders / Catalog"] -.->|"(5.3, future) resolve X-Customer-Id<br/>against the LOCAL model, NOT a sync call"| CONS
```

Solid edges are realized by the spike (5.1 write, 5.2 read). Dashed edges are the OHS/PL integration roadmap (5.3/5.4) — declared but not yet trafficked.

**v1.1 note.** Slices 5.5–5.7 (the `EmailChange` saga) are a separate, additive flow off the `Customer` row — a request-then-confirm-or-expire window, not shown on the diagram above (which covers the registration/read flow only). See § 4's saga-state subsection below.

**Architect note — the no-sync-HTTP constraint ([ADR 001](../decisions/001-separate-services-topology.md)).** The OHS read API (`GET /customers/{id}`) is for EXTERNAL callers — the storefront, exactly as it reads Catalog over HTTP. It is NOT how other services resolve identity: ADR 001/003 forbid synchronous service-to-service HTTP. A cross-BC consumer that needs customer data subscribes to the `CustomerRegistered` Published-Language event and maintains its own local read model (slice 5.4); slice 5.3's header resolution then reads that local model, never a sync call into Identity. This is the clean **OHS-for-the-frontend / PL-for-the-backends** split, and it is the load-bearing reconciliation of the owner's OHS+PL choice with ADR 001.

**Architect note (v1.3) — the auth trust boundary honors the same no-sync-HTTP constraint ([ADR 023](../decisions/023-real-authentication-for-identity.md)).** With real auth, the question becomes: how do Catalog/Inventory/Orders *trust* a caller's identity without calling back into Identity to check a token? The answer is the auth analogue of the OHS/PL split above. Identity mints a JWT signed with an **asymmetric private key it alone holds**; each resource server validates that token **offline**, against Identity's **public key distributed as configuration** — so there is **no HTTP into Identity, per-request or even once at startup** (the public key is config, not a fetched JWKS document). The token travels *with the browser's HTTP request* (`Authorization: Bearer …`, like every storefront call), never on RabbitMQ; where customer identity crosses a service boundary over the bus (e.g. `ReserveStock`, Orders→Inventory), it rides as ordinary message payload, not a token. Net: real auth adds **zero** synchronous service-to-service HTTP and **zero** new cross-BC runtime edges — the strongest possible reading of ADR 001 holds. This is the workshop's auth teaching payoff, and it is why the chosen mechanism is a *self-validated* JWT (asymmetric, config public key) rather than a cookie, an opaque `MapIdentityApi` bearer, or a JWKS-fetched key (each of which would reintroduce shared keys, a BFF, or a call into Identity — see [ADR 023](../decisions/023-real-authentication-for-identity.md)'s rejected alternatives).

---

## 4. Event Vocabulary

### Identity

- **CustomerRegistered** — a customer was added to the registry. Published-Language integration event carrying `{ customerId, email, displayName }`, emitted from the `RegisterCustomer` state change via the EF-Core transactional outbox. It is NOT a stream event and NOT the source of truth — the `Customer` row is. It lives *inside* the Identity service today (nothing consumes it); it graduates to `CritterMart.Contracts` when slice 5.4 lands a consumer. (Future: **CustomerProfileUpdated**, when profile edits are modeled.)

Identity has no other events. It is a registry, not an event-sourced aggregate.

### Saga state and saga messages (v1.1, NOT events on any stream)

The `EmailChange` saga (slices 5.5–5.7) introduces **saga state**, not stream events — Identity remains a registry with zero stream events regardless of this amendment. Per [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md), this state is EF Core doing relational things the Wolverine way, the direct EF-Core-backed counterpart to Inventory's Marten-backed `Replenishment` saga (Workshop 001 § 4):

- **EmailChange** *(saga state, not a stream)* — one open instance per customer with a pending email change, keyed by `CustomerId`. Holds `PendingEmail` while the confirm-or-expire window is open. `MarkCompleted()` fires on a successful confirm (slice 5.6) or an unconfirmed timeout (slice 5.7).
- **RequestEmailChange** — command; opens the saga, or updates an already-open one's `PendingEmail` to the newest request (slice 5.5). The original `EmailChangeTimeout` is **not** rescheduled — Wolverine offers no scheduled-message cancellation, so a second timeout would either race the first or require tracking which one is authoritative. Mirroring `Replenishment`'s re-open branch (which schedules no second `ReplenishTimeout` for the identical reason), the confirm window is anchored to the *first* request in a re-request sequence, not the latest.
- **ConfirmEmailChange** — command; applies the pending email if the window is still open and the email hasn't since been claimed by another registration (`ux_customers_email`) (slice 5.6).
- **EmailChangeTimeout** — scheduled when the saga opens (config-driven duration — mirrors `ReplenishDeadline`'s config-singleton pattern, `src/CritterMart.Inventory/Stock/ReplenishDeadline.cs`); if unconfirmed by the deadline, drops the pending change and completes the saga (slice 5.7).

### Auth commands and the issued token (v1.3, NOT events, NOT saga state)

The authentication increment (slices 5.8–5.11, [ADR 023](../decisions/023-real-authentication-for-identity.md)) introduces **commands and an issued token**, not stream events and not saga state. Identity remains a registry with **zero stream events** and **no saga in this increment** regardless of this amendment — ASP.NET Core Identity is a relational user store (its own tables: users, credentials), and the JWT is a stateless artifact, not persisted domain state.

- **RegisterWithCredentials** — command; the credentialed evolution of slice 5.1's `RegisterCustomer`. Creates the ASP.NET Core Identity user (`UserManager.CreateAsync`, password hashed) *and* the registry `Customer` row in one flow, keyed by the same string id so the `Customer.Id` and the Identity user id align (and `CustomerRegistered` still publishes as before). (slice 5.8)
- **LogIn** — command; `SignInManager.CheckPasswordSignInAsync` verifies the password and, on success, Identity **mints a signed JWT** carrying `sub` = the customer id (plus issuer, audience, and a lifetime). Returned to the SPA; not persisted. (slice 5.9)
- **the JWT** *(issued token, not an event, not saga state)* — a standard, asymmetrically-signed JSON Web Token. Identity holds the **private** signing key (mint); Catalog/Inventory/Orders hold the **public** key as config (verify offline). Its `sub` claim is what replaces the `X-Customer-Id` header as the trust boundary. It lives entirely in transit (browser ↔ services) — nothing writes it to a stream, a row, or a saga.
- **LogOut** — command (or pure client action); because the JWT is stateless, round-one logout is **client-side token discard**. Server-side revocation (a denylist, or short access tokens + a refresh flow) is a deferred future increment (§ 8). (slice 5.11)

Token *verification* at a resource server (slice 5.10) is a `*(system)*`, code-in-the-consumer concern — the same "Identity → consumer" shape as slices 5.3/5.4 — not an Identity command: each of Catalog/Inventory/Orders configures `AddJwtBearer` and validates the inbound token offline, reading `sub`.

---

## 5. Slice Table

Columns per Workshop 001 § 5. `*(query)*` = read-only; `*(system)*` = triggered by an upstream event/command. The BC column reads `Identity → consumer` where a slice is Identity's contract but its code lands in a *consuming* BC.

| #   | Slice                                                | Command                          | Events                              | View / Read model                          | BC                  | Reads-from                                  | Writes-to                                                   | Priority          |
| --- | ---------------------------------------------------- | -------------------------------- | ----------------------------------- | ------------------------------------------ | ------------------- | ------------------------------------------- | ---------------------------------------------------------- | ----------------- |
| 5.1 | Register a customer                                  | `RegisterCustomer`               | `CustomerRegistered` (PL, outbox)   | `Customer` row                             | Identity            | `Customer` (duplicate-email guard)          | `Customer` row; `CustomerRegistered` (outbox → RabbitMQ)   | P0 (spike)        |
| 5.2 | Resolve a customer *(query)*                         | *(query)*                        | —                                   | `Customer` row                             | Identity            | `Customer` row                              | —                                                          | P0 (spike)        |
| 5.3 | Resolve identity from `X-Customer-Id` *(future)*     | *(system)* header → customer     | —                                   | consumer-local customer read model         | Identity → consumer | consumer-local model (from 5.4)             | —                                                          | P2 (future)       |
| 5.4 | Consume `CustomerRegistered` *(future, PL)*          | *(system)* via PL subscription   | — (consumer-local)                  | consumer-local customer read model         | Identity → consumer | `CustomerRegistered` (Published Language)   | consumer-local read model (upsert)                         | P2 (future)       |
| 5.5 | Request an email change *(saga)*                     | `RequestEmailChange`             | — *(opens `EmailChange` saga, or updates `PendingEmail` on an open one; no stream event, no row change yet)* | —                     | Identity            | `Customer` row exists for `CustomerId` (guard); email not already registered to another customer | `EmailChange` saga state (`PendingEmail`); `EmailChangeTimeout` scheduled **once, on open** — a re-request updates `PendingEmail` only, no second timeout | P1 (v1.1, not yet implemented) |
| 5.6 | Confirm an email change within the window *(saga)*   | `ConfirmEmailChange`             | — *(applies `Customer.Email`; completes `EmailChange` saga — no stream event, Identity is not event-sourced)* | `Customer` row (email) | Identity            | `EmailChange` saga state; `Customer` row (`ux_customers_email` recheck) | `Customer` row (`Email` updated); `EmailChange` saga `MarkCompleted` | P1 (v1.1, not yet implemented) |
| 5.7 | Drop an email change on timeout *(saga)*             | *(scheduled)* `EmailChangeTimeout` self-message | — *(completes `EmailChange` saga)* | —                     | Identity            | `EmailChange` saga state                    | `EmailChange` saga `MarkCompleted` (dropped, no row change) | P1 (v1.1, not yet implemented) |
| 5.8 | Register with credentials *(auth)*                   | `RegisterWithCredentials`        | — *(no stream event; creates ASP.NET Identity user + `Customer` row; `CustomerRegistered` still published via outbox)* | `Customer` row + Identity user | Identity | `Customer`/user (duplicate-email guard: `ux_customers_email` + Identity's own uniqueness) | `Customer` row; ASP.NET Identity user (hashed credential); `CustomerRegistered` (outbox → RabbitMQ) | P1 (v1.3, not yet implemented) |
| 5.9 | Log in — issue a JWT *(auth)*                        | `LogIn`                          | — *(no stream event; mints a signed JWT, not persisted)* | the issued **JWT** (`sub` = customerId) | Identity | ASP.NET Identity user (password check via `SignInManager`) | — *(no row change; signed token returned to caller)* | P1 (v1.3, not yet implemented) |
| 5.10 | Verify a token at a resource server *(auth, system)* | *(system)* `Authorization: Bearer` → validated offline | — | authenticated principal (`sub` → customerId) | Identity → consumer | Identity's **public key (config)**; the inbound JWT | — *(no write; `401` on invalid/expired; no HTTP into Identity)* | P1 (v1.3, not yet implemented) |
| 5.11 | Log out *(auth)*                                    | `LogOut` *(client-side token discard)* | — | — | Identity → frontend | — | — *(stateless JWT; server-side revocation deferred, § 8)* | P1 (v1.3, not yet implemented) |

**Slice count.** Identity: 11 — two realized by the spike (5.1 / 5.2), two future OHS/PL integration slices whose code lands in consuming BCs (5.3 / 5.4), three v1.1 `EmailChange` saga slices (5.5 / 5.6 / 5.7 — Saga #2, not yet implemented), four v1.3 authentication slices (5.8–5.11 — ASP.NET Core Identity + self-validated JWT, not yet implemented).

**Slice priority distribution.** P0: 2 (5.1, 5.2). P1: 7 (5.5–5.7 the `EmailChange` saga; 5.8–5.11 the auth increment). P2: 2 (5.3, 5.4 — future OHS/PL integration).

**Pattern note.** No Klefter (translation-decision) adjunct pattern applies — Identity has no external decision to localize. Bruun temporal-automation also does not apply to the `EmailChange` saga: `EmailChangeTimeout` is convention-saga machinery (a `TimeoutMessage` advancing *saga* state, exactly like Inventory's `ReplenishTimeout`), not a todo-list-projection self-message advancing a *stream* (contrast Bruun's `CartActivityTimeout` / `OrderPaymentTimeout` in Workshop 001). The convention **Wolverine Saga** pattern is cited on slices 5.5–5.7 via the `*(saga)*` marker — CritterMart's *second* use of the `Wolverine.Saga` base class (per [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md)), EF-Core-backed in contrast to Inventory's Marten-backed `Replenishment`. Registration/read (5.1–5.4) still turn on the **persistence-agnostic transactional outbox**, unaffected by this amendment.

**Pattern note (v1.3 — auth slices).** The authentication slices (5.8–5.11, `*(auth)*` marker) introduce **no event-modeling adjunct pattern**: no stream event, no saga, no timeout, no projection. They are plain command (5.8/5.9/5.11) and system-validation (5.10) slices, exactly the shape a non-event-sourced BC produces — ASP.NET Core Identity is relational user-store CRUD, and the JWT is a stateless issued artifact. The *only* new architectural motif is the **issuer / resource-server** split (Identity mints, the others verify offline), which is a deployment-and-trust concern, not an aggregate/stream concern. That the increment adds no ES machinery is itself the teaching point: real authentication does not require an event store — it is the "when CRUD is fine" thread (Catalog) applied to credentials. Slice 5.10's code lands in the *consuming* BCs (Catalog/Inventory/Orders), the same "Identity → consumer" shape as 5.3/5.4.

---

## 6. GWT Scenarios

### 5.1 Register a customer — `RegisterCustomer`

**Happy path.**
- **Given** no customer exists for email `ada@example.com`.
- **When** the storefront issues `RegisterCustomer { email: "ada@example.com", displayName: "Ada Lovelace" }` at `POST /customers`.
- **Then** a `Customer` row is written (server-minted `id`, `registeredAt` = now), the response is `201 Created` with `Location: /customers/{id}`, and `CustomerRegistered { customerId, email, displayName }` is enrolled in the EF-Core outbox **in the same transaction** and published after the commit succeeds.

**Failure path — duplicate email (kept-service guard; NOT in the spike).**
- **Given** a customer already exists for email `ada@example.com`.
- **When** `RegisterCustomer { email: "ada@example.com", ... }` is issued again.
- **Then** the command is rejected with `CustomerAlreadyRegistered` — no row written, no event published, idempotent. **Note:** the spike performs no uniqueness guard (it inserts a new row every time); this is the first kept-service increment over the spike, to be specified in slice 5.1's OpenSpec proposal (including the uniqueness key).

### 5.2 Resolve a customer — *(query)*

**Happy path.**
- **Given** a customer `{ id: "c-1", email, displayName, registeredAt }` is registered.
- **When** the caller requests `GET /customers/c-1`.
- **Then** `200 OK` with `{ id, email, displayName, registeredAt }`.

**Failure path — unknown id.**
- **Given** no customer with id `nope`.
- **When** the caller requests `GET /customers/nope`.
- **Then** `404 Not Found`.

### 5.3 Resolve identity from `X-Customer-Id` — *(future, Open-Host Service)*

**Happy path (modeled, not built).**
- **Given** a consuming BC holds a local customer read model (populated by slice 5.4) including `c-1`.
- **When** a request arrives at that BC carrying `X-Customer-Id: c-1`.
- **Then** the consumer resolves `c-1` against its **local** model — never a synchronous call into Identity (ADR 001) — to enrich its behavior (e.g., the customer's display name on an order). If the local model lacks `c-1`, the consumer degrades gracefully (renders the id), since the PL event is eventually consistent.

### 5.4 Consume `CustomerRegistered` — *(future, Published Language)*

**Happy path (modeled, not built).**
- **Given** Identity publishes `CustomerRegistered { customerId: "c-1", email, displayName }`.
- **When** the consuming BC's subscriber handles it.
- **Then** the consumer upserts `c-1` into its local customer read model. `CustomerRegistered` moves to `CritterMart.Contracts` (Published Language) when this slice lands — until a consumer exists, keeping it Identity-local is correct (no premature shared contract).

### 5.5 Request an email change — `RequestEmailChange` *(saga, v1.1)*

**Happy path.**
- **Given** customer `c-1` is registered with email `ada@example.com` and has no open `EmailChange` saga.
- **When** the storefront issues `RequestEmailChange { customerId: "c-1", newEmail: "ada.new@example.com" }`.
- **Then** an `EmailChange` saga opens (`Id = "c-1"`, `PendingEmail = "ada.new@example.com"`) and an `EmailChangeTimeout` is scheduled for the configured deadline. `Customer.Email` is **unchanged** until confirmation (slice 5.6).

**Failure path — new email already registered to another customer.**
- **Given** `taken@example.com` is already the registered email of customer `c-2`.
- **When** customer `c-1` issues `RequestEmailChange { customerId: "c-1", newEmail: "taken@example.com" }`.
- **Then** the command is rejected (`EmailAlreadyRegistered`, mirroring `RegisterCustomer`'s duplicate-email guard) — no saga opens, no timeout scheduled. This is an application-level check, racy on its own; the confirm-time recheck (slice 5.6) is the true backstop against a same-window race.

**Failure path — unknown customer.**
- **Given** no customer exists with id `ghost-1`.
- **When** `RequestEmailChange { customerId: "ghost-1", newEmail: "new@example.com" }` is issued.
- **Then** the command is rejected (`CustomerNotFound`) — no saga opens, no timeout scheduled. Without this guard, the illustrative saga shape (`wolverine-saga-feasibility.md` § Candidate #2) would open a saga for a customer that doesn't exist and null-reference at confirm time (`db.Customers.FindAsync(Id)` returning `null`).

**Edge path — re-request while a change is already pending.**
- **Given** customer `c-1` already has an open `EmailChange` saga with `PendingEmail = "ada.new@example.com"`, opened at `t0` with its `EmailChangeTimeout` still scheduled for `t0 + deadline`.
- **When** `c-1` issues a second `RequestEmailChange { customerId: "c-1", newEmail: "ada.newer@example.com" }` at `t1` (`t0 < t1 < t0 + deadline`).
- **Then** the existing saga's `PendingEmail` becomes `"ada.newer@example.com"` — but the **original** `EmailChangeTimeout` (still armed for `t0 + deadline`) is left in place, NOT rescheduled: Wolverine offers no scheduled-message cancellation, so a naive "schedule a fresh timeout on every re-request" design would leave the first timeout armed and firing early against the re-armed window. Net effect: the confirm window for a re-requested email is the *original* request's remaining time, not a full fresh window — mirroring `Replenishment`'s `StartOrHandle` re-open branch, which for the identical reason schedules no second `ReplenishTimeout` on re-open. The original pending email is abandoned without a trace beyond the saga's current state.

### 5.6 Confirm an email change within the window — `ConfirmEmailChange` *(saga, v1.1)*

**Happy path.**
- **Given** customer `c-1`'s `EmailChange` saga is open with `PendingEmail = "ada.new@example.com"` and the deadline has not passed.
- **When** `c-1` issues `ConfirmEmailChange { customerId: "c-1" }`.
- **Then** `Customer.Email` is set to `"ada.new@example.com"` (normalized, same as `RegisterCustomer`) and the `EmailChange` saga completes (`MarkCompleted()`, state deleted).

**Failure path — window already expired.**
- **Given** customer `c-1`'s `EmailChange` saga completed via timeout (slice 5.7) before confirmation arrived.
- **When** `ConfirmEmailChange { customerId: "c-1" }` arrives late.
- **Then** it is a silent no-op (`NotFound(ConfirmEmailChange)`, mirroring `Replenishment`'s `NotFound` statics) — Wolverine would otherwise throw on a non-start message for a missing saga. `Customer.Email` is unaffected.

**Failure path — pending email claimed by another registration during the window.**
- **Given** customer `c-1`'s `EmailChange` saga is open with `PendingEmail = "ada.new@example.com"`, and in the interim a different customer registered (or changed into) that same email, so `ux_customers_email` would reject the update.
- **When** `c-1` issues `ConfirmEmailChange { customerId: "c-1" }`.
- **Then** the command is rejected (`EmailChangeConflict`) and the saga stays **open** (no `MarkCompleted()`) — `c-1`'s only forward paths are letting the window expire (5.7 drops it) or issuing a fresh `RequestEmailChange` for a different email (5.5's re-request path re-arms the saga). No row change, no event.

### 5.7 Drop an email change on timeout — *(scheduled)* `EmailChangeTimeout` *(saga, v1.1)*

**Happy path.**
- **Given** customer `c-1`'s `EmailChange` saga is open with `PendingEmail = "ada.new@example.com"` and no `ConfirmEmailChange` arrived before the deadline.
- **When** the scheduled `EmailChangeTimeout` fires.
- **Then** the `EmailChange` saga completes (`MarkCompleted()`, state deleted) and `Customer.Email` is **unchanged** — the pending change is silently dropped. The customer may issue a fresh `RequestEmailChange` to try again (opens a new saga; slice 5.5).

**Failure path — timeout fires after the saga already completed via confirmation.**
- **Given** customer `c-1`'s `EmailChange` saga already completed (slice 5.6 confirmed within the window).
- **When** the previously-scheduled `EmailChangeTimeout` fires anyway (the runtime offers no scheduled-message cancellation, the same property Bruun slices 3.4/4.7 and Inventory's `Replenishment` rely on).
- **Then** it is a silent no-op (`NotFound(EmailChangeTimeout)`) — `Customer.Email` (already updated by the confirm) is unaffected.

### 5.8 Register with credentials — `RegisterWithCredentials` *(auth, v1.3)*

**Happy path.**
- **Given** no customer/user exists for email `ada@example.com`.
- **When** the storefront issues `RegisterWithCredentials { email: "ada@example.com", displayName: "Ada Lovelace", password: … }` (the credentialed evolution of slice 5.1's `POST /customers`).
- **Then** an ASP.NET Core Identity user is created with the password hashed (`UserManager.CreateAsync`), a `Customer` row is written with the **same** id, `CustomerRegistered { customerId, email, displayName }` is enrolled in the EF-Core outbox **in the same transaction**, and the response is `201 Created`. (No stream event — Identity is not event-sourced; `CustomerRegistered` is the outbound PL notification, unchanged from slice 5.1.)

**Failure path — email already registered.**
- **Given** a customer/user already exists for `ada@example.com`.
- **When** `RegisterWithCredentials { email: "ada@example.com", … }` is issued again.
- **Then** rejected (`CustomerAlreadyRegistered`, mirroring slice 5.1's duplicate-email guard, plus ASP.NET Identity's own duplicate-user result) — no user, no row, no event; idempotent. The `ux_customers_email` index and Identity's uniqueness are the DB backstops.

**Failure path — password fails policy.**
- **Given** a password that fails the configured ASP.NET Core Identity password policy.
- **When** `RegisterWithCredentials` is issued.
- **Then** rejected with the Identity validation errors — no user, no `Customer` row, no event. (Registration is all-or-nothing: the user and the row are created together or not at all.)

### 5.9 Log in — issue a JWT — `LogIn` *(auth, v1.3)*

**Happy path.**
- **Given** customer `c-1` is registered with email `ada@example.com` and a known password.
- **When** the storefront issues `LogIn { email: "ada@example.com", password: … }` at `POST /login`.
- **Then** `SignInManager.CheckPasswordSignInAsync` succeeds and Identity returns a **signed JWT** with `sub = "c-1"`, an issuer and audience, and a lifetime — signed with Identity's asymmetric **private** key. No row change; the token is not persisted.

**Failure path — bad credentials.**
- **Given** a wrong password, or an unknown email.
- **When** `LogIn` is issued.
- **Then** `401 Unauthorized`, no token issued. The response does **not** distinguish "unknown email" from "wrong password" — no user enumeration.

### 5.10 Verify a token at a resource server — *(system, Conformist)* *(auth, v1.3)*

*Code lands in the consuming BCs (Catalog / Inventory / Orders), the "Identity → consumer" shape — not an Identity command.*

**Happy path (modeled; code in the resource server).**
- **Given** a resource server configured with Identity's **public key (config)**, and a request carrying `Authorization: Bearer <valid, unexpired JWT for c-1>`.
- **When** the request reaches that resource server.
- **Then** `AddJwtBearer` validates the token **fully offline** — signature against the public key, issuer, audience, lifetime — the principal's `sub` resolves to `c-1`, and the request proceeds as `c-1`. **No HTTP call into Identity**, per-request or at startup.

**Failure path — invalid or expired token.**
- **Given** a request with a missing, tampered, wrong-issuer/audience, or expired token.
- **When** it reaches the resource server.
- **Then** `401 Unauthorized`, rejected **locally** — again with no call into Identity (a tampered signature fails the public-key check; a stale token fails the lifetime check). This is the load-bearing demonstration that the no-sync-HTTP non-negotiable survives real auth.

### 5.11 Log out — `LogOut` *(auth, v1.3)*

**Happy path.**
- **Given** customer `c-1` holds a valid JWT.
- **When** the storefront logs out.
- **Then** the client **discards the token** and the `useCurrentCustomer` seam returns to unauthenticated; subsequent requests carry no bearer and are `401`. Because the JWT is stateless, the discarded token stays cryptographically valid until it expires — **server-side revocation is out of scope for round one** (a denylist, or short access tokens + refresh, is the deferred future increment; § 8).

---

## 7. Read Models / Projections

Identity has **none**. The `Customer` row IS the read model — there is no projection, no fold, no snapshot. This is the deliberate contrast with the event-sourced BCs (Workshop 001 § 7's async-projection teaser has no analogue here). The teaching point: when current-state CRUD is the right shape, you do not manufacture an event stream or a projection — you store the row and publish a notification. Identity is the **"when an event store would be over-engineering"** example, the counterpart to Catalog's **"when CRUD is fine"** document-store thread.

**v1.1 note — the `EmailChange` saga is not a customer-facing read model.** This still holds: the saga adds no new customer-facing view (no "email change pending" screen is modeled — the storefront simply awaits the confirm-or-timeout outcome). Its instance state (`PendingEmail`, whether open, the scheduled `EmailChangeTimeout`) is durably persisted (its own EF-Core table) but is **not yet a CritterWatch-visible diagnostic surface** in beta.1 — see § 8, open question 10 (empirically closed in [`critterwatch-saga-visibility-beta1.md`](../research/critterwatch-saga-visibility-beta1.md)).

**v1.3 note — auth adds no projection either.** The authentication increment introduces **no read model and no projection**. ASP.NET Core Identity persists its own relational tables (users, credentials, and whatever roles/claims tables the schema needs) — current-state rows, EF-Core-managed, in the `identity` schema — exactly the registry's shape, not a fold over events. The JWT is a stateless transit artifact, never materialized into a view. There is no "logged-in users" projection and no session table (round-one logout is client-side token discard, § 6 slice 5.11). This keeps Identity the pure **"when an event store would be over-engineering"** example, now including credentials: real auth, still zero projections.

---

## 8. Open Questions / Parking Lot

### Open questions — the spike promotion

1. **Duplicate-email policy (slice 5.1).** The spike performs no uniqueness guard. A kept registry should reject a duplicate with `CustomerAlreadyRegistered` (modeled in 5.1's failure path). Resolve in slice 5.1's OpenSpec proposal — and decide the uniqueness key (email, or a separate natural key).
2. **When does `CustomerRegistered` move to `CritterMart.Contracts`?** It lives inside the Identity service today (unconsumed). It graduates to the shared Contracts assembly (Published Language) the moment slice 5.4 lands a consumer — not before.
3. **`X-Customer-Id` resolution mechanism (slice 5.3).** Confirmed: via the slice-5.4 local read model, NOT a sync HTTP call into Identity (ADR 001). The open sub-question is *which BC needs it first* (Orders, to put a customer name on `OrderStatusView`?) — that drives where 5.4's read model lives.
4. **Customer lifecycle beyond registration.** Profile edits (`CustomerProfileUpdated`), deactivation, and merge are unmodeled. A registry that only inserts is round-two-minimal; the lifecycle is a future increment. **v1.1 update:** email-change (5.5–5.7) is the first lifecycle increment past registration, and it earns a saga specifically because of the confirm-or-expire window — name/address edits remain a single mutating command (no saga) per the research's "don't invent a workflow" reasoning; they stay unmodeled until a real need surfaces.

### Open questions — the `EmailChange` saga (v1.1)

7. **Re-request behavior (slice 5.5).** Resolved (corrected during design review — the first draft had this wrong): a second `RequestEmailChange` while one is already pending updates `PendingEmail` to the latest request but does **not** reschedule `EmailChangeTimeout` — the *original* deadline governs the confirm window, exactly mirroring why `Replenishment`'s re-open branch schedules no second `ReplenishTimeout` (Wolverine has no scheduled-message cancellation; a second timeout would either fire early against the new window or require tracking which timeout is authoritative). It does not stack, queue, or reject the second request. Revisit only if a product reason to give a re-request its own full window surfaces (would require a generation marker on the saga so a stale timeout can no-op instead of firing).
8. **Confirm-time conflict handling (slice 5.6).** Resolved, closing the research's deferred question: a confirm that collides with `ux_customers_email` (another registration claimed the email during the open window) returns a conflict and does **not** complete the saga. The customer's only forward paths are a timeout-drop (5.7) or a fresh `RequestEmailChange` for a different email (re-arms per item 7).
9. **Future Published-Language notification for email changes.** Identity emits `CustomerRegistered` on registration but this amendment models no analogous `CustomerEmailChanged` notification for a successful confirm (5.6) — no consumer needs it yet (5.4's future local read-model doesn't currently carry email). Deferred, not built; follows the same "graduates to `CritterMart.Contracts` on first consumer" convention already set for `CustomerRegistered` if one emerges.
10. **CritterWatch saga-lifecycle payoff.** **Resolved (empirically, v1.2) — does not hold as originally framed.** CritterWatch beta.1 does not surface saga *instances* for either saga: the Explore → Workflow page is a pre-1.0 stub replaying observed message traffic, not a structural saga view (confirmed for both the Marten `Replenishment` and the EF-Core `EmailChange`, so the gap is CritterWatch-version-driven, not a Marten-vs-EF-Core difference). Separately, `EmailChangeTimeout` is scheduled on a non-durable in-memory local queue and so is absent from CritterWatch's Scheduled view even once instance discovery ships. See [`critterwatch-saga-visibility-beta1.md`](../research/critterwatch-saga-visibility-beta1.md) for the full evidence trail; a durable-local-queue fix (which would also close the Scheduled-view gap) is deliberately not actioned here.

### Open questions — authentication (v1.3, ADR 023)

13. **Entity shape — one entity or two id-linked tables (slice 5.8).** Whether the ASP.NET Core Identity user and the registry `Customer` row are a single entity or two tables joined by id. `Customer.Id` is already a string chosen to line up with the identity seam ([`Customer.cs`](../../src/CritterMart.Identity/Customers/Customer.cs)); `IdentityUser.Id` is a string by default, so they can align. Resolve in slice 5.8's OpenSpec proposal — including the **naming collision**: CritterMart's existing `IdentityDbContext` shares a name with ASP.NET Core Identity's `Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<TUser>` base class (either rename the local context or derive it from the ASP.NET base).
14. **Registration — replace or layer (slice 5.8)?** Whether `RegisterWithCredentials` supersedes the spike's open `RegisterCustomer` outright, or the two coexist (an admin-provisioned customer without a self-set password). Ties to parking-lot item 12 (self-service vs. admin-provisioned). Resolve in 5.8's proposal.
15. **Token lifetime, refresh, and logout semantics (slices 5.9/5.11).** Round one: a short-ish access token + **client-side-discard logout**, no refresh flow, no server-side revocation. A refresh-token flow and a revocation denylist are modeled-as-future, not built. Decide the access-token TTL in 5.9's proposal — and whether it gets a demo-paced config knob mirroring `EmailChangeDeadline` (a short default for speaking pace, a realistic production value).
16. **Authorization (roles/claims) is deferred.** ADR 023 settles **authN** (who the caller is); **authZ** (what they may do — roles, policies, `[Authorize(Policy=…)]`) is not modeled here. Round-one CritterMart is single-actor (one customer role); an admin/backoffice actor is a vision non-goal. Deferred until a second actor surfaces; the JWT can carry role claims later without changing the issuer/resource-server shape.
17. **Signing-key management and rotation.** Where the asymmetric keypair lives — user-secrets/config for dev (mirroring the CritterWatch license-key pattern), a real secret store for prod — and the rotation cadence. ADR 023 accepts config-redeploy rotation for round one (the tradeoff for avoiding a JWKS call into Identity); the mechanics are a slice/implementation concern for 5.9/5.10.

### Long-road parking lot

11. ~~**Polecat-backed authentication.**~~ **Resolved by [ADR 023](../decisions/023-real-authentication-for-identity.md) (2026-07-07)** — real authentication is now chosen and modeled (slices 5.8–5.11, above), via **ASP.NET Core Identity**, *not* Polecat (the Polecat attribution was a factual error struck by ADR 009's 2026-07-07 correction). Identity-as-data-store (slices 5.1–5.4) and Identity-as-auth-issuer (5.8–5.11) now coexist in this one BC — both non-event-sourced relational CRUD. What remains parked past this pass: authorization/roles (item 16), refresh tokens and server-side revocation (item 15), and a possible future `CustomerEmailChanged` PL notification (item 9).
12. **Self-service signup vs. admin-provisioned customers.** The spike's `RegisterCustomer` is open. Whether customers self-register (storefront signup) or are provisioned is a product question deferred past this pass.

---

## 9. Document History

| Version | Date       | Notes                                                                                                                                                                                                                                                                                                                                                                                       |
| ------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| v1.0    | 2026-06-18 | Initial commit. Identity BC promoted from round-one stub to a kept EF-Core registry. Models 4 slices: **5.1 Register** / **5.2 Resolve** (realized by the spike on `spike/efcore-identity` @ `0ffe42e`); **5.3 Resolve from `X-Customer-Id`** / **5.4 Consume `CustomerRegistered`** (the future OHS/PL integration roadmap, modeled-not-built). Names the strategic-design relationship **Open-Host Service + Published Language** (context map amended in the same PR), with the OHS-for-frontend / PL-for-backends split reconciling the choice with ADR 001's no-sync-HTTP rule. Identity stays a **data store, not auth** (ADR 009). Authored as the design-return for the spike promotion; the spike code re-lands on `main` later via an OpenSpec proposal + narrative + implementation prompt that reference this model. |
| v1.1    | 2026-07-02 | **Saga #2 forward increment** (not yet implemented) — adds the **`EmailChange` saga**, Identity's first stateful consumer and CritterMart's *second* convention `Wolverine.Saga` (EF-Core-backed, contrast Inventory's Marten-backed `Replenishment`). Added slices **5.5 request / 5.6 confirm / 5.7 timeout-drop** to § 5 (Identity 4→7, P1 0→3) with a new `*(saga)*` marker; § 1 and § 2 gain amendment summaries; § 4 gains a new **"Saga state and saga messages (NOT events on any stream)"** subsection (`EmailChange` saga, `RequestEmailChange`, `ConfirmEmailChange`, `EmailChangeTimeout`); § 6 gains GWT scenarios 5.5–5.7 (happy + failure/idempotency/conflict paths, plus an unknown-customer guard and a re-request edge path); § 7 notes the saga is a CritterWatch diagnostic surface, not a customer-facing read model; § 8 adds open questions 7–10 (re-request behavior, confirm-time conflict handling, future PL notification, CritterWatch payoff) and renumbers the parking lot to 11–12. Gated on [ADR 009's second amendment](../decisions/009-polecat-deferred-for-round-one.md) (boring-CRUD stance holds) and bound by [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md) (relational-things-the-Wolverine-way guard). **Corrected during authoring, before this version was shared:** an independent design review (Fable 5, fresh-context) caught that the first draft's re-request path claimed to reschedule `EmailChangeTimeout` — but Wolverine has no scheduled-message cancellation, so the original timeout would still fire and drop the re-armed window early. Fixed to mirror `Replenishment`'s re-open branch exactly: a re-request updates `PendingEmail` only, the original deadline governs. The same review caught a missing existence guard on `RequestEmailChange` for an unknown `CustomerId` (now slice 5.5's second failure path) and flagged `docs/rules/structural-constraints.md` as stale against ADR 022 and ADR 009's amendment (fixed same-session, see that file's v1.7 entry). Authored as the design-return preceding the sibling OpenSpec proposal (`customer-registry` capability, per CLAUDE.md's one-capability-per-aggregate rule), narrative, and implementation — all consolidated into one PR per Erik's slice-PR preference ([[feedback-consolidate-slice-prs]]). Feasibility spike: `docs/research/wolverine-saga-feasibility.md` § Candidate #2. |
| v1.2    | 2026-07-07 | **Corrects § 7 and § 8 item 10's CritterWatch-visibility expectation against empirical findings**, closing retro 036's outstanding item (verified in [`docs/research/critterwatch-saga-visibility-beta1.md`](../research/critterwatch-saga-visibility-beta1.md), 2026-07-04). Beta.1 does not surface saga *instances* for either saga — the Explore → Workflow page is a pre-1.0 stub replaying observed message traffic, confirmed uniform across both the Marten `Replenishment` and the EF-Core `EmailChange` (not a store-backend difference). Separately, `EmailChangeTimeout` is scheduled on a non-durable in-memory local queue (`UseDurableLocalQueues()` not called), so it is also absent from the Scheduled view independent of the instance-discovery gap. Also corrects the matching stale "Saga Explorer" claim in `docs/demo-runbook.md` § 5c (same session). No slices, events, or commands changed — a doc-accuracy tidy, not a modeling amendment. |
| v1.3    | 2026-07-07 | **Authentication increment** (modeled, not yet implemented) — closes the auth stub the v1.0 scope deferred, per [ADR 023](../decisions/023-real-authentication-for-identity.md). Identity gains real authentication via **ASP.NET Core Identity** (`UserManager`/`SignInManager`, EF-Core-backed) and becomes the system's sole **auth issuer** of a self-validated, asymmetrically-signed **JWT** (customer id in `sub`) that Catalog/Inventory/Orders verify **offline** against Identity's config-distributed public key — zero HTTP into Identity, honoring the no-sync-HTTP non-negotiable (ADR 001). Added slices **5.8 register-with-credentials / 5.9 log-in-issue-JWT / 5.10 verify-token-at-resource-server / 5.11 log-out** to § 5 (Identity 7→11, P1 3→7) with a new `*(auth)*` marker; frontmatter `scope`/`status` updated (the "NOT authentication" clause superseded); § 1 and § 2 gain amendment summaries; § 3 gains a second **architect note** deriving the auth trust boundary as the no-sync-HTTP analogue of the OHS/PL split (offline validation, config public key, no JWKS, no token on the bus); § 4 gains an **"Auth commands and the issued token (NOT events, NOT saga state)"** subsection (`RegisterWithCredentials`, `LogIn`, the JWT, `LogOut`); § 6 gains GWT scenarios 5.8–5.11 (happy + failure paths incl. duplicate-email, weak-password, bad-credentials/no-enumeration, invalid/expired-token-rejected-locally, stateless-logout); § 7 notes auth adds no projection (still zero); § 8 adds open questions 13–17 (entity shape + `IdentityDbContext` naming collision, registration replace-or-layer, token lifetime/refresh/logout, authZ deferred, key management) and **resolves parking-lot item 11** (Polecat-backed auth → ASP.NET Core Identity, ADR 023). Load-bearing framings held throughout: auth is **still non-event-sourced** (relational user store extending the boring-CRUD foil per ADR 009 amendment / ADR 022 — zero stream events, no saga in this increment) and the **no-sync-HTTP rule survives real auth** (offline JWT validation). Paired with ADR 023, the context-map amendment, and `structural-constraints.md` v1.8, all in the same PR. Authored as the design-return preceding the sibling OpenSpec proposal (`customer-registry` capability), narrative, and implementation prompts for slices 5.8–5.11. |
