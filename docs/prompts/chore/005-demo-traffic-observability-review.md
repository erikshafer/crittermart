# Prompt: Chore 005 — Demo-traffic & observability review (CritterWatch fodder)

**Kind**: chore (review + truth-alignment tidy of the demo/observability tooling)
**Files touched**: `docs/demo-runbook.md`, `docs/demo-traffic.ps1`, possibly `src/CritterMart.Seeding/Program.cs` (comment/seed-set only) — exact set confirmed during Part C
**Mode**: solo review; no slice, no spec artifact changes
**Commit subject**: `tidy: demo-traffic & runbook — align to live config + CritterWatch fodder review`
**Branch**: `tidy/demo-traffic-observability-review`

---

## Framing

The active goal for CritterMart right now is to **observe CritterWatch in action** — JasperFx's monitoring
console (Jeremy Miller + Babu Nallagatla), which CritterMart serves as both a teaching reference and a
real-world UI/UX test-bed. CritterWatch only shows something when the system is *doing* something, so the
quality of the demo depends on the quality of the **traffic-generation and noise-making tooling**: the
`demo-traffic.ps1` generator, the boot-time `seeder`, and the `demo-runbook.md` that drives it all.

This session reviews that tooling end-to-end, identifies where it leaves CritterWatch's surfaces dark or
sparse, and applies the safe, small truth-alignment fixes. It is a **review + tidy**, not a feature slice.

**Important — this is a desk review, not a live-verify run.** Do **not** boot the stack or make network
calls. Read the source of truth (code + scripts) and reason from it. A separate `/verify` or live-boot
session validates behavior against a running stack.

---

## System under observation (condensed)

CritterMart is a single-seller ecommerce demo on the Critter Stack: .NET 10, Wolverine 6 + RabbitMQ
messaging, Marten 9 on PostgreSQL (shared DB, schema-per-service), .NET Aspire orchestration,
Wolverine.Http endpoints.

**Projects:** three event-sourced services — `Catalog` (products, price changes), `Inventory`
(receive/reserve/commit/release stock), `Orders` (cart + order + the cross-BC saga) — plus `Identity`
(EF Core customer registry, the *one* non-event-sourced BC), the `CritterWatch` console host, the
`AppHost` (Aspire), the one-shot `Seeding` console, and the `client/` Vite+React storefront.

**The order saga (cascading handlers over RabbitMQ — NOT a Wolverine Saga; see gotcha G6):**
`POST /orders` → `OrderPlaced` → cascade `ReserveStock` → `StockReserved` | `StockReservationFailed`;
on reserve → `PaymentAuthorizationRequested` → `PaymentAuthorized` | `PaymentDeclined`; authorized →
`StockCommitted` → `OrderConfirmed`; declined/timeout → `StockReleased` → `OrderCancelled`. Every
`POST /orders` also schedules a durable `OrderPaymentTimeout` self-message (`PlaceOrder.cs:91`).

**CritterWatch surfaces to assess:** Topology (routing graph), Services (per-service health/telemetry),
Projections (async projection health/lag), Durability (inbox/outbox + dead-letter counts), Event Store
Explorer (Marten streams), Scheduled Messages, Saga Explorer.

---

## Orientation files (read in this order — these are the source of truth)

1. `docs/demo-traffic.ps1` — the primary traffic generator. Read the whole file incl. the comment block.
2. `src/CritterMart.AppHost/Program.cs` — **the authority for runtime config.** The three demo knobs on
   the `orders` resource (lines ~72–85) decide saga timing and the decline path. Trust this over any doc.
3. `src/CritterMart.Orders/Features/PlaceOrder.cs` — the `POST /orders` contract (header identity, no body).
4. `src/CritterMart.Seeding/Program.cs` — the canonical seed set (3 products + `customer-demo`) and quantities.
5. `src/CritterMart.Inventory/Stock/StockLevel.cs` + `Features/ReserveStock.cs` — the stock math (see G5).
6. `docs/demo-runbook.md` — the operational runbook. **Treat as suspect** — it is the staleness target
   this session corrects (Steps 3–6, the `Payment` affordance box, Known gaps, curl quick reference).
7. `client/e2e/seeder.spec.ts` — small Playwright smoke; confirms the seeded product set, worth a glance.
8. Endpoint surface (skim only): `src/CritterMart.{Orders,Catalog,Inventory}/Features/*.cs`.

---

## Known drift & gotchas (verified against `main` @ `704775b`, 2026-06-28 — RE-VERIFY before acting)

These were found during prompt authoring by reading the code above. They are **leads, not gospel** —
`main` may have moved. Confirm each against the source-of-truth file named before you rely on or fix it.

