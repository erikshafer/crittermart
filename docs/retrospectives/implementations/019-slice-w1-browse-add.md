---
retrospective: 019
kind: implementations
prompt: docs/prompts/implementations/019-slice-w1-browse-add.md
deliverable: client/src/catalog/ (catalogSchema.ts, catalogQueries.ts, BrowsePage.tsx + 3 test files), client/src/cart/cartMutations.ts (+ test), client/src/api/client.ts (postCommand), client/src/router.tsx, docs/narratives/005-customer-storefront.md (v1.3), docs/skills/frontend/SKILL.md (v4)
date: 2026-06-16
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 019: W1 Browse + Add-to-cart (first optimistic mutation)

## Outcome summary

Shipped the **W1 storefront landing** and the project's **first mutation / first optimistic-UI**, bundling workshop slices **1.2** (browse) and **3.1** (add-to-cart) in one PR. The storefront is now a loop: **browse → add → (badge bumps) → W2 cart** — W2's read screen (#58) finally has a way to be populated.

- **`client/src/catalog/`** — the W1 noun feature folder: `ProductCatalogViewSchema` (the **second** per-read-model Zod schema, and the first against a **second service surface** — Catalog; a `z.array` of camelCase `{ sku, name, description, price }`), `catalogQueries.ts` (`productsQueryOptions`, **public-keyed** — no customer id in the key, the deliberate contrast with `cartKeys.mine`), and `BrowsePage` (a product grid of per-card `ProductCard`s; loading / empty / error states).
- **`client/src/cart/cartMutations.ts`** — `useAddToCart`, the three-callback optimistic hook (`onMutate` bumps the cart badge by adding/merging the line, `onError` rolls back to the snapshot, `onSettled` invalidates so the guess reconciles against the refetched `CartView`), plus the **pure `addLineToCart` merge** extracted for deterministic unit testing. Snapshots name + price from the loaded listing into `AddToCart` (product data reaches the cart only via the SPA snapshot — Narrative 005 Moment 2).
- **`client/src/api/client.ts`** — `postCommand`, the command-side counterpart of `fetchParsed` (same `X-Customer-Id` seam + boundary parse, plus a JSON body).
- **Router** — `/` became the W1 browse landing; the bootstrap `HomePage` wiring-check placeholder (+ its test) was **retired**, its job done.

**Tests**: client — **34 vitest tests, 0 failures** (17 new: 5 schema parse/drift, 3 catalog query incl. the public-key assertion, 6 mutation [3 pure-merge: cold-seed / append / sum-no-reprice; 3 hook: optimistic bump via a never-settling fetch, rollback on 500, the route-keyed-URL + `productSnapshot`-body contract], 3 `BrowsePage` populated/empty/click; the retired `HomePage`'s single bootstrap test removed). `npm run build` (tsc --noEmit + vite build, 237 modules) green. **No backend code touched.**

**Spec movement**: Narrative 005 → **v1.3** (Moments 1–2 built; the browse→add→W2 loop demonstrable); frontend skill → **v4** (Convention 3's optimistic-UI example converged onto the real `cartMutations.ts`; Convention 4 gained the route-keyed-command divergence note). No OpenSpec/workshop change — a screen-only bind of two existing contracts.

**Two forks resolved with previews before any code** (locked in the prompt): identity transport (route-keyed URL, screen-only) and the route/HomePage decision (`/` = Browse, retire HomePage).

## What worked

- **The live two-service wire verification grounded both OTel hops — and the casing claim — before the docs hardened.** Booting the Aspire stack and hitting the services on `:5101`/`:5103` confirmed at the wire: (a) `GET /products` returns **camelCase** `{ sku, name, description, price }` with `price` a JSON number — exactly what `ProductCatalogViewSchema` assumes; (b) the route-keyed `POST /carts/customer-demo/items` returns **201** `{ "cartId": ... }` (camelCase, matching `AddToCartResponseSchema`) and `GET /carts/mine` reads the line back **with name + price intact** — proving the `productSnapshot` field name binds (the retro-018 NRE bug class avoided); (c) **both** services echo `Access-Control-Allow-Origin: http://localhost:5173` (the two-service CORS the talk needs). This discharged the *two-service* browser-level verification the prior retros owed, at the wire.
- **Extracting `addLineToCart` as a pure function.** The hard part of the optimistic update — the merge rules (cold-seed a cart from `null`; append a new SKU; sum quantity on a re-add **without re-pricing**) — became three deterministic unit tests with no React-Query timing. The hook's `onMutate` is then a thin wire-up, and the merge mirrors the Cart aggregate's own rule, so the guess reconciles cleanly instead of flickering.
- **Per-card mutation hook.** Each `ProductCard` owns its own `useAddToCart()`, so `isPending` ("Adding…" + disabled) is per-button — the tapped card gives feedback without freezing the grid (the alternative, one shared page-level hook, would disable every button on any add). All cards manipulate the same cart cache key, so the header badge bumps regardless of which fired. This is the list-item-action template the W2-edit slices copy.
- **A contract-wiring test that guards the retro-018 bug class.** One mutation test asserts the POST body carries `productSnapshot` (not `snapshot`) and targets the **route-keyed** URL — encoding both the field-name lesson and locked decision 1 as a regression guard, not just prose.

## What was harder / notable

- **The read=header / command=route divergence is the slice's one real design wrinkle.** The cart *read* is header-keyed (`/carts/mine`) but the round-one `AddToCart` endpoint is route-keyed (`/carts/{customerId}/items`). Locked decision 1 took the pragmatic path — interpolate the seam's id into the path, header rides along, no backend change — and the divergence is now documented in the frontend skill's Convention 4 with an explicit "do not 'fix' this by stripping the path segment" note, so a future contributor doesn't mistake the legitimate route key for the `?customerId=` anti-pattern. The header-keyed `POST /carts/mine/items` harmonization is logged for a future tidy (a backend + OpenSpec change).
- **A fresh DB meant seeding products before the casing could be observed.** `GET /products` first returned `[]` (correctly — empty catalog is 200, not 404). Confirming `ProductCatalogView`'s casing required publishing `crit-001`/`crit-002` via `POST /products` first. (The empty-catalog 200 is itself now an asserted test.)
- **Observing the optimistic bump mid-flight needs a non-settling fetch.** The badge bump happens in `onMutate`, *before* the server answers, then reconciles on `onSettled`. To assert the optimistic state without it being immediately overwritten by reconciliation, the test stubs `fetch` with a promise that never resolves and uses `waitFor` to observe the cache after `onMutate`'s microtask. (The rollback test, conversely, uses a 500 and asserts the snapshot is restored.)
- **`BrowsePage` needs no router context** (unlike `CartPage`, whose empty state renders a `<Link>`). Its tests wrap only `QueryClientProvider` + `CurrentCustomerProvider` — a lighter harness than the W2 precedent.

## Methodology refinements

- **Verification-before-claiming, honored prospectively.** Retro 018 named the discipline ("don't write 'confirmed' until observed"). This session applied it from the start: the schema comment's "confirmed against the live response" was only written as fact *after* the boot observed camelCase. The pattern holds — and this time extended to the *command* path and *both* services' CORS, not just a single read.
- **The headless-verification ceiling, two-service edition.** Retro 018 lifted single-service casing + CORS to curl-confirmable. This session confirms the *full cross-service loop* is curl-confirmable: catalog read, route-keyed cart command (incl. the snapshot binding), cart read-back, and both ACAO headers. What still genuinely needs eyes: the SPA's in-browser grid render + badge-bump at `:5173`, and the OTel trace spanning **SPA → Catalog** and **SPA → Orders** in the Aspire dashboard. That residue is the irreducible visual criterion.
- **Pure-function extraction is the optimistic-UI testing pattern.** Pull the cache transform out of `onMutate` into a pure `(cache, change) → cache` function; unit-test the rules there; let the hook test cover only wiring (URL/body), rollback, and reconcile. The W2-edit mutations (3.2/3.3) should follow the same split.

## Outstanding / next-session inputs

- **Browser render + OTel trace = the owner's visual pass (stack left running).** The Aspire stack is **up** from this session's verification. `crit-001`/`crit-002` are published and a cart (`crit-001` ×1) is seeded for `customer-demo`. To finish the deferred criterion: load `http://localhost:5173/` (see the W1 grid), click `[ Add to cart ]` (watch the header badge bump), open `/cart` (the W2 render), and watch the `GET /products` (SPA→Catalog) and `POST /carts/{id}/items` (SPA→Orders) spans in the dashboard at `https://localhost:17090` (login token `05817a3248497dfb10f6af08745d606c`; OTLP/gRPC `:21090`). Tear down with `Ctrl+C` on the AppHost (or kill the `dotnet` AppHost PID) when done. *Not headless-checkable — by design.*
- **Next: the W2 command slices.** 3.2 remove / 3.3 change-qty (the `[-]/[+]/[x]` controls) reuse `useAddToCart`'s three-callback + pure-merge template; then 4.1 place-order → W3 (where optimism stops). `react-hook-form` gains its first consumer only at a form-bearing slice — add-to-cart is a button, so it stays unconsumed.
- **Harmonization tidy logged**: the header-keyed `POST /carts/mine/items` (so command and read share the identity transport) is a candidate future tidy — a backend + OpenSpec `shopping-cart` change, recorded in the frontend skill's Convention 4.
- **Design-return cadence**: this PR is self-interleaving (it bumps Narrative 005 + the skill), so it doubles as a design-return alongside the implementation work. The next pure-implementation slice (3.2/3.3) is clear to proceed.

## Spec-delta — landed?

**Named delta landed.** The prompt named **Narrative 005 → v1.3** as the sole spec movement (no OpenSpec/workshop change, because W1 binds two existing contracts). That landed: Narrative 005 is v1.3, its `## Forthcoming` now reads "mostly built — the storefront is a loop," Moments 1–2 are built, and a v1.3 `## Document History` row records the W1 browse + add-to-cart screen. The forward-confirmed "no behavioral spec delta" holds — no new SHALL, event, or read model. The frontend skill's v3→v4 convergence (Convention 3's optimistic example onto the real `cartMutations.ts`, plus the Convention 4 route-keyed-divergence note) landed alongside, as named.
