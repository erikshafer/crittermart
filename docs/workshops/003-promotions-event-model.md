---
workshop: 003
title: CritterMart Promotions — DCB-Protected Coupon Redemption (Definitions-Only Increment)
scope: The Promotions concept (coupon definitions, configuration-as-events) and coupon redemption at Orders checkout under a global per-coupon redemption cap enforced by Marten DCB inside the Orders store, per ADR 024. Promotions is definitions-only this increment — no fifth service, no new schema; every slice executes in the Orders BC.
status: Modeled and IMPLEMENTED (slices 6.1/6.2/6.3/6.4/6.5). The invariant-bearing DCB core shipped via implementations/039; the advisory cart-review query + W2/W3 storefront UI (slice 6.2) shipped via implementations/040; the second DCB — one-redemption-per-customer, the composite (coupon × customer) boundary (slice 6.5) — shipped via implementations/041.
version: v1.3
date: 2026-07-15
participants: session-runner in solo multi-persona mode (Facilitator, Domain Expert, Architect, Backend Developer, Frontend Developer, QA, Product Owner, UX); four forks resolved with the owner via AskUserQuestion before authoring (workshop location, coupon entry point, definition birth, cancellation semantics)
references:
  - docs/decisions/024-dcb-coupon-redemption-in-orders.md
  - docs/workshops/001-crittermart-event-model.md
  - docs/context-map/README.md
  - docs/rules/structural-constraints.md
  - docs/skills/event-modeling/SKILL.md
  - docs/decisions/002-shared-postgres-schema-per-service.md
  - docs/decisions/016-frontend-full-pipeline-ui-first-class.md
---

# Workshop 003 — CritterMart Promotions (DCB-Protected Coupon Redemption)

## 1. Scope

This workshop models the increment [ADR 024](../decisions/024-dcb-coupon-redemption-in-orders.md) chose: **coupon redemption at Orders checkout, enforcing a global per-coupon redemption cap via Marten's Dynamic Consistency Boundary (DCB) inside the Orders event store**. It is CritterMart's first DCB and the first use of the Bruun **configuration-as-events** adjunct pattern (coupon definitions as events).

**In scope (modeled here):**

- Coupon **definition** — `DefineCoupon` → `CouponDefined` on a coupon stream in the Orders store (Seller lane; seed-realized this round).
- Coupon **validation/pricing at cart review** — an advisory read-model query; nothing written.
- Coupon **redemption at checkout** — a tagged `CouponRedeemed` on the order stream, appended under the DCB cap check. The **cap-breach rejection** and the **`DcbConcurrencyException` race** are modeled as explicit failure scenarios, not implied.
- Redemption **release on order cancellation** — a compensating tagged `CouponRedemptionReleased`, mirroring Inventory's reserve/release symmetry as tag arithmetic.
- **One redemption per customer** *(added v1.3, slice 6.5)* — an **opt-in** `oneRedemptionPerCustomer` policy on the definition, enforced at checkout by a **second** DCB over a composite `(coupon × customer)` tag: CritterMart's first composite-tag boundary and an **existence** check (vs. the first DCB's count cap). ADR 024 §38's "more dynamic boundary."

**Out of scope (parked, § 8):** a standalone Promotions service (deferred by ADR 024), the remaining richer DCB variant (a shared discount budget — a summed value across streams), coupon lifecycle beyond definition (expiry, disable, edit), coupon stacking, and all implementation mechanics (deferred to the per-slice loop and the `marten-advanced-dynamic-consistency-boundary` skill).

**Decisions carried in, locked by ADR 024 (not re-litigated here):** the invariant is a **global per-coupon cap** ("usable ≤ *N* times, ever"); enforcement lives **inside the Orders store** because DCB is store-scoped and Orders is the one store every checkout flows through; redemption events are **tagged by a strong-typed `CouponId`** and checked via `FetchForWritingByTags` over `EventTagQuery().Or<CouponId>(id)` (verified first-class in the pinned Marten 9.11.0 — no Polecat, no version bump); the Orders store opts into the DCB schema (`tags TEXT[]` + GIN index on its `mt_events`); Promotions contributes **definitions only** this increment.