- **G1 — `POST /orders` is header-keyed, body-ignored.** `PlaceOrder.cs:32` binds `[FromHeader
  X-Customer-Id]`; a missing/blank header returns **400**; there is no request body. The runbook's Steps 4,
  5a, 5b, the request-shapes table, and the curl block all POST `/orders` with a `{ customerId }` **body and
  no header** → they would 400 against the real endpoint. `demo-traffic.ps1` is already correct (PR #87/#101);
  the **runbook is the stale one.** This is the highest-impact drift — following the runbook on stage breaks
  every order.
- **G2 — decline threshold is $200, not $100.** AppHost injects `Payment__DeclineOverAmount = 200`
  (`Program.cs:77`). The runbook says **$100** in ~5 places, and its Step 5b example (`5 × $24.99 = $124.95`)
  is **below the real $200 threshold**, so following it produces a *confirmed* order — the compensation beat
  silently fails. To decline `crit-deluxe` ($24.99) you need qty ≥ 9 (`$224.91`); the seeder comment
  (`Program.cs:37`) already knows this. `demo-traffic.ps1` is correct (declines via `-DeclinePrice $250`, 1
  unit > $200).
- **G3 — three live demo knobs, not one.** AppHost sets `Payment__DeclineOverAmount=200`,
  `Payment__AuthDelay=00:03:00`, and `Orders__PaymentTimeout=00:07:00` on `orders`. The draft's `Cw__Telemetry`
  flag is **spike-only** (not on `main`). The runbook documents only the decline knob.
- **G4 — the 3-minute `AuthDelay` breaks the runbook's timing claims.** With `AuthDelay=00:03:00`, a placed
  order sits in `stock_reserved` for ~3 min before authorizing → `confirmed`. Runbook Step 4 claims the saga
  settles "usually < 1s" and polls 25×800ms (~20s) — that loop **never reaches `confirmed`** under the demo
  config. `demo-traffic.ps1`'s fire-and-forget (no polling) is the correct pattern *because* of this delay.
  This same delay is what makes Scheduled Messages + Durability light up richly (large in-flight backlog).
- **G5 — happy orders permanently consume stock; a dense/continuous run exhausts it.** `StockReserved`
  decrements `Available` immediately (`StockLevel.cs:48`) and `ReserveStock` refuses the whole order when
  `Available < Quantity` (`ReserveStock.cs:38`). Under the 3-min `AuthDelay`, every happy order holds a unit
  out of the pool for the full window before committing. With only `crit-001` + `crit-deluxe` (100 each =
  200 units) in the rotation, a dense `-Continuous` run drains the pool in ~3–4 min and **silently flips to
  producing `stock_unavailable` cancels** — an unplanned third flow. The script comment's "a sustained run
  never stalls on an exhausted SKU" is true only for short bursts. This is the #1 practical obstacle to
  "keep it busy for a long time" and should drive a Part B recommendation (periodic reseed, higher seed
  quantities, or a shorter `AuthDelay` for sustained runs).
- **G6 — Saga Explorer + async Projections are empty *by architecture*, not for lack of traffic.** Round one
  uses **inline** projections (CLAUDE.md) and the order saga is **cascading handlers**, not a Wolverine Saga.
  So CritterWatch's Saga Explorer and async-Projections health/lag panels stay dark no matter how much traffic
  you generate. **Verify** the projection lifecycles in each service's `Program.cs` (Inline vs Async) before
  asserting this in Part A — do not assume; CLAUDE.md allows "one async projection as a teaser."
- **G7 — the DLQ stays empty without a deliberate poison.** Nothing on `main` intentionally dead-letters a
  message, so Durability shows inbox/outbox movement but an empty dead-letter table. The spike's `PoisonPing`
  fills it (see Spike context).
- **G8 — `crit-rare` seed quantity drift.** `Seeding/Program.cs:41` seeds `crit-rare` at **3**; the runbook
  Step 3 table and the `demo-traffic.ps1` comment both say **1**. Align the docs to the seeder (3).
- **G9 — traffic orders never exercise customer-name enrichment.** `demo-traffic.ps1` uses ephemeral
  `traffic-xxxxxxxx` GUID customers that are not registered in Identity, so their orders carry
  `customerName = null` (the eventually-consistent degradation). The seeder registers `customer-demo`
  (`Program.cs:95`); routing some traffic through that id would light up the `LocalCustomerView` enrichment
  path (slices 5.3/5.4) and the Identity→Orders `CustomerRegistered` PL flow. Weigh in Part B.

---

## Spike context (`research/cw-telemetry-spike`, PR #103 — OPEN, NOT on `main`)

The spike branch deliberately stays off `main`. It adds, gated behind a `Cw__Telemetry` env flag:

- `demo-traffic.ps1` params `-LinesPerOrder`, `-MaxQuantity`, `-PoisonEvery` (multi-line / multi-qty orders,
  and an optional `POST /spike/poison` every Nth iteration to populate Dead Letters).
- `Spike/PoisonPing.cs` + `Spike/CwTelemetryFlag.cs` (the gated poison endpoint).
- **Async analytics projections** (`Analytics/ProductSalesLeaderboard.cs`, `Analytics/OrderLineItemsProjection.cs`)
  + an `OrderPlacedSignal` contract with per-service signal handlers — these exist specifically to light up
  the async-Projections surface that `main` leaves dark (see G6).

For each, assess in Part B whether a cleaned-up version is worth bringing to `main`. **Anything from the
spike is a separate PR — flag async projections / new handlers / new endpoints as "requires its own PR
(+ ADR for the async-daemon decision, given round one's inline-only stance)."** Do not import spike code in
this session.

---

## Goal

### Part A — Analysis (write in your response)

1. **Traffic shapes today.** Enumerate every flow `demo-traffic.ps1` + the runbook can drive, and label each
   automated vs. manual-only. (Correct the draft's premise: the *script* drives happy + payment-decline only;
   insufficient-stock is **manual** in the runbook, and the unplanned exhaustion path from G5 is a third
   shape that emerges under load.)
2. **CritterWatch surface coverage.** For each surface (Topology, Services, Projections, Durability/DLQ,
   Event Store Explorer, Scheduled Messages, Saga Explorer) say whether current traffic lights it up
   meaningfully or leaves it empty — and *why* (traffic gap vs. architectural gap, per G6/G7).
3. **Gaps & weaknesses.** Catalog endpoints never exercised by any script (`ChangeProductPrice`,
   `PublishProduct` post-seed); the customer-enrichment path (G9); `crit-rare` / stream diversity; stock
   exhaustion under sustained load (G5); and any fragility you notice.

### Part B — Recommendations (write in your response)

Prioritized list. For each: **What** (one sentence) · **Why** (which CritterWatch surface / gap, ref the
gotcha id) · **Effort** (S/M/L) · **File(s)**. Minimum areas: `demo-traffic.ps1` new flags worth having; a
sustained-run stock strategy (G5); whether a catalog-mutation script (price changes / new products) earns
its keep; whether routing traffic through `customer-demo` adds coverage (G9); and which spike features (if
any) to graduate — flagged as separate PRs per the Spike section.

### Part C — Implement the S-effort truth-alignment wins

Apply only changes that are clearly **S** and clearly safe: doc/script/comment alignment to the source of
truth, no new dependencies, no `.cs` feature work. **Verify each against the named source-of-truth file
first** (main may have moved). The pre-identified S-wins, in priority order:

1. **Runbook header identity (G1)** — Steps 4/5a/5b, the request-shapes table, and the curl block: place the
   order with the `X-Customer-Id` **header**, drop the ignored `{ customerId }` body. This is the must-fix.
2. **Runbook decline threshold (G2)** — change `$100` → `$200` everywhere, and fix the Step 5b example from
   `5 × $24.99` (won't decline) to qty ≥ 9 (`$224.91`).
3. **Runbook timing reality (G3/G4)** — document `Payment__AuthDelay=00:03:00` and `Orders__PaymentTimeout=00:07:00`
   as live AppHost knobs, and correct Step 4's "< 1s" claim + 20s poll loop so the poll budget exceeds the
   auth delay (or note fire-and-forget, as `demo-traffic.ps1` does).
4. **`crit-rare` quantity (G8)** — align the runbook table + the `demo-traffic.ps1` comment to the seeder's 3.

For each edit: make it, then state in one line what changed and which gotcha it closes. Anything you rate
M/L (new flags, async projections, catalog-mutation script, customer-demo routing): **document in Part B,
do not implement.**

---

## Working pattern

1. Read the orientation files (source of truth first, runbook last).
2. Re-verify each gotcha G1–G9 against its named file; note any that no longer hold.
3. Write Part A, then Part B.
4. Apply the verified Part C S-wins; keep edits inside the named files.
5. Close with the one-paragraph summary (Done-when).

---

## Constraints

- **No boot, no network calls.** Desk review only.
- **No `.cs` feature work.** Script + runbook (+ seed-set/comment) edits only. The single allowed code touch
  is a small `Seeding/Program.cs` seed-set or comment change *if* it is clearly S and you justified it in
  Part B (e.g. a higher seed quantity to mitigate G5, or a comment correction).
- **Keep `demo-traffic.ps1` backward-compatible** — existing parameter names and defaults must not change.
- **Spike features are not on `main`** — never assume `Cw__Telemetry` / `PoisonPing` / multi-line orders are
  present; any recommendation to graduate them is "requires separate PR (+ ADR)."
- **No new files** unless a new script is a Part B deliverable you decided to implement (unlikely for an
  S-only Part C). Prefer improving existing files.
- **No opportunistic edits** outside the named files (CLAUDE.md discipline).

---

## Spec delta

**None.** This is demo/observability tooling, not domain behavior — no narrative or workshop changes.

---

## Out of scope

- Live stack boot / UI verification (separate `/verify` or live-boot session).
- Importing any spike-branch code or the PR #103 feedback packet.
- Async-daemon / async-projection work on `main` (needs its own PR + ADR — round one is inline-only).
- CritterWatch version/dependency changes (that is chore/004's lane).
- Any `.cs` feature changes to the services.

---

## Done when

- Part A analysis written (in the response).
- Part B prioritized recommendation list written (in the response).
- Part C S-wins applied to the named files, each with a one-line "what changed / which gotcha".
- A one-paragraph summary: what changed vs. what was deferred (with effort labels).

No retrospective doc required for a pure truth-alignment tidy (CLAUDE.md "Tidy ceremony rule" — no spec
content authored). If Part C ends up landing new *script behavior* beyond doc alignment, add a brief
`docs/retrospectives/chore/005-demo-traffic-observability-review.md`.
