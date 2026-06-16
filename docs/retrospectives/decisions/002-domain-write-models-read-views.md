---
retrospective: 002
kind: decisions
prompt: docs/prompts/decisions/002-domain-write-models-read-views.md
deliverable: docs/decisions/020-domain-write-models-read-views.md, src/CritterMart.Orders/Cart/{Cart,CartView,CartLine}.cs, docs/rules/structural-constraints.md (v1.4)
date: 2026-06-16
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Decisions 002: Domain write-models vs. `*View` read models (ADR 020 + Cart pilot)

## Outcome summary

Shipped **ADR 020** and piloted it on Cart. The decision: aggregates are domain-named, `sealed`, immutable write models (`this with` evolution); the raw aggregate is never served; a public read is a separate `*View` projection from the same events. The Cart slice realizes it: `CartView` the mutable aggregate-class became **`ShoppingCart`** (the write aggregate — self-aggregating `Snapshot<ShoppingCart>(SnapshotLifecycle.Inline)`, the open-cart unique invariant, the `FetchForWriting`/`StartStream` target on the five cart write paths) and **`CartView`** (a dedicated inline read projection the storefront binds). A shared `CartLines` helper keeps read and write folds consistent. ADR 020 refines ADR 008 and aligns with MMO Reconnect ADR 005.

This began as a **mid-session course-correction**. The prior turn had proposed (and Erik initially selected) "codify the posture but *keep* `*View` on aggregates." Erik then corrected it: the real defect was serving the raw aggregate as a view, not the name. Three forks were re-resolved — stance (separate, don't keep `*View`), sequencing (ADR + Cart pilot in one PR), read mechanism (dedicated read projection, not aggregate+mapping) — and the prompt was frozen on the *corrected* decisions.

**Tests**: full solution green — **99 backend tests, 0 failures** (Catalog 9, Inventory 16, Orders 71, CrossBc 3); `dotnet format` clean. The `CartView` wire shape is preserved exactly, so the W2 frontend (`CartViewSchema`, PR #58) is untouched — proven by the Alba integration tests deserializing `CartView` from the live response and the CrossBc test driving `AddToCart → PlaceOrder → ReserveStock` across services.

## What worked

- **Resolving the three forks with previews before any code.** The stance/sequencing/mechanism questions (AskUserQuestion + code previews for the read mechanism) meant the refactor ran without a mid-stream interrupt, and the corrected decisions were frozen into prompt 002 cleanly.
- **The dedicated-read-projection choice paid off in protection + clarity.** `Cart` never crosses the wire; `CartView` is projected independently. Two inline projections committing atomically is the idiomatic ES/CQRS shape the repo exists to teach.
- **Testcontainers made "tests stay green" verifiable end-to-end.** The Alba integration tests (real Postgres per run) validated the three hardest claims — the moved unique index still enforces one-open-cart-per-customer, `Query<Cart>` resolution works, and the wire shape is byte-for-byte preserved — not just the unit-level fold.
- **The Marten skill confirmed the immutable API before writing it.** `Snapshot<T>(SnapshotLifecycle.Inline)` with static `Create`/`Apply` returning `with` — the documented self-aggregating-record pattern — so the projection compiled and the inline snapshot behaved on the first green build (after clearing unrelated locks).
- **Wire-preservation kept the blast radius backend-only.** Because the `CartView` read model kept the same fields, the read endpoints (`ViewMyCart`, `CartEndpoint`) didn't change and the frontend was untouched.

## What was harder / notable

- **The `Cart` type ↔ `…Cart` namespace collision (CS0118 / CS0234) — surfaced, then resolved within the PR.** A type named `Cart` in namespace `CritterMart.Orders.Cart` is ambiguous in cross-namespace files (the four `Features/` handlers + the test project). First aliased (`using CartAggregate = …Cart.Cart;`, the frozen prompt's "fully-qualify, don't rename" call). Discussed with Erik, who chose the cleaner resolution: **rename the aggregate to `ShoppingCart`** — the natural ecommerce compound (echoing the `shopping-cart` capability), which differs from the namespace so all five aliases deleted. The events/commands/read-model stay `Cart*`; only the aggregate carries the full compound. **`Order` takes the complementary tack** (Erik's call): a verb feature folder `Ordering/` keeping the canonical `Order` — a verb never collides with a noun, and dodges the redundant `CritterMart.Orders.Orders` a plural folder would produce. `Stock` (`StockLevel` ≠ `…Stock`) needs neither.
- **The lingering W2 Aspire stack locked build outputs.** The stack left running for the W2 visual check held `CritterMart.Orders` / `AppHost` binaries (MSB3027), failing the build until torn down. A first teardown (the dashboard-port process) missed a second AppHost process + the DCP process; a name-based sweep cleared them. The stack is now down — re-boot it to finish the W2 browser/OTel visual pass.
- **Near-identical Cart/CartView folds.** For the cart, the read model mirrors the aggregate, so the two projections look duplicative; the shared `CartLines` helper reduces it to the `with` wrapping. This is inherent to P1 for an aggregate whose public view ≈ its state — the decoupling (independent evolution) is the payoff, not DRY.

## Methodology refinements

- **A mid-session course-correction is legitimate, and the frozen-prompt discipline absorbs it cleanly.** The prompt is frozen on the *corrected* decisions (not the rejected "keep `*View`"); the rejected option lives in the ADR's "rejected alternatives" prose. Freezing intent at *the point the decisions settle* — even mid-conversation — keeps the record honest without rewriting history.
- **"ADR + pilot one aggregate" is the right shape for a cross-cutting model refactor.** Proving the pattern on Cart (the aggregate the frontend already consumes, so wire-compatibility is the binding constraint) establishes the template and de-risks Order/Stock before they touch their own consumers.
- **Surface a recurring structural smell, then resolve it with the owner rather than letting the workaround become the convention.** The namespace collision was first aliased, then — instead of shipping the alias — surfaced to Erik and resolved cleanly within the same PR (`ShoppingCart` for the cart aggregate; a `Ordering/` verb folder planned for Order). A mid-PR refinement driven by the owner's input beats normalizing a stopgap.

## Outstanding / next-session inputs

- **Order and Stock pilots** reuse this template: `OrderStatusView` → an `Order` write aggregate in a **`Ordering/` verb folder** (keeps the canonical `Order` name; it is also the PMvH state) + an `OrderStatusView` read model; `StockLevelView` → `StockLevel` + a read model (no folder change — `StockLevel` ≠ `Stock`). Each its own PR.
- **Naming-collision approach is settled** (no separate namespace-pluralization PR owed): `ShoppingCart` resolved the cart aggregate this PR (aliases deleted); the `Ordering/` verb folder resolves Order at its pilot.
- **W2 visual verification still owed** (browser render + OTel trace) — the Aspire stack was torn down this session to clear build locks; re-boot to complete it.
- **`tidy: encode-ceremony-rule`** remains overdue (carried since retro 013).

## Spec-delta — landed?

**Named delta landed.** ADR 020 is Accepted and indexed (README row 020); `docs/rules/structural-constraints.md` gained the **Aggregate and read-model naming** section (v1.4 + history); the Cart pilot applied the stance with all 99 backend tests green and the wire shape preserved. No workshop/narrative amendment — the modeled behavior and the wire contract are unchanged (this restructures *how* the model is expressed), so the forward-confirmed "no behavioral spec delta" holds.
