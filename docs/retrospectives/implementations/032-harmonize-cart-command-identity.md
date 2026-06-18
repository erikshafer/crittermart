---
retrospective: 032
kind: implementations
prompt: docs/prompts/implementations/032-harmonize-cart-command-identity.md
deliverable: openspec/changes/harmonize-cart-command-identity/ (proposal + shopping-cart spec delta [3 MODIFIED] + design + tasks), src/CritterMart.Orders/Features/{AddToCart,ChangeCartItemQuantity,RemoveCartItem}.cs, tests/CritterMart.Orders.Tests/{AddToCart,ViewMyCart,ChangeCartItemQuantity,RemoveCartItem,PlaceOrder,PaymentTimeout,CartAbandonment,ListMyOrders}Tests.cs, tests/CritterMart.CrossBc.Tests/{CrossBcReserveStock,CrossBcReleaseStock,CrossBcCommitStock}SmokeTests.cs, client/src/cart/cartMutations.ts, client/src/cart/cartMutations.test.tsx, client/src/cart/CartPage.test.tsx, docs/skills/frontend/SKILL.md (Convention 4, v8), docs/narratives/005-customer-storefront.md (v1.9), docs/demo-runbook.md, docs/demo-traffic.ps1, docs/research/otel-trace-walkthrough.md
date: 2026-06-17
mode: collaborate
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 032: Harmonize cart command identity onto the X-Customer-Id header

## Outcome summary

Closed the **cart-command identity-transport divergence** that slices 3.1–3.3 logged as a deferred future tidy. The three cart commands move from route-keyed to **header-keyed**, matching the read:

- `POST /carts/{customerId}/items` → **`POST /carts/mine/items`**
- `POST /carts/{customerId}/items/{sku}/quantity` → **`POST /carts/mine/items/{sku}/quantity`**
- `DELETE /carts/{customerId}/items/{sku}` → **`DELETE /carts/mine/items/{sku}`**

Each endpoint now binds `[FromHeader(Name = "X-Customer-Id")] string? customerId` and guards a blank/missing header with `400` (mirroring `ViewMyCart`), distinct from the existing `409` (`NoOpenCart`/`CartItemNotPresent`). The `{sku}` stays on the route; only the customer key leaves the path. **No event, projection, index, or domain rule changed** — this is a transport change. The frontend's three mutation hooks drop the `${ctx.customerId}` path interpolation; the `X-Customer-Id` header the shared client already set on every request is now authoritative server-side too.

**The genuine fork — scope — was put to the owner.** Cart commands only (`shopping-cart`) vs +place-order (`order-lifecycle`); the owner chose **cart commands only**, keeping the PR to one capability and reviewable. Place-order (`POST /orders { customerId }`, body-keyed) is the one remaining identity transport, a flagged fast-follow. The route shape (`/carts/mine/*`) and guard pattern were settled by the `ViewMyCart` precedent, not forked.

**Tests**: **Orders 94, 0 failures** (91 + 3 new missing-header → 400 tests); **CrossBc 3, 0 failures**; `dotnet build` clean (only the pre-existing NU1507). Frontend **107 tests, 0 failures** across 16 files (the 3 cart-command URL-assertion tests strengthened to assert `/carts/mine/*`, `not.toContain(CUSTOMER)`, **and** the `X-Customer-Id` header — so the tests now prove identity travels via the header, not just that the URL changed); `tsc --noEmit` + `vite build` green.

**Spec movement**: OpenSpec **`shopping-cart`** gains **3 MODIFIED requirements** (each with the header-transport rule + a new "reject with no customer identity" scenario), validated `--strict` (Complete). **Narrative 005 → v1.9** records the transport change (a Moment 3 clause + a Document History row) though it is invisible to the screen journey. **Frontend SKILL Convention 4** flipped its "known divergence … do not fix" block to "harmonized" (v8), and the "third transport shape" → "two transports." The living demo surfaces (`demo-runbook.md`, `demo-traffic.ps1`, `otel-trace-walkthrough.md`) were repointed to `/carts/mine/*` + header so the demo doesn't break.

## What worked

- **The "landmine" was a green light, and recon proved it before any edit.** The carry-forward flagged SKILL Convention 4 as warning *against* touching the cart call-sites. Reading it in full showed the warning was conditional — "do not fix … *until* [the] harmonization (a backend + OpenSpec change)" — and this slice *is* that named harmonization. Reading the convention rather than trusting the one-line summary turned a perceived blocker into the sanction for the work.
- **Confirming the shared client already sent the header de-risked the whole frontend half.** Reading `api/client.ts` first established that `postCommand`/`deleteCommand` set `X-Customer-Id` from `ctx` on *every* request — so the frontend change was purely *deleting* the path interpolation, and the server simply starts trusting a header it was already receiving. The change is mostly removing coupling, not adding it.
- **Grep-first call-site mapping caught the true blast radius.** A single content-grep for the route across `tests/` surfaced 11 test files (not just the 3 command-test files) — `PlaceOrder`, `PaymentTimeout`, `CartAbandonment`, `ListMyOrders`, and the 3 CrossBc smoke tests all seed carts through the add route. Mapping every live call-site up front (and explicitly fencing the frozen prompts/retros/archive) meant the build broke nowhere.
- **Real-Testcontainers integration tests carry this change.** Both fixtures spin their own Postgres (+ RabbitMQ for CrossBc) via Testcontainers, so the Alba tests exercise the real `[FromHeader]` binding, real Marten, and the real route table — not a mock. The header path is also structurally identical to the already-live `GET /carts/mine`. That coverage is why a full Aspire boot was treated as a nice-to-have here rather than load-bearing (see Outstanding).