**Modeling forks resolved with the owner at session start (this workshop's own decisions):**

1. **This is a standalone Workshop 003**, not a Workshop 001 amendment — the Workshop 002 precedent (new concept = new workshop), with redemption slices drawn in the Orders lane and Workshop 001 carrying only a resolved-pointer amendment (its v1.13).
2. **The coupon enters at cart review, UI-held** — the W2 screen gains a coupon field backed by an advisory query; the code becomes an event only when `PlaceOrder` carries it. One write point; the Cart aggregate is untouched.
3. **Definitions are born as events** — `DefineCoupon` → `CouponDefined` (configuration-as-events), realized round one by the seeder, keeping cap *N* an event-sourced domain fact and the Published-Language graduation path intact.
4. **Cancellation releases the redemption** — `CouponRedemptionReleased`, so the DCB boundary counts redemptions **minus** releases and failed payments do not burn flash-sale slots.

## 2. Bounded-Context Summary

### Promotions (concept only — NOT a deployed service, NOT a new schema)

Owns the **coupon-definition vocabulary**: code, discount, redemption cap *N*. Modeled as a Published-Language contract Orders consumes; **realized this increment as events in the Orders store** issued by the seeder (`DefineCoupon`). There is no Promotions project, schema, queue, or Aspire resource. If a later round graduates Promotions to its own service (the deferred Customer-Supplier gate, ADR 024), the same `CouponDefined` contract crosses RabbitMQ instead of being seeded locally — the model here is written so that only the *transport* of definitions changes, not their shape.

### Orders (deployed; event-sourced) — the DCB host

Hosts everything executable in this workshop: the coupon stream (definitions), the advisory read models, the tagged redemption/release events on order streams, and the DCB cap enforcement at checkout. The Orders store is the **only** store that opts into the DCB schema. The Order aggregate's existing lifecycle (Workshop 001 slices 4.1–4.7, PMvH per ADR 007) is unchanged; redemption composes with it — `CouponRedeemed` rides the same checkout transaction as `OrderPlaced`, and release rides the existing cancellation decisions.

## 3. Timeline / Storyboard — Coupon Journey Around Place Order

The coupon journey wraps Workshop 001's Place Order storyboard (§ 3 there); only the coupon-specific interactions are drawn here. Everything is intra-Orders — **no RabbitMQ hop, no cross-BC edge** — which is precisely ADR 024's point: the consistency boundary is inside one store, spanning many order streams.

```mermaid
sequenceDiagram
    autonumber
    actor Seeder as Seeder (Seller lane)
    actor Customer
    participant FE as Frontend
    participant Orders as Orders Service
    participant Store as Orders Event Store (DCB-enabled)

    Seeder->>Orders: DefineCoupon { code: FLASH20, discountPercent, cap N }
    Note over Store: Coupon stream: CouponDefined<br/>CouponView updated (inline)

    Customer->>FE: Enter "FLASH20" on Cart Review (W2)
    FE->>Orders: GET coupon validation (query)
    Orders-->>FE: { valid, discountPercent } — advisory
    Note over FE: Discounted total shown.<br/>Code held in UI state — nothing written.

    Customer->>FE: Tap "Place Order"
    FE->>Orders: PlaceOrder { ..., couponCode: "FLASH20" }
    Note over Orders,Store: FetchForWritingByTags over<br/>EventTagQuery().Or&lt;CouponId&gt;(id)<br/>CouponUsage boundary: net count &lt; N?
    alt cap not reached
        Note over Store: Order stream: OrderPlaced { ..., discount, total }<br/>+ CouponRedeemed [tag: CouponId]<br/>(same append; cap re-checked at SaveChanges —<br/>DcbConcurrencyException on a losing race)
        Orders-->>FE: 201 { orderId }
    else cap reached
        Orders-->>FE: 409 CouponExhausted — no Order stream created
    end

    Note over Orders,Store: ...slices 4.2–4.7 proceed unchanged...
    opt order is cancelled (any reason)
        Note over Store: Order stream: OrderCancelled<br/>+ CouponRedemptionReleased [tag: CouponId]<br/>net count decremented — slot returns
    end
```

**Storyboard interpretation:**

- **Swim lanes.** Three lanes: **Seller/Promotions** (definitions — the seeder this round, a Promotions service later), **Customer/UI** (W2 coupon field, W3 discount line), and **Orders** (every write; the DCB boundary). There is no Inventory or payment change — those lanes are untouched from Workshop 001.
- **One write point.** The coupon code exists in exactly three durable places: the definition (`CouponDefined`), the redemption (`CouponRedeemed`), and the release (`CouponRedemptionReleased`). The cart-review check is a **query**; its answer is advisory and can go stale — the checkout append is the only authority. This advisory-vs-authoritative split is a deliberate teaching contrast.
- **The DCB moment.** At checkout, the handler does not load a `Coupon` aggregate — there is no single stream whose version could guard the cap. It loads the `CouponUsage` **boundary state** projected from every event tagged with this `CouponId` across all order streams, decides, appends, and lets Marten's tag-scoped concurrency detect a racing redemption at `SaveChangesAsync` (`DcbConcurrencyException`). The boundary aligns with **no aggregate** — the textbook DCB shape (ADR 024).
- **Where the discount lands.** `OrderPlaced` carries the priced outcome (`subtotal`, `discount`, `total`); payment (slice 4.3) authorizes the discounted total with no knowledge of coupons. Downstream slices 4.2–4.7 are unchanged.

## 4. Event Vocabulary

Additions to the Orders store. Past tense, no `Event` suffix, domain-meaningful — extends [Workshop 001 § 4](001-crittermart-event-model.md). This list is the authoritative naming source for the downstream OpenSpec proposal, narrative, and code.

### Orders — coupon definitions (configuration-as-events; Seller lane)

- **CouponDefined** — a coupon came into existence: `{ couponId, code, discountPercent, cap, oneRedemptionPerCustomer }`. Appended to a per-coupon stream in the Orders store. This is CritterMart's first **configuration-as-events** (Bruun) use: the definition — including cap *N* — is an event-sourced domain fact with an audit trail, not a config row. Issued by the seeder this round; the identical contract is what a future Promotions service would publish (Published Language, ADR 024). *(v1.3, slice 6.5)* the definition gained `oneRedemptionPerCustomer` (default `false`) — a **second policy dimension** on the same event, so checkout picks the boundary shape from configuration-as-events; a defaulted record field means every pre-6.5 `CouponDefined` folds as `false` (a non-breaking event evolution).

### Orders — coupon redemption (tagged; the DCB events)

- **CouponRedeemed** — a coupon was redeemed by an order at checkout: `{ orderId, couponId, couponCode, discount, perCustomer }`. Appended to the **order stream** it belongs to (per ADR 024's "real order streams" intent), **tagged with the strong-typed `CouponId`**. The tag is what lets the DCB boundary find every redemption regardless of which stream carries it. *(v1.3, slice 6.5)* for a per-customer coupon the event **also** carries the composite `CouponCustomerTag` tag and sets `perCustomer` (so the `Order` aggregate can rebuild the composite tag for the compensating release).
- **CouponRedemptionReleased** — a redemption was returned to the pool because its order was cancelled: `{ orderId, couponId }`. Appended to the same order stream, **tagged with the same `CouponId`** — and, for a per-customer coupon *(v1.3)*, **also** with the same composite `CouponCustomerTag`, so the customer's slot returns too. The compensation twin of `CouponRedeemed` — the tag-arithmetic mirror of Inventory's `StockReserved`/`StockReleased` pair, now on both boundaries.

### DCB boundary state (NOT an event, NOT a persisted read model)

- **CouponUsage** *(boundary state)* — the write-side decision state `FetchForWritingByTags` projects on demand from all events tagged with a `CouponId`: net count = redemptions − releases, checked against cap *N*. It is computed at write time inside the consistency boundary and **never persisted** — distinguish it from the advisory `CouponUsageView` below (§ 7). Mechanics defer to the `marten-advanced-dynamic-consistency-boundary` skill and ADR 024's verified Marten 9.11 symbols (`FetchForWritingByTags`, `EventTagQuery().Or<CouponId>(id)`, `DcbConcurrencyException`); the skill titles DCB as Polecat — CritterMart uses the Marten path.
- **CouponCustomerTag** *(tag; v1.3, slice 6.5)* — the strong-typed **composite** tag for the second DCB. Verified against the resolved Marten 9.15.1 assembly, `EventTagQuery` has no two-tag `.And<>()` conjunction (`AndEventsOfType` filters event *types*, and a tag stores a single scalar), so the `(coupon × customer)` pair is a **single-scalar tag whose value encodes both** — `CouponCustomerTag("{couponId}|{customerId}")`, structurally identical to `CouponId` and queried through the same `.Or<>()` path. Registered `RegisterTagType<CouponCustomerTag>("couponcustomer").ForAggregate<CustomerCouponUsage>()`; the composite value matches no event property, so it lands only by explicit `WithTag`.
- **CustomerCouponUsage** *(boundary state; v1.3, slice 6.5)* — the second boundary aggregate: the write-side state `FetchForWritingByTags<CustomerCouponUsage>` projects on demand from every event tagged with a specific `CouponCustomerTag` (one coupon, one customer). Net count = redemptions − releases for the pair, checked as an **existence** invariant (`>= 1` → reject) rather than a count-vs-cap. Id-less `[BoundaryAggregate]` like `CouponUsage`, **never persisted**; opened alongside the global-cap boundary in the same checkout transaction for a per-customer coupon.

### Commands and queries (NOT events)

- **DefineCoupon** — creates a coupon definition (slice 6.1). Seed-issued this round. *(v1.3)* gains an optional `oneRedemptionPerCustomer` (default `false`) — the per-customer policy the second DCB (slice 6.5) reads.
- **PlaceOrder — amended shape** — Workshop 001's slice 4.1 command gains an **optional `couponCode`** field. Absent → slice 4.1 behavior is byte-for-byte unchanged. Present → slice 6.3 composes with 4.1 in the same checkout transaction.
- **Coupon validation query** — the W2 cart-review advisory check (slice 6.2): resolves a code against `CouponView` (+ `CouponUsageView` for availability). Read-only.

## 5. Slice Table

Slice group **6.x** — the workshop-global numbering continues from Workshop 002's 5.x. Columns per [Workshop 001 § 5](001-crittermart-event-model.md); `*(query)*` and `*(system)*` markers as there. A new `*(dcb)*` marker denotes a slice whose write runs inside a Dynamic Consistency Boundary.

| #   | Slice                                          | Command                  | Events                                        | View                                             | BC     | Reads-from                                                        | Writes-to                                                                                                  | Priority |
| --- | ---------------------------------------------- | ------------------------ | --------------------------------------------- | ------------------------------------------------ | ------ | ----------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | -------- |
| 6.1 | Define a coupon (configuration-as-events)      | `DefineCoupon`           | `CouponDefined`                               | `CouponView`                                     | Orders | `CouponView` (code-uniqueness guard)                              | Coupon stream; `CouponView`                                                                                | P0       |
| 6.2 | Validate & price a coupon at cart review       | *(query)*                | —                                             | `CouponView`; `CouponUsageView` (advisory)       | Orders | `CouponView`; `CouponUsageView`                                   | — *(code held in UI state; nothing written)*                                                               | P1       |
| 6.3 | Redeem coupon at checkout *(dcb)*              | `PlaceOrder { couponCode }` | `CouponRedeemed` [tag: `CouponId`] (+ slice 4.1's `OrderPlaced` with discounted total, same transaction) | `OrderStatusView` (discounted total); `CouponUsageView` | Orders | `CouponView` (definition); **`CouponUsage` DCB boundary** (net count across all order streams) | Order stream (tagged); `OrderStatusView`; `CouponUsageView`                                                | P0       |
| 6.4 | Release redemption on order cancellation *(system)* | *(aggregate decision)* on `OrderCancelled` where the stream holds `CouponRedeemed` | `CouponRedemptionReleased` [tag: `CouponId`] | `CouponUsageView` | Orders | Order stream                                                      | Order stream (tagged); `CouponUsageView`                                                                   | P0       |
| 6.5 | Enforce one redemption per customer *(dcb)* | `PlaceOrder { couponCode }` for a `oneRedemptionPerCustomer` coupon | `CouponRedeemed` [tags: `CouponId` + `CouponCustomerTag`] (+ `CouponRedemptionReleased` doubly-tagged on cancel) | — *(boundary state only; no persisted per-customer view)* | Orders | `CouponView` (the `oneRedemptionPerCustomer` flag); **`CustomerCouponUsage` composite DCB boundary** (net count for the `(coupon × customer)` pair) | Order stream (doubly-tagged) | P0 |

**Slice count.** 5 new slices, all Orders BC (Promotions is a lane, not a deployed BC — ADR 024). P0: 4. P1: 1 (the advisory cart-review query — the DCB demo stands without it; the storefront UX wants it). Slice 6.5 *(added v1.3)* is the **second** DCB — the composite-tag variant ADR 024 §38 named — a same-PR draw+build (modeled and implemented in implementations/041).

**Pattern citations in the table.**

- **DCB** is cited on slice 6.3 via the `*(dcb)*` marker — CritterMart's first: the write's consistency boundary is the set of events tagged with the `CouponId`, spanning arbitrarily many order streams and aligning with no aggregate. Slice 6.4's release is an ordinary tagged append (no cap check on release), but its event participates in every future boundary read.
- **DCB (composite)** is cited on slice 6.5 *(added v1.3)* — CritterMart's **second** DCB and first **composite-tag** boundary: the consistency boundary is the set of events tagged with a `(coupon × customer)` `CouponCustomerTag`, and the check is **existence** (`>= 1`) not count. It **composes with** slice 6.3 — for a per-customer coupon both boundaries are opened and armed in the *one* checkout transaction, and the doubly-tagged `CouponRedeemed` participates in both. The per-customer invariant is enforced in practice by the cross-order existence check (a customer's *later* order refused because an *earlier* one redeemed); a single customer's concurrent checkouts are already serialized by the one-open-cart invariant, with the DCB assertion the transactional backstop.
- **Configuration-as-events (Bruun)** is cited on slice 6.1 — the first CritterMart use of the third adjunct pattern the event-modeling skill names (Klefter and Bruun temporal automation both landed in round one; this completes the set).
- Slice 6.3 **composes with** Workshop 001's slice 4.1 (same command, same transaction) and 6.4 composes with slices 4.5–4.7 (the three cancellation decisions). Those slices' modeled behavior is unchanged when no coupon is present.

### 5.1 Wireframe Deltas (proportional, per ADR 016)

Two existing screens gain coupon affordances; no new screen. Frozen Workshop 001 § 5.1 wireframes are untouched (append-only) — these are the deltas the frontend slice will bind.

**W2 Cart Review — coupon field (slice 6.2):**

```
│  Coupon code: [ FLASH20     ] (Apply)          │
│    ✓ FLASH20 — 20% off            − $8.00      │
│                                                │
│  Subtotal                           $40.00     │
│  Discount (FLASH20)                − $8.00     │
│  Total                              $32.00     │
│                    [ Place Order ]             │
```

- Apply fires the advisory query (slice 6.2). Invalid code → inline "This code isn't valid." Exhausted (advisory) → "This coupon is no longer available." The field is optional; an empty field means slice 4.1's unchanged checkout.
- The applied state is **UI-held**: a reload forgets it. That is accepted round-one behavior (parked, § 8).

**W3 Order Confirmation — discount line:** the order summary renders `subtotal / discount / total` from `OrderStatusView` when a discount is present; unchanged otherwise. The cap-breach rejection (slice 6.3) surfaces on W2 as a checkout error: "This coupon was just claimed by the last shopper — remove it or try another." — the customer decides, the system never silently charges full price.

**Slice 6.5 adds no wireframe delta** *(v1.3, backend-only)*: the per-customer refusal surfaces on W2 as the same checkout-error affordance the cap-breach uses — a `409` with a distinct title (`CouponAlreadyRedeemedByCustomer`) the storefront's existing ProblemDetails handling covers. Tailored copy ("You've already used this coupon.") and a per-customer *advisory* preview are follow-ons (§ 8).

## 6. GWT Scenarios

### 6.1 Define a coupon — `DefineCoupon`

**Happy path.**
- **Given** no coupon stream exists for code `FLASH20`.
- **When** the seeder issues `DefineCoupon { code: "FLASH20", discountPercent: 20, cap: 3 }`.
- **Then** a new coupon stream is created appending `CouponDefined { couponId, code: "FLASH20", discountPercent: 20, cap: 3 }`; `CouponView` resolves `FLASH20` → `{ couponId, discountPercent: 20, cap: 3 }`.

**Failure path — duplicate code.**
- **Given** `CouponDefined { code: "FLASH20", ... }` already exists.
- **When** `DefineCoupon { code: "FLASH20", ... }` is issued again.
- **Then** the command is rejected with `CouponCodeAlreadyExists`; no event. *(Round-one realization may use a `CouponView` unique index as the backstop — the open-cart partial-unique-index precedent; a code-uniqueness DCB would be over-engineering for seed-issued definitions. See § 8.)*

**Failure path — nonsensical definition.**
- **Given** any state.
- **When** `DefineCoupon` carries `cap < 1` or a discount outside (0, 100].
- **Then** the command is rejected at validation; no stream created.

### 6.2 Validate & price a coupon at cart review — *(query)*

**Happy path.**
- **Given** `CouponDefined { code: "FLASH20", discountPercent: 20, cap: 3 }` and an open cart totaling $40.00.
- **When** the customer applies `FLASH20` on W2.
- **Then** the query answers `{ valid: true, discountPercent: 20 }`; the UI shows `− $8.00`, total `$32.00`, and holds the code in UI state. **No event is written.**

**Failure path — unknown code.**
- **Given** no definition for code `BOGUS`.
- **When** the customer applies `BOGUS`.
- **Then** the query answers invalid; inline error; nothing held.

**Edge — advisorily exhausted.**
- **Given** `FLASH20` with cap 3 and `CouponUsageView` showing a net count of 3.
- **When** the customer applies `FLASH20`.
- **Then** the UI shows "no longer available" and does not apply the code. *(Advisory only: the view may lag or a slot may free up by cancellation moments later — the checkout boundary is the authority either way.)*

### 6.3 Redeem coupon at checkout — `PlaceOrder { couponCode }` *(dcb)*

**Happy path.**
- **Given** `CouponDefined { code: "FLASH20", discountPercent: 20, cap: 3 }`; two `CouponRedeemed` events tagged with its `CouponId` exist across other order streams (net count 2); the customer's cart holds items totaling $40.00.
- **When** the customer issues `PlaceOrder { customerId, couponCode: "FLASH20" }`.
- **Then** the handler resolves the definition via `CouponView`, opens the DCB boundary (`FetchForWritingByTags` over `EventTagQuery().Or<CouponId>(id)`), finds net count 2 < cap 3, and the new Order stream appends `OrderPlaced { orderId, items, subtotal: 40.00, discount: 8.00, total: 32.00 }` **and** `CouponRedeemed { orderId, couponId, discount: 8.00 }` tagged `[CouponId]` in the same transaction; the Cart stream appends `CartCheckedOut` per slice 4.1; downstream slices 4.2–4.4 proceed against the discounted total.

**Failure path — cap reached (the mandatory breach path).**
- **Given** `FLASH20` with cap 3 and a net count of 3 (three tagged redemptions, no releases).
- **When** `PlaceOrder { couponCode: "FLASH20" }` is issued.
- **Then** the boundary check finds the cap reached and the **placement is rejected** with `CouponExhausted` — **no Order stream is created, no event is appended**. The customer keeps their cart and decides: remove the code and place at full price, or try another code. The system never silently drops the discount and charges more.

**Failure path — the concurrent race (`DcbConcurrencyException`).**
- **Given** `FLASH20` with cap 3 and a net count of 2.
- **When** two customers issue `PlaceOrder { couponCode: "FLASH20" }` concurrently — both boundary reads see net count 2 and both proceed.
- **Then** one transaction commits the third redemption; the other's `SaveChangesAsync` throws **`DcbConcurrencyException`** (a tagged event landed inside its boundary after its read). The losing handler retries: the fresh boundary read now finds net count 3 = cap, and the placement is rejected with `CouponExhausted` per the breach path above. *Exactly one of the two racing orders exists afterward — the cap held under concurrency, which is the invariant's entire point and the talk's demo moment.*

**Failure path — unknown code at checkout.**
- **Given** no definition resolves for the carried code (bypassed UI, stale client).
- **When** `PlaceOrder { couponCode: "BOGUS" }` is issued.
- **Then** the placement is rejected with `CouponInvalid`; no Order stream. *(The API is the boundary; the W2 advisory check is a convenience, not a guard.)*

**Edge — no coupon.**
- **Given** any state.
- **When** `PlaceOrder` is issued without `couponCode`.
- **Then** slice 4.1's modeled behavior applies unchanged — no coupon event, no boundary opened, no discount fields beyond `subtotal = total`.

### 6.4 Release redemption on order cancellation — *(system, aggregate decision)*

**Happy path (release).**
- **Given** an Order stream holding `OrderPlaced` and `CouponRedeemed { couponId }` [tagged], and the order subsequently cancels — any reason: `stock_unavailable` (4.5), `payment_declined` (4.6), or `payment_timeout` (4.7).
- **When** `OrderCancelled` is appended by the owning cancellation slice.
- **Then** the same decision appends `CouponRedemptionReleased { orderId, couponId }` [tagged `CouponId`] to the same stream. The coupon's net count decrements; a slot returns to the pool.

**Edge — released slot is reusable.**
- **Given** `FLASH20` with cap 3, three redemptions and one release (net count 2).
- **When** a new `PlaceOrder { couponCode: "FLASH20" }` is issued.
- **Then** the boundary finds 2 < 3 and the redemption succeeds — the release genuinely returned capacity, the reserve/release symmetry Inventory already teaches, expressed as tag arithmetic.

**Edge — cancellation without a coupon.**
- **Given** an Order stream with no `CouponRedeemed`.
- **When** the order cancels.
- **Then** no release event — slices 4.5–4.7 behave exactly as modeled in Workshop 001.

**Edge — at most one release per redemption.**
- **Given** an Order stream holding `CouponRedeemed` and `CouponRedemptionReleased`.
- **When** any further event is considered for that stream.
- **Then** no second release can occur: `OrderCancelled` is terminal and appended once (Workshop 001's terminal-guard discipline), and the release rides that single append. The net count can never under-count below the true usage.

### 6.5 Enforce one redemption per customer — `PlaceOrder { couponCode }` *(dcb, composite tag; added v1.3)*

The second DCB: for a coupon defined `oneRedemptionPerCustomer`, a given customer may redeem it **at most once, ever**, enforced by a composite `(coupon × customer)` boundary opened alongside the global cap in the same checkout transaction. ADR 024 §38.

**Happy path — a per-customer coupon admits a customer once.**
- **Given** `CouponDefined { code: "FIRSTORDER", discountPercent: 15, cap: 100000, oneRedemptionPerCustomer: true }` and `customer-X` has not redeemed it.
- **When** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }`.
- **Then** the composite boundary (`FetchForWritingByTags<CustomerCouponUsage>` over `EventTagQuery().Or<CouponCustomerTag>(FIRSTORDER × customer-X)`) finds net count 0, and the new Order stream appends `OrderPlaced` (priced) and a `CouponRedeemed` tagged with **both** the `CouponId` and the `CouponCustomerTag`, in the same transaction as the global-cap check.

**Failure path — the same customer's second redemption (the mandatory per-customer refusal).**
- **Given** `FIRSTORDER` (per-customer) which `customer-X` has already redeemed once (composite net count 1).
- **When** `customer-X` issues `PlaceOrder { couponCode: "FIRSTORDER" }` again — a fresh cart, a later order.
- **Then** the composite boundary finds net count 1 and the **placement is rejected** with `CouponAlreadyRedeemedByCustomer` — **no Order stream is created, no event appended**. The per-customer check runs *before* the global-cap check, so the reason is honest even if the coupon were also near its global cap.

**Edge — a different customer still succeeds.**
- **Given** `FIRSTORDER` (per-customer) already redeemed once by `customer-X`.
- **When** `customer-Y` (who has not redeemed it) issues `PlaceOrder { couponCode: "FIRSTORDER" }`.
- **Then** the placement succeeds — `(FIRSTORDER × customer-Y)` is a distinct pair at net count 0. Each `(coupon, customer)` pair is an independent composite boundary.

**Edge — concurrent different customers all succeed (composite isolation).**
- **Given** `FIRSTORDER` (per-customer, high global cap) and several distinct customers, none having redeemed it.
- **When** they issue `PlaceOrder { couponCode: "FIRSTORDER" }` concurrently.
- **Then** every placement succeeds — distinct pairs do not false-conflict on the per-customer boundary (they contend only on the shared global cap, which is far from reached). *This is the reachable concurrency proof; a single customer's concurrent checkouts are serialized by the one-open-cart invariant, so a same-customer self-race is not the demonstrable case — the cross-order existence check is.*

**Edge — a cancelled per-customer redemption returns the customer's slot.**
- **Given** an Order by `customer-X` holding a `CouponRedeemed` for per-customer `FIRSTORDER` (tagged with both `CouponId` and the composite tag), which subsequently cancels for any reason.
- **When** `OrderCancelled` is appended.
- **Then** the compensating `CouponRedemptionReleased` carries **both** tags; the composite net count for `(FIRSTORDER × customer-X)` decrements to 0, and `customer-X` may redeem `FIRSTORDER` again — the reserve/release symmetry, now on both boundaries.

**Edge — a non-per-customer coupon lets one customer redeem more than once.**
- **Given** `CouponDefined { code: "FLASH20", cap: 3, oneRedemptionPerCustomer: false }` which `customer-X` has redeemed once (below the global cap).
- **When** `customer-X` issues `PlaceOrder { couponCode: "FLASH20" }` again.
- **Then** no composite boundary is opened and, the global cap permitting, the placement succeeds — the per-customer invariant does not apply to a global-cap-only coupon.

## 7. Read Models / Projections

- **`CouponView`** *(inline, single-stream)* — the definition lookup: `code → { couponId, discountPercent, cap }`. Folds `CouponDefined`. Serves slices 6.1 (uniqueness backstop), 6.2 (validation/pricing), and 6.3 (definition resolution at checkout). The realized shape of ADR 024's "seed/local read model in the Orders store."
- **`CouponUsageView`** *(advisory, multi-stream)* — per-coupon net usage for display: folds `CouponRedeemed` (+1) and `CouponRedemptionReleased` (−1), keyed by `couponId`. Serves slice 6.2's "no longer available" affordance and any seller-facing usage readout. **Advisory by design** — it is a projection and may lag; the authoritative count is only ever computed inside the DCB boundary at write time. Groups by the events' `couponId` member (ordinary multi-stream identity routing — it does not need tags; only the write-side boundary does). Lifecycle (inline vs. the async-teaser slot) is an open question, § 8.
- **`CouponUsage`** *(DCB boundary state — NOT persisted)* — see § 4. Named here only to fence the distinction: same arithmetic as `CouponUsageView`, different existence — computed transactionally at the write, never stored, never queried by the UI.
- **`CustomerCouponUsage`** *(DCB boundary state — NOT persisted; v1.3, slice 6.5)* — the second boundary aggregate, net count for one `(coupon × customer)` pair (see § 4). There is **no** persisted per-customer usage view: the advisory `GET /coupons/{code}/validate` stays global-only and anonymous, so the per-customer verdict is computed **only** at checkout inside the boundary (a per-customer advisory preview would need the caller's identity — a parked follow-on, § 8). The per-customer invariant is thus checkout-authoritative-only, an even purer advisory-vs-authoritative stance than the global cap.
- **`OrderStatusView`** *(existing, Workshop 001)* — gains the discounted-total dimension: `subtotal`, `discount`, `couponCode?` alongside the existing `total`. Additive, the `placedAt`/`cancelReason` enrichment precedent (W001 v1.11).

## 8. Open Questions / Parking Lot

### Open questions — deferred to the per-slice loop (the model above holds regardless)

1. **`CouponRedeemed` append mechanics.** ADR 024 pins the intent — the tagged event lives on the real order stream, in the same transaction as `OrderPlaced` — but the handler shape (how `StartStream` composes with `FetchForWritingByTags` in one session, where the retry-on-`DcbConcurrencyException` policy lives) is slice-level Marten/Wolverine mechanics. Defer to the `marten-advanced-dynamic-consistency-boundary` skill (noting its Polecat framing; CritterMart uses the Marten 9.11 path) and the OpenSpec proposal.
2. **`CouponUsageView` lifecycle.** Inline (simplest, consistent with ADR 008's inline-first stance) vs. async (a second async-teaser candidate alongside `CartAbandonmentReport`). The advisory role tolerates lag either way. Resolve in the slice's OpenSpec proposal.
3. **Code-uniqueness realization (slice 6.1).** A `CouponView` unique index (the open-cart partial-unique-index precedent) is the lean backstop while definitions are seed-issued. If coupon authoring ever becomes user-facing/concurrent, a uniqueness boundary is a textbook DCB — a natural second DCB for a later round, not this one.
4. **Discount shape.** Modeled as `discountPercent` for demo legibility; a flat-amount variant (or both) is a definition-payload detail the OpenSpec proposal may widen without touching the model.
5. **Demo coupon set.** Which codes/caps the seeder defines for the talk (a cap-3 flash coupon makes the race demonstrable by hand). Belongs to the demo-runbook update in the implementation session.

### Long-road parking lot

6. **One-redemption-per-customer** — ✅ **IMPLEMENTED (v1.3, slice 6.5, implementations/041).** The composite `(coupon × customer)` tag ADR 024 §38 named as the more dynamic boundary — CritterMart's second DCB and first composite-tag boundary, an opt-in `oneRedemptionPerCustomer` policy on the definition. Two follow-ons it surfaced, parked: a per-customer **advisory preview** (the `validate` query would need the caller's identity + a per-customer advisory view), and **tailored W2 copy** for the `CouponAlreadyRedeemedByCustomer` refusal (the generic ProblemDetails affordance covers it for now). A `perCustomerLimit > 1` generalization is a definition-payload widening if ever wanted (the modeled invariant is exactly "once").
7. **Shared discount budget** — a summed value across streams rather than a count; the third variant ADR 024 names.
8. **Promotions as a standalone service** — the deferred Customer-Supplier redemption gate; `CouponDefined` becomes a RabbitMQ-published contract, definitions cross as Published Language, and § 2's transport-only promise is cashed in.
9. **Coupon lifecycle** — expiry (`validUntil`), disable/enable (`CouponDisabled`/`CouponReenabled` — more configuration-as-events), edit-with-history. All additive to the coupon stream.
10. **Coupon stacking** — one coupon per order is the modeled rule; stacking would multiply boundary tags per checkout.
11. **Cart-persistent coupon application** — the rejected fork 2 alternative (a `CouponApplied` cart event) if reload-survival ever matters more than cart-aggregate blast radius.

## 9. Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-07-15 | Initial commit. The Promotions event model per ADR 024: slices 6.1–6.4 (define / validate / redeem-with-DCB / release-on-cancel), three new Orders-store events (`CouponDefined`; tagged `CouponRedeemed` + `CouponRedemptionReleased`), the `CouponUsage` DCB boundary state vs. advisory `CouponUsageView` distinction, W2/W3 wireframe deltas, and GWT scenarios including the mandatory cap-breach rejection and the `DcbConcurrencyException` race. Four session-start forks resolved with the owner: standalone Workshop 003 (W001 v1.13 carries the resolved pointer); cart-review UI-held coupon entry; definitions as configuration-as-events (first CritterMart use); release-on-cancel compensation. Modeled, not built — the per-slice loop follows. |
| v1.2    | 2026-07-16 | **Slice 6.2 IMPLEMENTED** (implementations/040, OpenSpec change `2026-07-16-slice-6-2-advisory-coupon-validate`; `coupon-promotion` MODIFIED with one ADDED requirement — the advisory validate query). The read-only `GET /coupons/{code}/validate` resolves a code against `CouponView` (+ `CouponUsageView` for the exhausted affordance) and answers a discriminated `{ code, status, discountPercent? }` — `valid` / `invalid` / `exhausted`, always `200`, writing nothing (§6.2 GWT). The endpoint shape was an owner fork (AskUserQuestion): action route + discriminated status + client-side pricing, over a RESTful-resource read and a server-priced variant. The **W2** coupon field previews `Subtotal / Discount (CODE) / Total` (priced client-side in integer cents against the live cart total) with inline "not valid" / "no longer available" errors, holds the code in UI state (a reload forgets it — §8 item 11 accepted), and rides checkout as the existing `?couponCode=` param (no checkout change). **W3** binds the already-shipped `subtotal`/`discount`/`couponCode` (a Zod schema bump — the fields were returned by `EnrichedOrderView` since #144 but unparsed). The §5.1 wireframe deltas are now bound; the advisory-vs-authoritative split (§3) is preserved — the preview never guards, checkout re-decides against the DCB boundary. Rode along: the ~10 stale `client/src` `X-Customer-Id` header-transport comments corrected to `Authorization: Bearer` (ADR 023 retired the header; retro 038 candidate folded here). |
| v1.1    | 2026-07-16 | **Slices 6.1 / 6.3 / 6.4 IMPLEMENTED** (implementations/039, OpenSpec change `2026-07-16-slices-6-1-6-3-6-4-coupon-dcb`; new capability `coupon-promotion` + `order-lifecycle` MODIFIED). CritterMart's first DCB, on **Marten 9.15.1** (ADR 024 verified 9.11.0; re-confirmed on the resolved assembly): `opts.Events.RegisterTagType<CouponId>("coupon").ForAggregate<CouponUsage>()` is the sole DCB opt-in (spike-confirmed — no `EnableDcb()`); the tagged `CouponRedeemed` rides the real order stream (`StartStream` + `session.Events.Append(orderId, evt.WithTag(...))`). The §8-item-1 open call resolved to a **canonical reload-and-retry loop** (`RedeemWithDcbAsync`, fresh session per attempt) after implementation revealed DCB optimistic concurrency is **cap-blind** — a bare pre-check under-admits under a burst; the retry admits exactly `cap`. §8 items 2–5 resolved: `CouponUsageView` inline (no async daemon); code-uniqueness via a unique index; `discountPercent`-only; demo set `FLASH20` (cap 3) + `WELCOME10`. Live-verified on the full Aspire stack (seeder-defined coupons, cap held at 3, discount priced through to `stock_reserved`). Slice **6.2** (advisory validate query + W2/W3 UI + stale-comment tidy) deferred to the next PR. Two model footnotes the build surfaced, recorded so the model matches the code: (a) the tagged redemption event carries the human `couponCode` too (for the view's display), not only the `couponId`; (b) the DCB boundary aggregate `CouponUsage` is intentionally id-less (`[BoundaryAggregate]`), which trips Marten 9.15.1's `DeleteAllDocumentsAsync` — a test-cleanup rough edge worked around with a SQL truncate (retro 039). |
| v1.3    | 2026-07-17 | **Slice 6.5 MODELED + IMPLEMENTED** (implementations/041, OpenSpec change `slice-6-5-per-customer-redemption-dcb`; `coupon-promotion` MODIFIED *Define a coupon* + *Release a redemption* and ADDED *Enforce one redemption per customer at checkout*). CritterMart's **second DCB** and first **composite-tag** boundary — a same-PR draw+build (this slice was modeled here and built in the one session). The one modeling fork resolved with the owner (`AskUserQuestion`): per-customer is an **opt-in `oneRedemptionPerCustomer` policy on `CouponDefined`** (configuration-as-events extended), not a universal law and not a distinct coupon kind. **Composite-tag API verified before modeling** (the handoff's flagged risk): the resolved `JasperFx.Events` **v2.27.0.0** `EventTagQuery` has `For`/`Or`/`Or<TEvent,TTag>`/`AndEventsOfType` but **no two-different-tag `.And<>()`** and stores a single scalar per tag — so the `(coupon × customer)` boundary is a **single-scalar `CouponCustomerTag("{couponId}|{customerId}")`**, structurally identical to `CouponId`, registered/queried through the same proven path (no new Marten API, no spike, no version bump). Checkout opens the composite boundary **before** the global cap for a per-customer coupon (honest reason wins) and rejects a second redemption `409 CouponAlreadyRedeemedByCustomer`; the doubly-tagged `CouponRedeemed`/`CouponRedemptionReleased` arm/decrement **both** boundaries in one transaction. Model correction the build surfaced (recorded so the model matches the code): the per-customer invariant's *reachable* proof is the **cross-order existence check**, not a same-customer self-race — the one-open-cart invariant already serializes a single customer's checkouts (a same-cart double-submit conflicts on the *cart* stream, not the DCB), so the DCB assertion is the transactional backstop and the meaningful concurrency test is *different customers concurrent → all succeed* (composite isolation). 127 Orders tests green (+6 coupon integration, +2 folds); demo coupon `FIRSTORDER` (15%, cap 100000, 1/customer). Parked follow-ons (§8 item 6): a per-customer advisory preview + tailored W2 copy. |
