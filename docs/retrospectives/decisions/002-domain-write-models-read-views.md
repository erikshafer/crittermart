---
retrospective: 002
kind: decisions
prompt: docs/prompts/decisions/002-domain-write-models-read-views.md
deliverable: docs/decisions/{020-domain-write-models-read-views,021-verb-feature-folders}.md, src/CritterMart.Orders/Shopping/{Cart,CartView,CartLine}.cs, docs/rules/structural-constraints.md (v1.4)
date: 2026-06-16
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Decisions 002: Domain write-models + verb feature folders (ADR 020 + ADR 021; Cart pilot)

## Outcome summary

Shipped **two ADRs** and piloted both on Cart. **ADR 020**: aggregates are domain-named, `sealed`, immutable write models (`this with` evolution); the raw aggregate is never served; a public read is a separate `*View` projection from the same events. **ADR 021** (added mid-session): feature/slice folders are named for the **activity** (a verb — `Shopping/`, `Ordering/`); domain types keep canonical **noun** names, so an aggregate never needs a qualifying suffix. The Cart slice realizes both: `CartView` the mutable aggregate-class became **`Cart`** (the write aggregate — self-aggregating `Snapshot<Cart>(SnapshotLifecycle.Inline)`, the open-cart unique invariant, the `FetchForWriting`/`StartStream` target on the five cart write paths) and **`CartView`** (a dedicated inline read projection the storefront binds), and the slice folder moved `Cart/` → **`Shopping/`**. A shared `CartLines` helper keeps read and write folds consistent. ADR 020 refines ADR 008; MMO Reconnect ADR 005 is parallel prior art on domain-named aggregates, **not** a consistency target — CritterMart's verb folders deliberately diverge from that sibling's noun folders (different projects).

This was a **twice-refined session**. (1) A course-correction on the stance: the prior turn had proposed (and Erik initially selected) "codify the posture but *keep* `*View` on aggregates"; Erik corrected it — the defect was serving the raw aggregate, not the name — so the three forks were re-resolved (separate read/write; ADR + Cart pilot in one PR; dedicated read projection) and the prompt frozen on the corrected decisions. (2) A naming evolution as the collision was worked: a `using` alias → an interim `ShoppingCart` aggregate → finally the **verb-folder convention** (ADR 021), which keeps the canonical `Cart` in a `Shopping/` folder. Erik's prompt to *not over-weight cross-repo consistency with MMO* and his stated preference for verb namespaces in VSA drove the final shape.