## What was harder / notable

- **Breadth, not depth, was the cost — and it crossed into the demo docs.** The handoff's scope preview ("cart Alba tests") undercounted: the harmonization broke *every* test that seeds a cart over HTTP, plus three living demo surfaces (`demo-runbook`, `demo-traffic.ps1`, `otel-trace-walkthrough`) that POST to the route. A breaking route change's real surface is "everything that calls the route," and the demo scripts are call-sites too — missing them would have silently broken the demo the talk depends on.
- **Disambiguating identical Alba blocks needed the status code, not the URL.** Several test files had two identical `_.Delete.Url("/carts/customer-X/items/crit-001")` / `ChangeCartItemQuantity(3)` blocks differing only by expected status (204 vs 409). Matching the two-line block (URL + `StatusCodeShouldBe`) made each edit unique; a URL-only match would have been ambiguous. The `nobody`/`decline-customer`/`commit-customer` literals each became a distinct header value, so the per-customer intent survived the move.
- **The OpenSpec MODIFIED delta re-states each requirement in full.** Unlike an ADDED requirement, a MODIFIED block replaces the whole requirement (header + SHALL text + every scenario), so the delta carries all the existing scenarios verbatim plus the new transport paragraph and the missing-identity scenario. Verbose by design — the archive sync needs the complete requirement, not just the diff.

## Methodology refinements

- **A conditional "do not touch" caveat names its own release condition — honor the condition, not the prohibition.** Convention 4 said don't strip the path *until the harmonization lands*. The mature read is to check whether the current work *is* the named release condition before treating a caveat as a blocker. (Generalizes: deferred-tidy markers encode *when* they unlock, and the unlocking PR is allowed — indeed obligated — to act on them.)
- **For a breaking route/contract change, grep the route across the whole repo and partition live vs frozen before editing.** The live set (source, tests, client, living docs/scripts) all move in lockstep in the one PR; the frozen set (prompts, retros, archived changes) keeps its historical references. Doing that partition first is what kept the PR both complete and clean.
- **Strengthen a test while you're moving it.** The 3 cart-command URL assertions were going to change anyway; adding `not.toContain(CUSTOMER)` + a header assertion turned "the URL is different" into "identity now travels via the header, never the path" — the actual property the change establishes. A transport change's tests should assert the *transport*, not just the new string.

## Outstanding / next-session inputs

- **Design-return interleave is DUE next (cadence).** This is the **2nd BC implementation since the #72 interleave** (#73 was the 1st; #78/#79 were non-BC demo-solidification). The next PR should be the interleave — a new narrative, the next BC workshop pass, or a `tidy:`. Flagged, not silently spent.
- **`openspec archive harmonize-cart-command-identity` — post-merge tidy.** Syncs the 3 MODIFIED requirements into `openspec/specs/shopping-cart/spec.md` (the active change → archive step). Should run after merge.
- **Place-order identity harmonization — the named fast-follow.** `POST /orders { customerId }` (body-keyed, `order-lifecycle`) is the one remaining identity transport. A separate slice: empty-body `POST /orders` + `PlaceOrder` record loses `CustomerId` + the checkout narrative moment + `usePlaceOrder` + `PlaceOrderTests`.
- **Live Aspire boot — DONE, green** (post-PR, at the owner's request). Booted the full stack on the branch and drove every new route against it: `POST /carts/mine/items` → **201**, `GET /carts/mine` → **200** (qty folds correctly), `POST …/items/{sku}/quantity` → **204**, `DELETE …/items/{sku}` → **204**; all four cart operations with **no header** → **400**; the **old** `POST /carts/{customerId}/items` → **404** (route genuinely gone); and a `POST /orders` from a cart built *entirely through the new header-keyed routes* walked the real cross-BC saga (reserve → authorize → commit) to **`confirmed`**. Real Marten + RabbitMQ, not mocks; stack torn down clean. This is on top of the real-Testcontainers Alba integration tests + the frontend hook tests — so the harmonization is confirmed at unit, integration, and full-stack levels.
- **Carry-forwards (unchanged, non-blocking):** the still-owed #77 trace-visual + #78 Docker-grouping owner eyeballs; the timed dry-run + screenshot-fold demo-solidification picks; the flaky `PaymentAuthorizationTests` Wolverine-shutdown race (`gh run rerun --failed`); NU1507 + `global.json` SDK pin; no frontend CI job; **POST-TALK: delete the AppHost `Payment__DeclineOverAmount=100` line**; CritterWatch trial expires 2026-07-10.

## Spec-delta — landed?

**Named delta landed.** The prompt named: OpenSpec `shopping-cart` **3 MODIFIED requirements** (header-transport rule + missing-identity scenario each); **Narrative 005 → v1.9** (Moment 3 clause + history row); **SKILL Convention 4 → harmonized (v8)**. All landed: the change validates `--strict` and shows Complete; Narrative 005 is v1.9 with the Moment 3 transport clause, the corrected "two transports" phrasing, and a Document History row; the SKILL's Convention 4 reads "harmonized," the "third transport" → "two transports," and the status line is v8. Four-step closure: **prompt named → session executed → this retro confirms → the OpenSpec change + Narrative 005 + the SKILL recorded.** The one deliberately-deferred edit — `openspec archive harmonize-cart-command-identity` (the spec-sync into the main `shopping-cart` spec) — is the standard post-merge tidy, named above.