**Tests**: full solution green — **99 backend tests, 0 failures** (Catalog 9, Inventory 16, Orders 71, CrossBc 3); `dotnet format` clean. The `CartView` wire shape is preserved exactly, so the W2 frontend (`CartViewSchema`, PR #58) is untouched — proven by the Alba integration tests deserializing `CartView` from the live response and the CrossBc test driving `AddToCart → PlaceOrder → ReserveStock` across services.

## What worked

- **Resolving the three forks with previews before any code.** The stance/sequencing/mechanism questions (AskUserQuestion + code previews for the read mechanism) meant the refactor ran without a mid-stream interrupt, and the corrected decisions were frozen into prompt 002 cleanly.
- **The dedicated-read-projection choice paid off in protection + clarity.** `Cart` never crosses the wire; `CartView` is projected independently. Two inline projections committing atomically is the idiomatic ES/CQRS shape the repo exists to teach.
- **Testcontainers made "tests stay green" verifiable end-to-end.** The Alba integration tests (real Postgres per run) validated the three hardest claims — the moved unique index still enforces one-open-cart-per-customer, `Query<Cart>` resolution works, and the wire shape is byte-for-byte preserved — not just the unit-level fold.
- **The Marten skill confirmed the immutable API before writing it.** `Snapshot<T>(SnapshotLifecycle.Inline)` with static `Create`/`Apply` returning `with` — the documented self-aggregating-record pattern — so the projection compiled and the inline snapshot behaved on the first green build (after clearing unrelated locks).
- **Wire-preservation kept the blast radius backend-only.** Because the `CartView` read model kept the same fields, the read endpoints (`ViewMyCart`, `CartEndpoint`) didn't change and the frontend was untouched.

## What was harder / notable

- **The `Cart` type ↔ `…Cart` namespace collision (CS0118 / CS0234) — three iterations to the clean answer, all within the PR.** A type named `Cart` in namespace `CritterMart.Orders.Cart` is ambiguous in cross-namespace files (the four `Features/` handlers + the test project). (1) First aliased (`using CartAggregate = …Cart.Cart;`, the frozen prompt's "fully-qualify, don't rename" call). (2) Then, instead of shipping the alias, the aggregate was renamed **`ShoppingCart`** — a noun-folder fix. (3) Finally, once Erik flagged that verb namespaces fit VSA and shouldn't be judged against MMO's noun folders, the clean resolution landed: a **verb feature folder** ([ADR 021](../../decisions/021-verb-feature-folders.md)). `Cart/` → `Shopping/`, and the aggregate is the canonical **`Cart`** again (a verb namespace never collides with a noun type; events/commands/view stay `Cart*` with no desync). `Ordering/Order` follows for the Order pilot; `Stock` (`StockLevel` ≠ `…Stock`) needs neither. Because #59 hadn't merged, the interim `ShoppingCart` was superseded in place — no introduce-then-revert in the merged history.
- **The lingering W2 Aspire stack locked build outputs.** The stack left running for the W2 visual check held `CritterMart.Orders` / `AppHost` binaries (MSB3027), failing the build until torn down. A first teardown (the dashboard-port process) missed a second AppHost process + the DCP process; a name-based sweep cleared them. The stack is now down — re-boot it to finish the W2 browser/OTel visual pass.
- **Near-identical Cart/CartView folds.** For the cart, the read model mirrors the aggregate, so the two projections look duplicative; the shared `CartLines` helper reduces it to the `with` wrapping. This is inherent to P1 for an aggregate whose public view ≈ its state — the decoupling (independent evolution) is the payoff, not DRY.

## Methodology refinements

- **A mid-session course-correction is legitimate, and the frozen-prompt discipline absorbs it cleanly.** The prompt is frozen on the *corrected* decisions (not the rejected "keep `*View`"); the rejected option lives in the ADR's "rejected alternatives" prose. Freezing intent at *the point the decisions settle* — even mid-conversation — keeps the record honest without rewriting history.
- **"ADR + pilot one aggregate" is the right shape for a cross-cutting model refactor.** Proving the pattern on Cart (the aggregate the frontend already consumes, so wire-compatibility is the binding constraint) establishes the template and de-risks Order/Stock before they touch their own consumers.
- **Surface a recurring structural smell, then resolve it with the owner rather than letting the workaround become the convention.** The collision went alias → `ShoppingCart` → verb folders across three iterations as Erik sharpened the intent — each surfaced, none silently shipped. A mid-PR refinement driven by the owner beats normalizing a stopgap.
- **A convention earns its place on the project's own merits, not by mirroring a sibling.** I over-leaned on "aligns with MMO ADR 005" as a yardstick; Erik's correction ("CritterMart isn't an MMO") was right. The final calls (separate read/write, verb folders) are justified intrinsically — the C# collision, the wire contract, CritterMart's VSA emphasis — and the verb-folder convention *deliberately diverges* from MMO's noun folders. Cross-repo provenance is honest to record; cross-repo *consistency* is not a goal.

## Outstanding / next-session inputs

- **Order and Stock pilots** reuse this template: `OrderStatusView` → an `Order` write aggregate in a **`Ordering/` verb folder** (keeps the canonical `Order` name; it is also the PMvH state) + an `OrderStatusView` read model; `StockLevelView` → `StockLevel` + a read model (no folder change — `StockLevel` ≠ `Stock`). Each its own PR.
- **Naming-collision approach is settled and codified** ([ADR 021](../../decisions/021-verb-feature-folders.md)): verb feature folders. Cart shipped as `Shopping/Cart` this PR (aliases gone, `ShoppingCart` superseded); `Ordering/Order` resolves Order at its pilot; `Stock` needs no change. The Catalog `Products/` verb (if ever adopted) is an open pick.
- **W2 visual verification still owed** (browser render + OTel trace) — the Aspire stack was torn down this session to clear build locks; re-boot to complete it.
- **`tidy: encode-ceremony-rule`** remains overdue (carried since retro 013).

## Spec-delta — landed?

**Named delta landed — and grew.** ADR 020 **and ADR 021** are Accepted and indexed (README rows 020, 021); `docs/rules/structural-constraints.md` gained the **Aggregate and read-model naming** section (v1.4 + history) covering both the read/write split and the verb-folder convention; the Cart pilot applied them (`Shopping/Cart` aggregate + `CartView` read model) with all 99 backend tests green and the wire shape preserved. The prompt named only ADR 020; ADR 021 emerged mid-session from the owner's input — recorded here as the honest delta-beyond-prompt. No workshop/narrative amendment — modeled behavior and wire contract unchanged (this restructures *how* the model is expressed and *where* it lives).
